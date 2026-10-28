#!/usr/bin/env python3
"""Import local Codex/Claude transcripts without asking a model to quote prompts."""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
CONTEXT_PREFIXES = ('<environment_context>', '<external_codex_apps_open_page>',
                    '# AGENTS.md instructions for', '<user_instructions>',
                    '<permissions instructions>', '<collaboration_mode>')


def rows(path):
    with path.open(encoding='utf-8') as stream:
        for index, line in enumerate(stream):
            try:
                yield index, json.loads(line)
            except json.JSONDecodeError:
                # A live writer may not have finished its last line yet.
                if not line.endswith('\n'):
                    return
                raise ValueError(f'Malformed transcript: {path.name}, line {index + 1}')


def text_content(content):
    if isinstance(content, str):
        return content
    return '\n'.join(x.get('text', '') for x in content or []
                     if isinstance(x, dict) and x.get('type') in ('text', 'input_text', 'output_text'))


def in_repo(cwd, root):
    try:
        return Path(cwd).resolve().is_relative_to(root.resolve())
    except (TypeError, ValueError):
        return False


def parse_transcript(path, tool, root):
    records = []
    session = path.stem
    eligible = False
    current = None
    seen = set()
    for index, row in rows(path):
        if tool == 'codex':
            payload = row.get('payload', {})
            if row.get('type') == 'session_meta':
                eligible = in_repo(payload.get('cwd'), root) and payload.get('thread_source', 'user') == 'user'
                session = payload.get('id', session)
            if not eligible or row.get('type') != 'response_item' or payload.get('type') != 'message':
                continue
            role = payload.get('role')
            text = text_content(payload.get('content'))
            meta = payload.get('internal_chat_message_metadata_passthrough', {})
            kinds = meta.get('content_item_kinds', [])
            if role == 'user':
                if kinds and not any(k.startswith('user.') for k in kinds):
                    continue
                if not kinds and text.lstrip().startswith(CONTEXT_PREFIXES):
                    continue
            elif (role != 'assistant' or payload.get('channel') not in (None, 'final')
                  or payload.get('phase') not in (None, 'final_answer')):
                continue
            source_id = payload.get('id') or str(row.get('ordinal', index))
        else:
            if row.get('isSidechain') or not in_repo(row.get('cwd'), root):
                continue
            session = row.get('sessionId', session)
            role = row.get('type')
            if role not in ('user', 'assistant'):
                continue
            content = row.get('message', {}).get('content')
            if role == 'user' and isinstance(content, list) and any(
                    isinstance(x, dict) and x.get('type') == 'tool_result' for x in content):
                continue
            if role == 'user' and (row.get('isMeta') or row.get('isCompactSummary')
                                   or row.get('promptSource') == 'system'):
                continue
            text = text_content(content)
            if role == 'user' and text.startswith('[Request interrupted by user'):
                continue
            source_id = row.get('uuid') or str(index)
        blocks = payload.get('content', []) if tool == 'codex' else content or []
        attachments = [x.get('type') for x in blocks if isinstance(x, dict)
                       and x.get('type') in ('image', 'input_image')]
        if not text and not (role == 'user' and attachments):
            continue
        key = f'{tool}:{session}:{source_id}'
        if key in seen:
            continue
        seen.add(key)
        if role == 'user':
            current = {'id': key, 'tool': tool, 'session': session,
                       'timestamp': row.get('timestamp', ''), 'prompt': text,
                       'attachments': attachments,
                       'responses': []}
            records.append(current)
        elif current is not None:
            current['responses'].append({'id': key, 'text': text,
                                         'timestamp': row.get('timestamp', '')})
    return records


def atomic_write(path, text):
    tmp = path.with_name(path.name + '.tmp')
    tmp.write_text(text, encoding='utf-8')
    os.replace(tmp, path)


def fenced(text):
    longest = max((len(part) for part in text.split('\n') if part and set(part) == {'`'}), default=0)
    # More backticks than any run prevents submitted Markdown closing the quote.
    import re
    longest = max(longest, max((len(m.group()) for m in re.finditer(r'`+', text)), default=0))
    fence = '`' * max(3, longest + 1)
    return f'{fence}text\n{text}' + ('' if text.endswith('\n') else '\n') + f'{fence}\n'


def render(records):
    parts = ['# Prompt log\n\nAutomatically copied from local chat histories. '
             'All user prompts are included; injected context and tool results are excluded.\n\n'
             'This file is generated. Keep edits and outcome notes in `prompt-notes.md` '
             'or make a separate filtered copy. Times below are UTC. '
             'Saved responses are the assistant’s reports, not independent verification.\n']
    for record in sorted(records, key=lambda r: (r['timestamp'], r['id'])):
        short_id = hashlib.sha256(record['id'].encode()).hexdigest()[:12]
        parts.append(f"\n## {record['timestamp']} — {record['tool']} — {short_id}\n\n"
                     f"Source ID: `{record['id']}`\n\n### Prompt\n\n{fenced(record['prompt'])}")
        if record['attachments']:
            parts.append('\nImage attachment present in source transcript; image data is not exported.\n')
        parts.append('\n### Saved assistant responses\n\n')
        if not record['responses']:
            parts.append('No response imported yet.\n')
        for response in record['responses']:
            parts.append(fenced(response['text']) + '\n')
    return ''.join(parts)


def sync(root, codex_home, claude_home, cache):
    state = root / 'docs' / '.prompt-log'
    state.mkdir(parents=True, exist_ok=True)
    with (state / 'lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        store = state / 'records.json'
        records = {r['id']: r for r in json.loads(store.read_text())} if store.exists() else {}
        for tool, base, pattern in [('codex', codex_home / 'sessions', '**/*.jsonl'),
                                     ('codex', codex_home / 'archived_sessions', '**/*.jsonl'),
                                     ('claude', claude_home / 'projects', '*/*.jsonl')]:
            for path in base.glob(pattern):
                stat = path.stat()
                signature = (stat.st_mtime_ns, stat.st_size)
                if cache.get(str(path)) == signature:
                    continue
                imported = parse_transcript(path, tool, root)
                for record in imported:
                    records[record['id']] = record
                cache[str(path)] = signature
        ordered = sorted(records.values(), key=lambda r: (r['timestamp'], r['id']))
        serialized = json.dumps(ordered, ensure_ascii=False, indent=2) + '\n'
        if not store.exists() or store.read_text() != serialized:
            atomic_write(store, serialized)
        output = root / 'docs' / 'prompt-log.md'
        markdown = render(ordered)
        if not output.exists() or output.read_text() != markdown:
            atomic_write(output, markdown)
        return len(ordered)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('sync', 'watch'), nargs='?', default='sync')
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--codex-home', type=Path, default=Path(os.environ.get('CODEX_HOME', str(Path.home() / '.codex'))))
    parser.add_argument('--claude-home', type=Path, default=Path.home() / '.claude')
    parser.add_argument('--interval', type=float, default=5)
    args = parser.parse_args()
    if args.interval <= 0:
        parser.error('--interval must be positive')
    cache = {}
    if args.command == 'sync':
        print(f'Captured {sync(args.root, args.codex_home, args.claude_home, cache)} prompts.')
        return
    state = args.root / 'docs' / '.prompt-log'
    state.mkdir(parents=True, exist_ok=True)
    with (state / 'watcher.lock').open('a') as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            print('Prompt watcher is already running.')
            return
        print('Prompt watcher running; checking saved histories every 5 seconds.', flush=True)
        while True:
            try:
                sync(args.root, args.codex_home, args.claude_home, cache)
            except (OSError, ValueError) as error:
                print(f'Prompt capture needs attention: {error}', file=sys.stderr, flush=True)
            time.sleep(args.interval)


if __name__ == '__main__':
    main()
