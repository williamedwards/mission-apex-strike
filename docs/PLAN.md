# GravBoots: Implementation Plan

A 3D, over-the-shoulder **puzzle and maze obstacle course** built around **magnetic boots**.
With the boots on, the player walks up steep ramps, along walls and across ceilings. With them
off, world gravity takes over at once and the player falls to the floor and lands upright. The
hazards are death pits, laser-firing enemies and moving machinery that needs precise timing.
The look is clean sci-fi test-chamber, in the style of Portal. Portals for teleporting are a
stretch goal.

Engine: Unity 6000.5.9f1, URP, Input System, Cinemachine 3, ProBuilder (greybox), Pipeline
(lets the CLI drive the editor).

---

## 0. Design pillars

1. **Route-finding is the puzzle.** Each chamber is a 3D maze of connected magnetic surfaces.
   The question is always *which surfaces link up, and where do I let go?* Many routes can only
   be reached by walking somewhere you can't see from the floor.
2. **Dropping is a deliberate, strategic move.** Switching the boots off isn't a bail-out. It
   is how you reach floors, platforms and switches that no magnetic path connects to. A drop is
   a **commitment**: in v1 the boots can only switch on while you're standing on a magnetic
   surface, not in mid-air (see §1.3). That keeps drop puzzles honest.
3. **Readable before reflexive.** Before committing, the player must be able to read which
   surfaces are magnetic, where a drop will land and how a hazard is timed. Death should feel
   like a wrong guess or a timing slip, never like information the game hid.
4. **Hazards add pressure to the route.** Pits, turrets and moving machinery decide *when* and
   *whether* a route can be used. They don't replace the route-finding.

The boots have no extra cost (energy, speed) by default. Dropping is already the trade-off. If
playtests show players never think about when to let go, small costs are available to tune
(slower movement when magnetised, no jumping).

---

## 1. Core technical decisions

### 1.1 Character controller: custom kinematic, built for any up direction from day one
Unity's `CharacterController` capsule is locked to world-up. A dynamic Rigidbody fights us on
edges and moving platforms. Instead:

- **Kinematic Rigidbody + capsule, with our own collide-and-slide** (`Physics.CapsuleCast`,
  3–5 iterations).
- **The motor never uses `Vector3.up`.** All movement (input plane, gravity, jumping, slope
  limit, step-up, the ground probe) is written relative to a `GravityBody.Up` vector, starting
  with the very first line of code. Normal gravity is just the case `Up == Vector3.up`. Code
  review rule: a `Vector3.up` in `Player/` is a bug, unless it's the line that defines world
  gravity.
- Ground detection: a `SphereCast` along `-Up` to detect *contact*, plus a `Raycast` to get
  the *face normal*. Sphere casts return blended normals at edges, which shows up as jitter at
  corners. Smooth the normal over a few frames before using it for alignment.
- Rotations pivot around the **capsule centre**. After rotating, `Physics.ComputePenetration`
  pushes the capsule back out of geometry (the stress case is righting yourself under a low
  ceiling).
- Fixed-timestep simulation in `FixedUpdate`. The visual model interpolates in `Update`.
- Fallback if this turns out to be too much work: the open-source **Kinematic Character
  Controller** (KCC) asset, which supports arbitrary up vectors. Decide at the end of Phase 1A.

### 1.2 What counts as "magnetic"
Boots stick only to surfaces on the **`Magnetic` physics layer**, which share one unmistakable
material (bright white panels with a faint grid). Non-magnetic surfaces are dark metal. This is
the main level-design lever, so the visual difference must hold up under every lighting setup.
A `SurfaceInfo` component can override friction, how fast the player re-aligns, and similar
values for each surface.

### 1.3 Boots states
```
                 toggle on (grounded on magnetic)                toggle off
  [Normal] ────────────────────────────────────▶ [Magnetized] ─────────────▶ [Dropping]
     ▲                                             │    ▲                        │
     │     on the floor, toggle off (no fall)      │    │ follows curves onto    │
     └─────────────────────────────────────────────┘    │ walls and ceilings     │
     └──────────────────────────────── lands upright ───┴────────────────────────┘
```
- **Normal**: `Up = Vector3.up`, standard platformer movement, can jump.
- **Magnetized**: `Up` follows the normal of the magnetic face directly under the player's
  centre.
  - **Orientation only changes along explicit curves**: quarter-pipes (`CurvedRamp`, concave)
    for inside corners and rounded edges (convex) for outside corners, plus pipes. Their facets
    meet at a few degrees, well under `followMaxAngle` (30°), so `Up` follows them smoothly.
  - **Sharp joins are impassable, even between magnetic surfaces.** Walking into a wall that
    meets the floor at 90° just blocks you. Walking off a sharp edge loses contact and drops
    you. This keeps every route visible in the level geometry: the curves *are* the routes.
  - The face is sampled by a ray along −Up from the player's centre, not taken from the
    motor's contact normal. Rolling over a sharp edge, the contact normal blends smoothly from
    one face to the other and would let the boots creep round a right angle.
  - Walking off a magnetic surface onto a non-magnetic one, or into empty space: no
    attachment, so it behaves like a drop. This is intentional, and the landing marker shows
    where you'd land.
  - Jumping while magnetized: **blocked** (the boots are too heavy). Decided in playtesting
    instead of a hop. `BootsSettings.jumpWhileMagnetized` can be set to `LetsGo`, which makes
    jump drop you like switching the boots off, if puzzles want that later.
  - Losing magnetic contact (a non-magnetic section, or empty space with no face beyond the
    edge) switches the boots **off** after a short grace period (`contactGrace`), so you have
    to switch them on again once you land on a magnetic surface.
- **Dropping** (boots switched off on a wall or ceiling): gravity becomes world-down on the
  very first frame. The body rights itself with `SurfaceAlignment.RotateUpToward` at about
  720°/s, so it is upright before landing. A small push away from the surface stops the player
  scraping down the wall.
  - The righting rotation is the **smallest rotation of the up axis**. Off a ceiling, where
    that rotation is ambiguous, it is a **roll about the facing direction**. Either way the
    player keeps their heading, and the camera stays behind them. This is covered by EditMode
    tests.
- **Re-attaching in mid-air**: not in v1, because of pillar 2. Later this could become an
  upgrade or a special surface type ("magnet anchor" panels), if puzzles want it.
- **Landing marker**: while magnetized, a reticle or shadow is projected straight down (world
  gravity) from the player onto whatever they would land on. It is coloured red if that is a
  kill volume. This is core to the puzzles, not polish, so it ships in Phase 1B.
- Boots input: **toggle** by default, with **hold** as an option. Playtest both.

### 1.4 Camera: a first-class system, tested from the first week
The camera is the riskiest system, so it gets its own test harness (Phase 1A) before the boots
exist.

- Cinemachine 3 `CinemachineCamera` with **ThirdPersonFollow** for the over-the-shoulder
  offset, plus `CinemachineDeoccluder` to stop it clipping into walls.
- A `CameraUpTracker` transform drives the camera's world-up override. It follows
  `GravityBody.Up` **smoothly**, and the speed is configurable. Look input orbits around that
  up direction, not world up.
- **Movement input mapping during transitions**: when the player's up starts to change, keep
  the mapping that was in place when the transition started, as long as the move stick stays
  held in roughly the same direction. Re-map once the stick is released or moves more than
  about 45°. Holding *W* into a wall then means "keep going up the wall".
- **Camera modes to A/B test** (switchable at runtime, settings in a ScriptableObject):
  1. **Full follow**: the camera's up equals the player's up, smoothed. On the ceiling the
     world is upside down.
  2. **Partial follow**: the camera rolls only part of the way (for example up to 90°) and
     makes up the rest with pitch and distance.
  3. **Recenter on settle**: like full follow, but it only commits after the player has stayed
     on a surface for about 0.3 s, so short corner-hopping doesn't spin the view.
- Known trouble spots, each with a dedicated room in the camera test bed:
  - Just after a transition, the camera's ideal spot is inside the surface you just left. The
    camera needs a short **pull-in and swing-around** instead of a hard push-out by the
    deoccluder.
  - Narrow corridors and walking on both walls of a corridor.
  - Looking "down" from a ceiling at a pit, which the landing marker has to make readable.
- Comfort settings: smoothing speed, roll limit, FOV, and a camera-shake toggle.

### 1.5 Level metrics (set in Phase 1A, before any real level geometry)
A `docs/LEVEL_METRICS.md` spec and a matching ProBuilder "metrics gym" scene:
player capsule height and radius, camera distance and shoulder offset, minimum corridor width
and height (big enough for walking on walls and ceilings with the camera behind you), standard
ramp angles, maximum step height, jump height and distance, and the **standard drop heights**
(safe, risky, lethal, if fall damage exists). Rooms are built on a 1 m grid in multiples of
these numbers.

### 1.6 Simulation order and determinism
- Update order: `LevelClock` → movers and hazards → `PlayerMotor` → `CameraUpTracker` →
  Cinemachine. Set it with `[DefaultExecutionOrder]`.
- Moving elements run from `LevelClock` time, not their own timers, so a puzzle's timing is
  the same every attempt and resets correctly on respawn.
- PlayMode tests set `Physics.simulationMode = SimulationMode.Script` and step physics by
  hand with a fixed delta and scripted input. That keeps them deterministic and not flaky.

### 1.7 Ready for portals
Portals are a stretch goal, but the *gravity frame* (position, velocity, `Up`, facing) is a
value type that a 4×4 matrix can transform from day one. A portal teleport then becomes
"multiply by the portal pair's relative transform", and nothing else has to change.

### 1.8 Project layout
```
Assets/_Project/
  Scripts/Runtime/{Player,Gravity,Hazards,Portals,Level,UI}   -> GravBoots.Runtime.asmdef
  Scripts/Editor                                             -> GravBoots.Editor.asmdef
  Tests/EditMode, Tests/PlayMode                             -> test asmdefs
  Scenes/Sandbox.unity   (mechanics test bed)
  Prefabs, Materials, Art, Audio, Settings
```

---

## 2. Phased roadmap

Each phase has an **exit check** that you can play or that a test checks.

### Phase 0: Project foundation ✅
URP project, packages, folder structure, assembly definitions, test assemblies, git.
`SurfaceAlignment` math with EditMode tests, including the ceiling-drop roll.

### Phase 1A: Movement with any up direction + camera test bed ✅
The goal is to test the camera and the motor in every orientation **before** any boots logic
exists.
- Input actions: `Move`, `Look`, `Jump`, `ToggleBoots`, `Interact`, `Pause`, and debug keys
  (gamepad + keyboard/mouse).
- `GravityBody` (holds the gravity frame) and `PlayerMotor` (collide-and-slide, ground probe,
  slope limit, step-up, jump, coyote time, jump buffering), all relative to `Up` (§1.1).
- **Debug gravity keys**: set `Up` to any of the 6 axis directions (instantly or blended), or
  to "the face under the crosshair". This stands in for the boots, so the camera and motor can
  be tested on walls and ceilings in the first week.
- The camera rig from §1.4 with all three modes switchable at runtime, plus `CameraUpTracker`.
- **Debug overlay**: up vector, ground normal, probe hits, contact state, velocity, camera
  mode.
- **Camera test bed scene** (ProBuilder): a big cube room where all six faces can be walked on,
  a narrow corridor, a low-ceiling crawlspace, an outside corner, and a pit seen from the
  ceiling. Plus the metrics gym from §1.5.
- Settings ScriptableObjects (`MotorSettings`, `CameraSettings`) that can be edited while the
  game is running.
- **Exit check:**
  - Walking, jumping and the camera feel tight at Up = +Y, ±X, ±Z and −Y. No jitter on ramps or
    steps.
  - Blending Up between faces never breaks the movement mapping.
  - PlayMode tests: "grounded on a 30° ramp at every orientation", "can't climb 60° at every
    orientation".
  - Decision made: custom motor or KCC. First pick of camera mode (it can still change).

### Phase 1B: Magnetic boots (the core mechanic) (built; playtest pending)
Built: `MagneticBoots` (Off / Magnetized / Dropping), `BootsSettings`, the `Magnetic` layer and
panel material, `CurvedRamp` (quarter-pipes and rounded edges), the landing marker (red over
`Lethal`-tagged surfaces), boot glow, the HUD (boots state, plus a crosshair that turns blue over
magnetic surfaces) and the Boots Gym area. `MagneticBootsTests` cover the exit checks (curves at
all six orientations; sharp joins block or drop). Not done yet:
audio (clank, hum, thud), and a playtest of how the camera feels through corners.
- The boots states from §1.3, the `Magnetic` layer and its material.
- Orientation changes only along curved surfaces (quarter-pipes, rounded edges, pipes);
  sharp joins are impassable.
- Dropping, righting yourself and landing. The **landing marker**. The movement-mapping hold
  during transitions.
- Feedback: boot glow, a clank on attach, a hum while magnetized, an indicator that you're
  looking at a magnetic surface, and a landing thud.
- Test bed additions: ramp → wall → ceiling loop, a ceiling over a pit, a wall corridor, outside
  corners, a pipe, and a magnetic → non-magnetic seam.
- **Exit check:**
  - Floor → quarter-pipe → wall → ceiling → down the far wall with no pops or snagging, and
    no way round a right-angle join.
  - Dropping from any orientation lands upright with your heading kept.
  - PlayMode tests: "toggle off on the ceiling → grounded and upright within N seconds",
    "walk into a magnetic wall → Up == wall normal".

### Phase 2: Death, checkpoints and the game loop (built; playtest pending)
Built: `Health`, `PlayerSensor` (an overlap check after every motor step: `Lethal`-tagged
surfaces and `KillVolume`s kill; checkpoints and the exit are reported), `Checkpoint` (a
`SpawnPoint` with a trigger and a pad that lights), `LevelExit`, and `LevelSession` (start,
checkpoint, death → fall and fade → respawn in 1.2 s, the safety net 25 m outside the level's bounds,
the run clock and deaths; R returns to the checkpoint and Backspace restarts the chamber),
plus `SessionHud`. The landing marker also turns red over kill volumes. Level Kit: Checkpoint,
Kill Volume and Exit buttons, and "Add Game Loop" for older chambers. Test bed: kill volumes in
both pits, and the Checkpoint Run area. `GameLoopTests` cover it at all six orientations. The
`LevelClock` phase joins the respawn in Phase 3.
- `KillVolume` (death pits, plus a safety net under the whole level), `Health`, `Checkpoint`,
  `RespawnManager` (respawn restores the boots state, gravity frame and `LevelClock` phase).
- Level start and exit, a fast restart key, death → respawn in about a second (1.2 s, long enough to see the fall).
- A minimal HUD: boots state, health, checkpoint reached.

### 🚦 Gate: "Is it fun?" (chamber blocked out; first-time playtest pending)
Build **one** rough greybox chamber that combines a hidden magnetic route, a deliberate drop
onto a platform you can't reach otherwise, a death pit, and one simple turret placeholder.
Watch someone play it for the first time.
**Don't start the content phases until this chamber is fun.** If it isn't, iterate on the camera,
the boots and readability here.

**Built:** `Levels/Chamber_02.unity`, a 20 × 32 m hall with a ceiling at 13 m.
1. **Start:** a magnetic floor. The east wall has a quarter-pipe; the west wall panel meets the
   floor at a sharp join, which teaches that curves are routes.
2. **Hidden route:** up the east wall onto the ceiling, behind a soffit that hides it from the
   floor, then out over the pit. The ceiling checkpoint is here.
3. **The drop:** from the ceiling strip, about 7.5 m onto a magnetic island in the death pit.
   Nothing else connects to it.
4. **Sentry and exit:** a plane sentry stands on the island, and its plane covers the island
   and the bottom of the climb. Drop onto it from the ceiling to destroy it. Miss, and it
   turns and fires within about a second. A curved climb leads to the exit ledge.

Supporting pieces:
- `Hazards/Sentry.cs` (it replaced the 3D-aiming turret in the chamber):
  - It sees and shoots only in the plane parallel to its surface, 1 m up.
  - States: Idle → Lock (it notices you within 1.2 m of its plane, all round, and turns to
    face you) → Charge, then it holds → Fire, the instant your body is in the plane; one hit
    kills → Cooldown.
  - Charged while you're near the plane, so stepping, falling or jumping into it is an instant
    shot. Walking in from out of view takes about 0.25 s, and it keeps its charge for 0.6 s
    after losing you. The design intent is that the player never enters its plane, and speed
    is no escape.
  - A stomp from a drop destroys it for good. It crumbles into rubble round a stump
    (`SentryWreck`, `DebrisChunk`).
- `Hazards/Turret.cs`: the earlier placeholder. It aims in 3D and does 50 damage per hit. It
  felt generic, so it's kept only for comparison.
- `Debugging/PlaytestLog.cs`: writes each run's record.
- **GravBoots → Playtests**: draws the records in the Scene view.

**To run the gate:** a first-time player plays without help; watch where they hesitate, then
review the records in the Scene view.

### Onboarding chamber (built: `Levels/Chamber_04.unity`)
A longer level that introduces one idea per room, then combines them. Fourteen rooms and the
exit are joined by short corridors, each with a checkpoint.
1. **Walk:** a 2 m jump over a shallow, safe pit (1.2 m deep, so a jump gets you back out).
2. **Boots:** switch on at a magnetic patch; the gap after it needs the boots off to jump.
3. **First curve:** a ledge, half with a quarter-pipe and half with a sharp corner that can't
   be climbed.
4. **Ceiling:** up onto the ceiling over a lethal floor, then the first drop. The marker shows
   red over the lethal floor and blue over the landing.
5. **Over the edge:** a block with rounded top edges: up, over and down the far side.
6. **Drop with stakes:** a ceiling route to the only drop onto an island in a death pit, then a
   climb to the exit.
7. **Platforms:** an elevator, then a ferry over a pit.
8. **Upside down:** a ceiling shuttle; drop off at the far end.
9. **Meet a sentry:** a walkway above its plane. A gap forces a detour along the wall to stay
   out of it.
10. **Stomp:** a sentry guards the floor; drop on it from the ceiling, using the gold marker.
11. **Finale:** a ceiling shuttle and a patrolling sentry share a 12 s cycle, so the stomp lines
    up. Then a ferry to the exit.
12. **Corkscrew:** floor, east wall, ceiling, west wall. The west wall meets the floor at a sharp
    corner, so the only way onto it is over the ceiling. Then along it over a pit and a drop
    from the wall onto the sentry guarding the far landing (landing anywhere else on it is in
    its plane).
13. **Diving board:** up and over a full-width block, out along a narrow board, and off its end
    onto a ferry waiting just beyond it. Walking off at either speed lands on the 5 m deck
    (16.4 m to 19.3 m along the room); jumping off overshoots.
14. **Gauntlet (challenge):** onto the ceiling, then two upside-down shuttles that meet only in a
    shared 2 s pause (both 12 s cycles, offset 6 s). From the second, drop onto the sentry on an
    island in the pit; missing it lands you in its plane. Then a running jump off the island
    onto the sentry guarding the exit landing: taking off in the island's last metre lands on
    it, earlier hits its body.
15. **Exit:** the exit elevator.

A scripted run gets through every room without dying, in about 225 s of game time.

**Still missing:** a way to tell the player the controls in the early rooms, for example signs
or a key prompt. Rooms 1 and 2 assume they know Space jumps and E toggles the boots.

### Phase 3: Moving elements and timing hazards (movers built; hazards to come)
**Built:**
- **`KinematicMover`:**
  - It follows a path of points, each a position and a rotation.
  - Modes: ping-pong, loop, or on-step (it waits until the player stands on it).
  - It eases in and out, and turning counts toward the path length, so a turntable works.
  - It's driven by `LevelClock`, which a respawn rewinds to the checkpoint's time.
- **Riding:** the motor rides whatever it stands on.
  - It's carried by that object's translation and rotation, in any orientation.
  - When magnetized, the gravity frame turns with the surface.
  - Leaving keeps the platform's velocity.
- **Sentries on tracks:** a sentry can be a mover's platform.
  - Its aim turns with it, and the aim lock holds a point rather than a direction, so a
    moving sentry still hits.
  - A destroyed sentry stops on its track.
- **Test level `Levels/Chamber_03.unity`:**
  1. an elevator;
  2. a ferry over a death pit;
  3. up onto the ceiling and across on a moving ceiling panel, upside down;
  4. a drop onto a patrolling sentry;
  5. a curve up to the exit, beside a second sentry that rides its own track up the wall.

**Still to do:**
- **Crushing:** a platform moving into the player only pushes them aside. Crushers need
  detection: still overlapping after depenetration should kill.
- **Smoother visuals:** platforms move at the physics rate, while the player is interpolated.
  If riding looks juddery, interpolate the platform visuals.
- Crushers, sliding doors, rotating arms, piston panels that push the player off magnetic
  surfaces, and **magnetic panels that switch on and off on a timer** (they flicker before
  switching off, and the player drops when one does).
- **Exit check:** riding a moving magnetic platform upside down stays stable, and every hazard
  gives a readable warning before it fires.

### Phase 4: Sentries (plane-firing robots)
Decided during the gate. The enemy is the **plane sentry** (`Hazards/Sentry.cs`, prototyped).
It sees and shoots only in the plane parallel to the surface it stands on, so the counterplay is
changing surface. One hit kills, and dropping onto one destroys it for good.
- **Sentries on tracks:** they ride rails using the Phase 3 `KinematicMover`. Rails can run
  over curves, so a sentry's plane swings across the room as it climbs onto a wall.
- **Readability:** keep the red line where its plane meets the room, and add audio cues for
  lock and charge (ElevenLabs).
- **Next hazard candidates from the brainstorm:**
  - laser planes: a full-room sheet of light that rises and falls;
  - magnet mines: drawn to the boots, shed by letting go;
  - charged panels.
- **Exit check:** a room with a stationary sentry and a tracked one, where the only safe route
  changes surface and one sentry must be stomped.

### Phase 5: Visual identity (sci-fi test chamber)
- A modular kit: white magnetic panels, dark non-magnetic metal, glass, orange and blue accent
  lights. Swap the greybox for final meshes.
- URP: baked plus mixed lighting, light probes, SSAO, bloom on emissives, subtle fog.
  **Check after every lighting change that magnetic and non-magnetic surfaces are still easy
  to tell apart.**
- VFX: boot magnet field, laser beam and impacts, sparks, landing dust. Audio: ambient hum,
  boot clanks, turret tones, music stingers.

### Phase 6: Content and polish
- 6–10 test chambers that introduce one idea at a time: walking on walls → ceilings →
  deliberate drops → pits → timing → turrets → combinations.
- Settings menu (sensitivity, inverted look, FOV, camera mode and comfort, toggle vs hold
  boots), pause menu, level select, best times.
- Linux standalone build through `unity build`. Optionally CI with `unity ci init`.

### Phase 7 (stretch): Portals
Only once the boots-only game passes the fun gate *and* the chambers are running out of new
ideas.
- Fixed portal pairs, each rendering through its partner's camera in URP (render texture,
  oblique near-clip plane, recursion limit of 1–2).
- Teleporting transforms the gravity frame (§1.7). Boots on + exit portal on a magnetic wall →
  the player arrives attached to it. Boots off → the drop and righting apply.
- Handle the player straddling a portal (a clone mesh). Projectiles pass through portals is a
  further stretch goal.

---

## 3. Feel and refinement loop
1. **Settings ScriptableObjects** (`MotorSettings`, `BootsSettings`, `CameraSettings`) that can
   be edited while the game runs and saved.
2. **Live tuning through the CLI**: with the editor open, `unity command eval '…'` reads and
   changes values while the game is playing.
3. **Debug overlay** from Phase 1A onward.
4. **Deterministic PlayMode tests** (§1.6) for every movement promise in the exit checks. Run
   them headless with `unity test --mode PlayMode`.
5. **Playtest checklist for each change**: inside and outside corners, curves, dropping at every
   orientation, landing marker accuracy, movers at every orientation, camera comfort.

| Risk | Mitigation |
|---|---|
| Camera disorientation or clipping | Test bed in week 1, three modes A/B tested, pull-in and swing-around after transitions, comfort options. |
| Jitter or snagging at transitions | Face normals plus smoothing, pivot at the capsule centre, penetration recovery, overlay. KCC fallback. |
| Hidden world-up assumptions | Motor written relative to `Up` from day one, tests at all 6 orientations. |
| Unfair drops | Mid-air re-attach off in v1, landing marker, standard drop heights. |
| Moving platforms in non-standard gravity | Fixed update order, inherit motion in the motor's own frame, tests in Phase 3. |
| Portal scope | Stretch only. The gravity frame is already transformable, so nothing gets rewritten later. |

---

## 4. Immediate next steps (Phase 1A)
1. Input actions and debug keys.
2. `GravityBody` + `PlayerMotor`, written relative to `Up`, as a capsule with a placeholder mesh.
3. `CameraUpTracker` + Cinemachine rig with the three camera modes.
4. Debug overlay.
5. Camera test bed and metrics gym scenes in ProBuilder, plus `docs/LEVEL_METRICS.md`.
6. PlayMode tests at all 6 orientations.
