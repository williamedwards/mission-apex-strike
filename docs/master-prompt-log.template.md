<!--
  Template for PROMPT_LOG.md. Edit this file, not PROMPT_LOG.md, then run:
      python3 scripts/build_master_log.py
  Each prompt marker (two braces, "prompt:", a phrase, two closing braces) becomes the verbatim prompt containing TEXT, from the captured
  log. Items in [square brackets] are for the author to fill in.
-->
# Master AI Prompt Log & Workflow Analysis

**Course:** AME 294: Games and AI — Creating Games with Artificial Intelligence  
**Assignment:** PG3: Unreal or Unity MCP Game  
**Student Name:** William Edwards  
**Project Title:** Mission: Apex Strike  
**Repository URL:** https://github.com/williamedwards/mission-apex-strike  
**Itch.io URL (Optional Bonus):** https://aembear.itch.io/mission-apex-strike

Mission: Apex Strike is a 3D puzzle obstacle course in Unity 6 (URP). Magnetic boots let the
player walk up curved ramps onto walls and ceilings, and dropping back to the floor is part of
every solution. This log selects the prompts behind the major decisions, the Tripo 3D assets,
the audio and the error recoveries; [section 8](#8-complete-prompt-list-chronological) lists
every prompt in order.

**Reading this log.** Prompts are quoted exactly as typed, typos included. "Reported" outcomes
are what the coding agent said it did; the agent verified most changes with automated tests
(62 EditMode and 282 PlayMode by the end) and scripted play-throughs, and the author
playtested each step.

---

## 1. Toolchain & AI Session Inventory

| Category | Primary Tool / Platform | Model Version / Specification | Purpose in Project |
| :--- | :--- | :--- | :--- |
| Code scaffolding agent | Claude Code (Claude desktop app, Code tab) | Claude Opus 5.5 (`claude-opus-5-5`), all sessions | Unity project setup, architecture, plan, tests |
| Logic & debugging agent | Claude Code | Claude Opus 5.5 | Player motor, boots, camera, hazards, movers, level building, audio wiring, UI, fixes |
| Engine automation | Unity CLI and Pipeline package (driven by Claude Code) | Unity 6000.5.9f1, URP | Running tests, driving the open Editor, captures, headless builds |
| Concept / reference images | Codex CLI, invoked by Claude Code | GPT-6 Astra, built-in `image_gen` tool (OpenAI GPT Image; exact image model not recorded) | Front and back turnarounds as Tripo inputs (17 images) |
| 3D models & animation | Tripo Studio (tripo3d.ai, Pro plan), used by the author | P2.0 | Image-to-3D models, auto-rigging, preset and text-to-animation clips |
| Sound effects | ElevenLabs Sound Effects API (Creator plan), called by Claude Code | `eleven_text_to_sound_v2` | 13 in-game effects, from 31 generations |
| Music | ElevenLabs (Creator plan), used by the author | Eleven Music | Two background tracks |
| Audio and mesh analysis (non-generative) | ffmpeg, NumPy, Blender (headless) | — | Loudness and spectrum checks, cutting footsteps, inspecting FBX files |

---

## 2. Code Development Prompts

### 2.1 Initial Setup & Boilerplate Scaffolding

#### The concept, and setting up the project

{{prompt:I want to create a game in Unity based around this concept}}

**Reported outcome:** Created a Unity 6 URP project with the Unity CLI, an assembly layout
(`GravBoots.Runtime`, editor and test assemblies), a first tested helper for aligning the
player to a surface, and a phased plan in `docs/PLAN.md`. A headless EditMode run passed.

#### Refining the plan: drops are part of the puzzle

{{prompt:Addressing 1, there is}}

**Reported outcome:** Revised the plan so deliberately switching the boots off to drop is a
core puzzle verb, with a landing marker to make drops readable.

#### First playable slice

{{prompt:Commit the setup and revised plan}}

**Reported outcome:** Input actions, a gravity body and kinematic player motor that work for
any up direction, an over-the-shoulder Cinemachine camera rig, debug gravity keys and a
generated test bed scene, with PlayMode tests at all six orientations.

### 2.2 Core Mechanic: Magnetic Boots and Curves

#### Orientation changes only along curves

{{prompt:conceived this was that the way you transition}}

**Reported outcome:** Replaced corner-transition logic with curved magnetic ramps
(`CurvedRamp`). A sharp join between surfaces became impassable by design, a rule kept for the
rest of the project.

#### Tuning the ramps, and jumping while magnetized

{{prompt:favor 2.5 m}}

**Reported outcome:** Standardised 2.5 m quarter-pipes after a side-by-side test of radii, and
blocked jumping while magnetized (the boots are too heavy).

#### The author building levels too

{{prompt:edit level layouts}}

**Reported outcome:** Built the Level Kit, an Editor window of snapping pieces (blocks, magnetic
panels, curves, hazards, movers, checkpoints), with a beginner's guide in
`docs/LEVEL_EDITING.md`, so the author could build and edit chambers by hand.

### 2.3 The Core Gameplay Triad (Reward — Damage — End)

Mission: Apex Strike has no score. Progress is the reward (checkpoints and reaching the exit),
damage is a single hit (a sentry's beam or a pit), and the end is the exit elevator and end
screen.

#### A. Reward: checkpoints and the game loop

{{prompt:commit and try building those things}}

**Reported outcome:** Deaths, respawn at the last checkpoint, an exit, a run timer and death
count, and a playtest log written on every run.

#### B. Damage: designing the sentry

{{prompt:brainstorm some more interesting hazards}}

{{prompt:Let's prototype the sentry}}

{{prompt:lock on and fire rather}}

**Reported outcome:** A sentry that sees and shoots only in a flat plane 1 m above its surface:
it locks on and fires, a hit kills, and dropping on its head destroys it for good. Its hit test
is geometric, not a physics query, so it is deterministic in tests.

{{prompt:lock and kill loop}}

**Reported outcome:** Faster lock-on, so the design rule holds: the player should never be in a
sentry's plane, and speed is no escape (see Incident 2 for the follow-up).

#### B. Damage: moving platforms and timing

{{prompt:let's do moving platforms}}

**Reported outcome:** `KinematicMover` platforms (ping-pong, loop, step-on) driven by a level
clock that rewinds on respawn, so timings repeat exactly; the player rides anything that moves
the collider under them, in any orientation. Sentries can ride tracks.

#### C. End: elevators, title and end screens

{{prompt:I'd like to model a tube elevator}}

**Reported outcome:** Start and exit elevators: a step-on platform that rises from a shaft at
the start, and carries the player up and out at the end while the camera watches.

{{prompt:The Apex Inversion}}

**Reported outcome:** Title and end screens over slow camera moves through the chamber's rooms.
The game was renamed mid-task (see the note at the top of section 8).

### 2.4 Level Design

{{prompt:larger and more incremental level}}

**Reported outcome:** Chamber_04, eleven rooms that introduce one idea each, then combine them,
built with a one-off script and then hand-edited through the Editor.

{{prompt:expand the level with a few more puzzles}}

**Reported outcome:** Three new rooms (Corkscrew, Diving Board, and the Gauntlet challenge),
each proved solvable by a frame-stepped scripted run of the whole chamber with no deaths.

### 2.5 Player Feedback and Polish

{{prompt:legible control hints}}

{{prompt:replace the orange kill floors with a shadowed void}}

{{prompt:turn off debug mode by default}}

**Reported outcome:** Context-sensitive key hints, a depth-based darkness shader over lethal
pits, and debug tools hidden (overlay) or Editor-only (cheat keys).

### 2.6 Wiring Up the Audio

{{prompt:Let's try another pass on the breathing loop}}

**Reported outcome:** A `SoundBank` asset holding every clip and volume, and audio components
added at runtime to the player, sentries, elevators and platforms, so no scene needed editing.
Footsteps are triggered from the animation's foot bones.

{{prompt:I now have two background music tracks}}

**Reported outcome:** A music player that crossfades between the two tracks and keeps playing
across chamber loads.

### 2.7 Web Build

{{prompt:prepare a build for web export}}

**Reported outcome:** A repeatable WebGL build for itch.io (menu GravBoots → Build for Web).
The first attempt crashed; see Incident 10.

---

## 3. Tripo 3D Asset Generation

Every model in the game came from the same pipeline:

1. **Concept** — the author described the asset; Claude Code drafted a Tripo-friendly version
   of the prompt (single view, plain background, even lighting, a clear silhouette, parts that
   read as separate) and ran it past the author.
2. **Reference images** — Claude Code sent the prompt to the Codex CLI, which generated a front
   view, then a back view using the front as its reference. Images are in each asset's
   `Reference/` folder. Codex prompts that attach a reference image start with the line
   "Codex could not read the local image at `-`": a harmless warning from how the command was
   invoked. The image was attached, and the quotes below are what was actually sent.
3. **Tripo** — the author generated the model in Tripo Studio from the reference images, then
   rigged and animated the character. Exports are in each asset's `Model/` folder.
4. **Integration** — Claude Code inspected each FBX (Blender, headless) and fitted it in Unity:
   scale, pivots, splitting parts, materials, and builders that apply the model across levels.

**Tripo credits:** about 3,250 before and 2,500 after, so roughly **750 credits** for every generation
below, including drafts and retries (Pro plan; not tracked per asset).

| Asset | Reference images | Tripo route | In the project |
| :--- | :--- | :--- | :--- |
| Player character | `player_ref_02_front.png`, `player_ref_02_back.png` | Image-to-3D from front and back views → auto-rig → animation | `Art/Characters/Player/Model/tripo_convert_b0228436…fbx` |
| Player animations | — | Preset clips (Idle, Walk, Run, Jump) and text-to-animation (4 clips) | `Art/Characters/Player/Animations/*/` |
| Sentry | `sentry_ref_02_front.png`, `sentry_ref_02_back.png` | Image-to-3D from front and back views, auto-segmented (40 parts) | `Art/Hazards/Sentry/Model/spherical+robot+3d+model/` |
| Tube elevator | `elevator_ref_04_front.png`, `elevator_ref_04_back.png` | Image-to-3D from front and back views | `Art/Level/Elevator/Model/futuristic portal 3d model/` |
| Checkpoint pad | `checkpoint_ref_01.png` | Image-to-3D from one view | `Art/Level/Checkpoint/Model/futuristic platform 3d model/` |
| Dark platform deck | `platform_dark_ref_02.png` | Image-to-3D from one view | `Art/Level/Platform/Model_Dark_Platform/` |
| Light (magnetic) platform deck | `platform_magnetic_ref_02.png` | Image-to-3D from one view | `Art/Level/Platform/Model_Light_Platform/` |

(Paths are under `Assets/_Project/`. Tripo's export names were kept; the folders say what each is.)

### 3.1 Player Character

#### Planning for a smooth Tripo integration

{{prompt:replacing the robot model with a animated mesh}}

**Reported outcome:** Advice on an A-pose, one character per image, a plain background, a
humanoid rig Unity can retarget, and in-place animations.

#### First reference image (the author's prompt)

{{prompt:Athletic man in blue jumpsuit with sci-fi exoskeleton, glowing blue circle on chest and back plate.  Front and back views.}}

**Result:** `player_ref_01.png`, a sheet with front and back views and close-up panels. Good
design, but extra panels and a busy layout confuse image-to-3D.

#### Revising the prompt for Tripo

{{prompt:cleaner for a tripo run}}

{{prompt:multi-views}}

{{prompt:Full-body character reference of an athletic man}}

{{prompt:Back view of the same character as the reference image}}

**Result:** `player_ref_02_front.png` and `player_ref_02_back.png`, separate A-pose views, the
Tripo inputs.

{{prompt:photorealism in the face}}

#### Bringing the model and animations in

{{prompt:exporting animations}}

{{prompt:pasted the FBX with four}}

**Reported outcome:** Tripo exports one FBX with all four preset clips (Idle, Walk, Run, Jump).
Claude Code set it up as a humanoid rig, built an Animator controller, and replaced the
placeholder robot.

{{prompt:He feels a bit small}}

**Reported outcome:** Scaled the character up, added blended transitions, and stopped the walk
cycle playing while riding a platform without input (it had been reading platform motion as
walking).

#### Text-to-animation clips

Four clips came from Tripo's text-to-animation: three tries at a heavier magnetized walk,
chosen between by playtesting in game, and the death fall. The full animation prompts weren't
kept; Tripo stores the first 63 characters of each in the exported FBX, recovered here.

| Clip | Prompt (start, as stored in the FBX) | Used? |
| :--- | :--- | :--- |
| DeliberateWalk | "Heavy, deliberate walk with careful foot placement and boots th…" | No: too slow at 1.5 m/s |
| PurposefulWalk | "Steady, purposeful walk at a normal to brisk walking pace, but …" | No: legs over-reached |
| PurposefulWalk2 | "Steady, purposeful walk at a normal to brisk walking pace." | Yes: magnetized walk at 3 m/s |
| FallingProne | "Falling prone after a sudden injury." | Yes: every death |

{{prompt:new clip I added to Animations/Deliberate_Walk}}

{{prompt:faster walking clip}}

{{prompt:Purpoesful_Walk2}}

{{prompt:settle on Purposeful_Walk2 with a speed of 3 m/s}}

{{prompt:Falling_Prone to be used on Player death}}

### 3.2 Sentry

{{prompt:models for the sentries}}

**Reported outcome:** Tripo can't rig a turret, so: model the sentry so the domed head reads as
a separate part, and Claude Code splits it into a body and a head that turns in code.

{{prompt:Evil squat robot with four legs, a domed  head}}

{{prompt:Evil squat four-legged robot sentry, front view only}}

{{prompt:Back view of the same robot sentry}}

{{prompt:autosegmented into several pieces}}

**Reported outcome:** Tripo's auto-segmentation produced 40 parts. Claude Code grouped them
into `SentryBody` and `SentryHead` prefabs, with the eye at 1 m to match the firing plane, and
the destroyed state crumbles the body into rubble.

{{prompt:Sentry/Model2}}

**Reported outcome:** `Model2` turned out to be a byte-identical copy of the first export, so
it was left out (see Incident 4).

### 3.3 Tube Elevator

{{prompt:Round elevator tube with glass sides, sci-fi machinery around base and top with glowing blue strips.  Front and back view}}

{{prompt:Sci-fi elevator tube, front view only. A tall round open frame}}

{{prompt:Back view of the same sci-fi elevator tube as the reference image: the identical round}}

{{prompt:more substantial mechanical collar}}

{{prompt:A substantial, heavy mechanical collar around the base}}

{{prompt:Can we try adding steps or a ramp}}

{{prompt:short flight of three wide, shallow metal steps}}

{{prompt:seen from directly behind it}}

{{prompt:empty gap in the middle of the floor}}

**Reported outcome:** The gap suits the design: the moving platform fills it. The elevator uses
the Tripo tube with Unity glass and a round step-on platform, a 4.5 m tall model on a 6 m ride.

### 3.4 Checkpoint Pad and Moving Platforms

{{prompt:checkpoints for the moving platforms and checkpoints}}

{{prompt:Sci-fi checkpoint pad for a game}}

{{prompt:Sci-fi magnetic moving platform for a game, three-quarter view from slightly above. A flat square deck}}

{{prompt:Sci-fi moving platform for a game, three-quarter view from slightly above. A flat square deck}}

{{prompt:underside thrusters}}

{{prompt:A thin, flat square slab, about one tenth as thick as it is wide, with a perfectly flat, walkable top surface of dark grey}}

{{prompt:A thin, flat square slab, about one tenth as thick as it is wide, with a perfectly flat, walkable top surface of light}}

{{prompt:Platform models are available now}}

**Reported outcome:** The checkpoint pad glows blue when idle and green when reached (generated
glow textures). The platform models skin the existing collider decks, dark for plain decks and
light for magnetic ones, stretched to each deck's size.

### 3.5 Textures Drawn in Code Instead of Tripo

{{prompt:retexture the quarter-pipe segments}}

**Reported outcome:** Code-drawn textures were simpler than a Tripo pass for flat curved
panels: mid-grey with glowing blue chevrons, so curves read as "the boots go this way."

---

## 4. Generative Audio & Sound Design Prompts

Claude Code generated every sound effect through the ElevenLabs API
(`eleven_text_to_sound_v2`, prompt influence 0.5), read the author's API key from a local file
without printing it, and played options on a listening page. Every round was playtested in the
game before the author chose. Credits came from the per-request cost header (the key couldn't
read the account balance): **723 credits in total** over 31 generations.

{{prompt:I'd like to generate sound effects to go with the game}}

### 4.1 Effects in the Game

| Event | File | Exact prompt | Length | Credits |
| :--- | :--- | :--- | :--- | :--- |
| Footsteps (cut into 6 steps) | `footstep_1.wav` … `footstep_6.wav` | Four footsteps, evenly spaced about half a second apart, dry close recording. Tactical boots with hard composite soles on a smooth spaceship floor panel, crisp heel and toe | 2.5 s | 28 |
| Boots on | `boots_on.mp3` | Magnetic boots locking on: quick rising electric whine and a sharp magnetic snap, sci-fi, punchy | 1.0 s | 11 |
| Boots off | `boots_off.mp3` | Magnetic boots releasing: a short electrical power-down sweep and a soft metallic unlatch, sci-fi | 1.0 s | 11 |
| Boots hum (loop) | `boots_hum_loop.mp3` | Gentle sci-fi force-field hum: warm midrange drone with faint crackling static and a slow pulse, seamless loop | 3.0 s | 33 |
| Breathing (loop) | `breathing_loop.mp3` | Barely audible, slow, relaxed breathing through the nose inside a sealed helmet, soft and muffled, no gasps, near silence between breaths, seamless loop | 5.0 s | 55 |
| Death | `death_groan.mp3` | A man's short pained groan as he collapses, muffled inside a helmet, not screaming | 1.5 s | ≈17 |
| Sentry lock-on | `sentry_alert.mp3` | Menacing robot sentry waking and locking on: a deep low mechanical growl rising slightly, ending in a heavy low targeting thrum, sci-fi | 1.5 s | 17 |
| Sentry fires | `sentry_fire.mp3` | Sci-fi laser weapon firing a single powerful red beam: sharp electrical zap with a short crackling tail | 1.0 s | ≈11 |
| Sentry destroyed | `sentry_destroyed.mp3` | Small sci-fi robot sentry crushed and destroyed: metal crunch, electrical sparks, a short power-down whine and debris clatter | 2.0 s | ≈22 |
| Elevator moving (loop) | `elevator_loop.mp3` | Subtle mechanical elevator platform moving: low motor hum with faint rhythmic servo whir, sci-fi, seamless loop | 4.0 s | ≈44 |
| Elevator stops | `elevator_stop.mp3` | Sci-fi elevator platform arriving: soft hydraulic hiss and a gentle heavy metallic clunk as it locks in place | 1.5 s | ≈17 |
| Platform gliding (loop) | `platform_loop.mp3` | Sci-fi hover platform gliding: soft magnetic levitation whoosh with a gentle electric hum and faint air hiss, seamless loop | 3.0 s | 33 |
| Platform stops | `platform_stop.mp3` | Heavy sci-fi metal platform stopping at the end of its rail: a deep solid clunk with a short hydraulic hiss | 1.0 s | 11 |

Files are in `Assets/_Project/Audio/SFX/Draft/`; volumes are in
`Assets/_Project/Resources/SoundBank.asset`. Credits marked ≈ are from the first batch of 11
drafts, which cost 249 in total and was reported per batch, not per clip.

### 4.2 Iterations

**Round 1 — first drafts** (11 clips, 249 credits, then a subtler breathing loop for 55). Replaced later: footsteps ("Four single
footsteps of a heavy armoured boot on a metal floor, evenly spaced, dry close recording,
sci-fi"), boots on ("Magnetic boots clamping on: a short electromagnetic charge-up whine ending
in a firm metallic clunk, sci-fi"), boots hum ("Very subtle low electromagnetic hum with faint
electrical crackle, steady, sci-fi magnetic field, seamless loop"), sentry lock-on ("Sinister
robot sentry activating and locking on: a menacing descending synth tone and a sharp targeting
beep, sci-fi"), and breathing v1 ("Calm, quiet, steady breathing of a man inside a sci-fi
helmet, very subtle, slightly filtered, seamless loop").

{{prompt:Footsteps and boot hum are barely audible}}

**Finding:** two of the four footsteps cut from the first take were silent (a bad split), and
the hum's energy was entirely below 60 Hz, which most speakers can't reproduce. See Incident 5.

{{prompt:generate several new options}}

**Round 2 — options** (10 clips, 230 credits). Footsteps all began "Four footsteps, evenly
spaced about half a second apart, dry close recording." followed by: A "Heavy metal-soled boots
on a steel deck plate: crisp metallic clank on top of a solid thud"; B "Armoured boots on a
metal grate catwalk: bright metallic ring and light rattle"; C "Sci-fi magnetic boots sticking
to a steel hull: each step a firm clunk with a sharp magnetic click"; **D "Tactical boots with
hard composite soles on a smooth spaceship floor panel, crisp heel and toe" (chosen)**. Boots on:
**A (chosen)**; B "Sci-fi power-up: a short bright electrical charge chirp then a heavy magnetic
clamp thunk"; C "Electromagnet engaging: a crisp relay click, a brief electrical buzz, and a
resonant metallic thump"; D "Futuristic boot clamps activating: two quick mechanical latch
clicks with a soft energy whoosh". Hum: A "Steady electromagnetic coil hum in the midrange, like
a transformer buzz with a soft high shimmer, sci-fi, seamless loop"; **B (chosen)**.

{{prompt:Let's try with footstep D, boot activation A, and boot hum B}}

{{prompt:mechanical/sliding sounds for the platforms}}

**Round 3 — platforms and sentries** (9 clips, 189 credits). Platform loop: A "Heavy metal
platform sliding along a steel rail: smooth grinding glide with a low servo motor whir, sci-fi,
seamless loop"; **B (chosen)**; C "Industrial moving platform on a track: rhythmic mechanical
clatter of rollers and a low motor drone, seamless loop". Platform stop: **A (chosen)**; B
"Moving metal platform arriving and locking: a servo wind-down and a firm mechanical latch
click". Sentry lock-on: **A (chosen)**; B "Sinister low synth drone swelling up as a hostile
robot detects its target, deep bass pulse and a dark metallic resonance, sci-fi"; C "Hostile
robot turret powering up: low heavy servo whir turning toward its target and a deep ominous
lock-on tone"; D "Short ominous low alarm blare from a robot sentry spotting an intruder, deep
distorted horn with a metallic edge, sci-fi".

{{prompt:Let's try sentry activation A, platform stop A, and platform movement B}}

### 4.3 Background Music

Two tracks generated by the author with Eleven Music (Creator plan), from this prompt, and
crossfaded in order in the game:

```text
Dark ambient background music with unsettling, sparse texture. Starts soft and gets slightly louder but remains subdued, suitable for use in a tense puzzle solving game.
```

| Track | File | Length |
| :--- | :--- | :--- |
| Hidden Chamber | `Audio/Background/Draft/Hidden_Chamber_2026-10-28T103908.mp3` | 3:00 |
| Silent Chamber | `Audio/Background/Draft/Silent_Chamber_2026-10-28T103908.mp3` | 2:54 |

---

## 5. Debugging, Error Recovery & Friction Log

### Incident 1: Flickering corridor floors and a platform pushing the player through the floor

* **Symptom:** Floors flickered in the corridors between rooms; standing under a descending
  platform pushed the player down through the floor into the void.
* **Root cause (reported):** The corridor floors ran through the doorways, so their tops shared
  a plane with the wall sections under the doors (z-fighting). The motor pushed an overlapped
  player out the shortest way, which was down.
* **Prompt:**

{{prompt:some sort of flickering or z-fighting effect going on}}

* **Resolution (reported):** Trimmed all 11 corridor floors to stop at the walls. When the
  shortest way out points into the ground, the motor now steps the player sideways out from
  under the platform; a new test covers it at all six orientations.

### Incident 2: Sentries could be beaten by speed or jumping

* **Symptom:** Players could dash through a sentry's plane, or jump in and out, before it fired.
* **Root cause (reported):** The sentry only reacted once the player was in its plane; a jump
  reset its charge.
* **Prompt:**

{{prompt:sentries can still be defeated}}

* **Resolution (reported):** An alert zone 1.2 m either side of the plane where it charges in
  advance, firing the instant any part of the player enters the plane, a 0.6 s grace period
  that keeps the charge, and line of sight checked to the body.

### Incident 3: The death animation sometimes didn't play

* **Prompt:**

{{prompt:prone animation fails}}

* **Root cause (reported):** The fall only played for some kinds of death; others kept the pose
  the player died in.
* **Resolution (reported):** The fall now plays on every death, after the author chose to
  include pit deaths too:

{{prompt:add it for pit deaths as well}}

### Incident 4: A Tripo export that wasn't what it seemed

* **Symptom:** The author added `Sentry/Model2`, believing it was a two-part segmentation.
* **Root cause:** The file was byte-identical to the first 40-part export; Tripo reuses the same
  file name, so the original was likely copied twice.
* **Resolution:** Left out of the project and noted for a future pass.

### Incident 5: Footsteps and boot hum barely audible

* **Prompt:** see section 4.2 ("Footsteps and boot hum are barely audible").
* **Root cause (reported):** Two of the four footsteps cut from the first take were pure
  silence (the split missed the steps), and the hum was all sub-bass below 60 Hz. Turning the
  hum up could never fix that.
* **Resolution (reported):** Recut and level-matched the steps, then generated new options,
  writing prompts that ask for midrange detail.

### Incident 6: Footsteps out of time with the feet

* **Prompt:**

{{prompt:footstep timing feels slightly off}}

* **Root cause (reported):** Sampling the walk and run clips frame by frame showed a step fired
  as the ankle passed 20 cm on the way down, 0.05–0.1 s before the heel landed.
* **Resolution (reported):** `FootfallDetector` fires when the foot stops descending, which is the
  heel settling, with EditMode tests built from heights sampled out of the clips.

### Incident 7: Z-fighting in the start shaft and jitter on platforms

* **Prompt:**

{{prompt:z-fighting when the elevator rises}}

* **Root cause (reported):** The floor's opening was exactly as wide as the shaft, so cut faces
  shared planes with the shaft walls. The player was drawn interpolated between physics steps,
  but platforms were drawn at their latest step, so a rider jiggled.
* **Resolution (reported):** Shaft walls inset 5 mm (in the builder and the scene); platforms are
  now drawn between steps the same way as the player. Measured: the player-to-deck gap that had
  varied between 2.0 and 3.5 cm while moving now holds at 2.0 cm.

### Incident 8: The first "magnetize" hint never appeared

* **Prompt:**

{{prompt:not seeing the first magnetize hint}}

* **Root cause (reported):** The hint looked only for curves, but the room that first asks for
  the boots has a flat magnetic patch. The playtest record showed the author crossing it without
  pausing.
* **Resolution (reported):** The first time the player stands on any magnetic surface with the
  boots off, the hint shows at once.

### Incident 9: The same z-fighting, regenerated

* **Prompt:**

{{prompt:z-fighting around the checkpoints before and after}}

* **Root cause (reported):** The script that built the new rooms made the corridor floors 4 m
  long, repeating Incident 1's mistake.
* **Resolution (reported):** Shortened to 3 m, and a scan for overlapping coplanar faces around
  all 21 checkpoints found nothing else visible.

### Incident 10: The web build crashed the Editor

* **Prompt:**

{{prompt:crashed part way through the web export}}

* **Root cause:** The system log showed Linux killed Unity for running out of memory during the
  WebGL compile (14 GB machine). The disk was also 99% full: a Game view set larger than the
  screen had filled the Editor log with 6 GB of repeated warnings.
* **Resolution:** Cleared up the crash's leftovers, deleted the logs (with permission), and built
  headless with the Editor closed, with code optimised for size without link-time optimisation.
  It succeeded in 10 minutes.

{{prompt:Delete them}}

---

## 6. Human-in-the-Loop Curation & Analytical Reflection

The game "Mission: Apex Strike" is a Unity game developed by human-AI collaboration using Claude Opus for planning and software development, ChatGPT Codex for reference image generation, Tripo AI for 3D asset generation, and ElevenLabs for music and sound effect generation.  As with the previous assignment, this game was developed via a highly iterative and conversational process.  My prompts guided the creation of the game mechanics, refined timing, animation, and mechanics for play feel, shaped level design at a high level, established an aesthetic starting point for reference art creation, and guided art refinement after initial drafts.  My playtesting surfaced issues that the AI couldn’t spot, including unnatural animations, difficult jump timing, and inaudible or inappropriate sounds, while the AI did the heavy lifting in code generation and level layout, with numerous automated tests created to ensure the mechanics were working smoothly and all the puzzles remained solvable.  I found the tests to be comprehensive and helpful, and was pleasantly surprised to find the level design to be quite usable.  One idea I explored, but did not end up incorporating into the final game, was using an AI-created level kit to manually place elements within the Unity Editor.  The level kit worked well, and I was able to build some very simple test environments using it, but I did not have time to build complex puzzle layouts suitable for in-game use.  Overall, the process of using Claude Code with Unity was mostly seamless, with Claude effectively driving the editor via CLI, running its own tests, and presenting scenes for feedback.  Although I suspect this project only scratched the surface of what a frontier engine like Unity can do, I feel empowered to explore its capabilities more deeply using agentic AI assistance in the future.

---

## 7. Asset Attribution & Licensing

**Terms in brief** (check each against your plan before publishing):

* **Tripo (Pro plan):** Every model and animation here was generated on Tripo's paid Pro plan,
  whose outputs are private and come with commercial rights. Note for comparison: models
  generated on Tripo's **free plan are public**, published under CC BY 4.0 so others may reuse
  them with attribution, and Tripo's help centre says free-plan users aren't granted
  commercial use. Sources: [Tripo: how to use Tripo models commercially](https://www.tripo3d.ai/help/privacy-policy/how-to-use-tripo-models-commercially)
  and Tripo's Terms of Service (section 5.2).
* **ElevenLabs sound effects (Creator plan):** Paid plans may be used in commercial projects
  without attribution; the free plan requires crediting elevenlabs.io.
  Source: [ElevenLabs sound effects](https://elevenlabs.io/sound-effects/music).
* **Eleven Music (Creator plan):** Self-serve plans, Creator included, restrict use in commercial video games (prior approval or
  Enterprise needed). This is a non-commercial class project. Source:
  [Eleven Music terms](https://elevenlabs.io/eleven-music-v1-terms).
* **OpenAI image generation (via Codex):** As between the user and OpenAI, the user owns the
  output, subject to OpenAI's terms. The images are used only as Tripo inputs and are kept as
  references. Source: [OpenAI terms of use](https://openai.com/policies/terms-of-use).
* **Claude Code:** Code and documentation written with Claude Code; outputs belong to the user
  under Anthropic's terms. Source: [Anthropic consumer terms](https://www.anthropic.com/legal/consumer-terms).
* **Unity and packages:** Unity 6 (Unity Terms of Service); URP, Cinemachine, Input System,
  ProBuilder and the Test Framework under the Unity Companion License.

| Asset | Type | AI model / source tool | License / terms | Prompt / origin |
| :--- | :--- | :--- | :--- | :--- |
| `Art/Characters/Player/Model/tripo_convert_b0228436…fbx` (+ texture) | Rigged character, 4 preset clips | Tripo Studio | Tripo terms, Pro plan (private, commercial rights) | §3.1; references `player_ref_02_*` |
| `Art/Characters/Player/Animations/{Deliberate_Walk, Purposeful_Walk, Purposeful_Walk2, Falling_Prone}/` | Animation clips | Tripo Studio (text-to-animation) | Tripo terms, Pro plan (private, commercial rights) | §3.1 clip table |
| `Art/Hazards/Sentry/Model/spherical+robot+3d+model/` | Segmented model, 40 parts | Tripo Studio | Tripo terms, Pro plan (private, commercial rights) | §3.2; references `sentry_ref_02_*` |
| `Art/Level/Elevator/Model/futuristic portal 3d model/` | Model | Tripo Studio | Tripo terms, Pro plan (private, commercial rights) | §3.3; references `elevator_ref_04_*` |
| `Art/Level/Checkpoint/Model/futuristic platform 3d model/` | Model | Tripo Studio | Tripo terms, Pro plan (private, commercial rights) | §3.4; reference `checkpoint_ref_01` |
| `Art/Level/Platform/Model_Dark_Platform/`, `Model_Light_Platform/` | Models | Tripo Studio | Tripo terms, Pro plan (private, commercial rights) | §3.4; references `platform_*_ref_02` |
| `Art/**/Reference/*.png` (17 images) | Concept / reference images | Codex CLI, built-in `image_gen` (OpenAI) | User-owned output, OpenAI terms | §3, exact prompts quoted |
| `Art/Level/Curve/*.png`, `Art/Level/Checkpoint/Generated/*`, `Art/GreyboxGrid.png`, `Art/MagneticPanel.png`, boot effect textures | Textures | Drawn by code written with Claude Code | Original | §3.5 |
| `Shaders/VoidFog.shader` | Shader | Written with Claude Code | Original | §2.5 |
| `Audio/SFX/Draft/*` (13 effects in use) | Sound effects | ElevenLabs `eleven_text_to_sound_v2` | ElevenLabs terms, Creator plan (commercial use, no attribution) | §4.1–4.2 |
| `Audio/Background/Draft/*.mp3` | Music | Eleven Music | Eleven Music terms, Creator plan (non-commercial project) | §4.3 |
| `Scripts/**`, `Tests/**`, `Editor/**` | Code | Claude Code (Claude Opus 5.5) | User-owned output | §2, §8 |
| UI text | Font | Unity's built-in default font | Unity terms | — |

(Paths are under `Assets/_Project/` unless shown otherwise.)
