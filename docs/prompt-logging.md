# Automatic prompt history

`prompt-log.md` holds every prompt typed into Claude Code (or Codex) in this
repository, with the assistant's saved replies. `scripts/prompt_log.py` copies
the text from the local transcripts; no model reproduces it. Injected context,
compaction summaries, background-task notifications, tool results and subagent
histories are left out. Images are marked but not embedded. The importer is the
one used in the space-jumper repo, with those extra exclusions.

Refresh by hand with `python3 scripts/prompt_log.py sync`, or keep
`python3 scripts/prompt_log.py watch` running in a terminal. Claude Code's
project hooks (`.claude/settings.json`) run the sync at session start, prompt
submission, completion and session end; a session started before the hooks
existed loads them on restart.

- `prompt-log.md`: generated; edits are undone on the next sync.
- `.prompt-log/records.json`: the captured records, kept in Git so history
  survives if the tools later prune old transcripts.
- `prompt-notes.md` (optional): outcome notes, never overwritten.

Sources: Claude transcripts under `~/.claude/projects` and Codex sessions under
`~/.codex`, for chats whose working directory is this checkout. Other worktrees
and cloud-only chats are not included. Review the log before publishing, since
it keeps exactly what you typed.
