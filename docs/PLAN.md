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
     │     on the floor, toggle off (no fall)      │    │ walks across a         │
     └─────────────────────────────────────────────┘    │ seam onto connected    │
     └──────────────────────────────── lands upright ───┴────────────────────────┘
```
- **Normal**: `Up = Vector3.up`, standard platformer movement, can jump.
- **Magnetized**: `Up` follows the face normal of the magnetic surface underfoot.
  - *Same surface vs transition*: when the normal changes by less than about 25° per probe, it
    is followed continuously (ramps, curved pipes). A larger change triggers a corner
    transition.
  - *Concave corner* (wall ahead, steep ramp): a forward capsule probe finds the new face. The
    player pivots onto it over about 0.15 s.
  - *Convex corner* (walking over an edge onto the far side): the ground probe finds nothing, so
    probe down-and-back around the edge and wrap the player around it.
  - Walking off a magnetic surface onto a non-magnetic one, or into empty space: no
    attachment, so it behaves like a drop. This is intentional, and the landing marker shows
    where you'd land.
  - Jumping while magnetized: undecided. Either not allowed or a small hop along `Up` that
    re-attaches to the same face. Playtest both in Phase 1B.
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

### Phase 1A: Movement with any up direction + camera test bed (start here)
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

### Phase 1B: Magnetic boots (the core mechanic)
- The boots states from §1.3, the `Magnetic` layer and its material.
- Concave and convex corner transitions. Continuous normal following on curves.
- Dropping, righting yourself and landing. The **landing marker**. The movement-mapping hold
  during transitions.
- Feedback: boot glow, a clank on attach, a hum while magnetized, an indicator that you're
  looking at a magnetic surface, and a landing thud.
- Test bed additions: ramp → wall → ceiling loop, a ceiling over a pit, a wall corridor, outside
  corners, a pipe, and a magnetic → non-magnetic seam.
- **Exit check:**
  - Floor → 70° ramp → wall → ceiling → down the far wall with no pops or snagging.
  - Dropping from any orientation lands upright with your heading kept.
  - PlayMode tests: "toggle off on the ceiling → grounded and upright within N seconds",
    "walk into a magnetic wall → Up == wall normal".

### Phase 2: Death, checkpoints and the game loop
- `KillVolume` (death pits, plus a safety net under the whole level), `Health`, `Checkpoint`,
  `RespawnManager` (respawn restores the boots state, gravity frame and `LevelClock` phase).
- Level start and exit, a fast restart key, death → respawn in under 1 second.
- A minimal HUD: boots state, health, checkpoint reached.

### 🚦 Gate: "Is it fun?"
Build **one** rough greybox chamber that combines a hidden magnetic route, a deliberate drop
onto a platform you can't reach otherwise, a death pit, and one simple turret placeholder.
Watch someone play it for the first time.
**Don't start the content phases until this chamber is fun.** If it isn't, iterate on the camera,
the boots and readability here.

### Phase 3: Moving elements and timing hazards
- A generic `KinematicMover` (waypoints or curves; ping-pong, loop or trigger modes) driven by
  `LevelClock`.
- The motor inherits platform velocity and rotation **in any orientation**. Riding a moving
  magnetic panel upside down is a key showcase.
- Crushers, sliding doors, rotating arms, piston panels that push the player off magnetic
  surfaces, and **magnetic panels that switch on and off on a timer** (they flicker before
  switching off, and the player drops when one does).
- **Exit check:** riding a moving magnetic platform upside down stays stable, and every hazard
  gives a readable warning before it fires.

### Phase 4: Laser enemies
- `Turret` states: Idle → Track (line-of-sight cone, laser sight) → Charge (wind-up
  warning) → Fire → Cooldown. Two weapon types: hitscan beam and dodgeable bolts (pooled).
- Turrets can be mounted on any surface and aim correctly whatever orientation the player is
  in.
- Counterplay comes from the route: break line of sight by moving onto another surface, take
  cover, or drop past.
- Damage and knockback, which can knock the player off a magnetic surface.
- **Exit check:** a room you can clear only by moving between surfaces to dodge charged shots.

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
