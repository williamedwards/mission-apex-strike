# Level Metrics

These are the standard measurements that every chamber is built from (docs/PLAN.md §1.5). They
come from `MotorSettings` and `CameraSettings`. Where a value is a tuning number, change the
asset first and then update this table. The **Metrics Gym** area in `CameraTestBed.unity` shows
each one in the level.

Rooms are built on a **1 m grid**, and the greybox texture shows 1 m cells with faint 0.5 m lines.

## Player
| Metric | Value | Source |
|---|---|---|
| Capsule height / radius | 1.8 m / 0.35 m | Player prefab |
| Walk speed | 6 m/s | `MotorSettings.moveSpeed` |
| Jump height | 1.3 m | `MotorSettings.jumpHeight` |
| Gravity | 25 m/s² | `MotorSettings.gravity` |
| Max walkable slope | 50° | `MotorSettings.slopeLimit` |
| Max step height (no jump) | 0.35 m | `MotorSettings.stepHeight` |

Running jump distance is about `2 × speed × √(2h/g)`, which is **≈ 3.9 m** gap to gap at the
current values. The gym has gaps of 2, 3, 4 and 5 m: 2 and 3 m are comfortable, 4 m is the limit,
and 5 m is impossible.

## Camera
| Metric | Value | Source |
|---|---|---|
| Pivot height | 1.45 m | `CameraSettings.pivotHeight` |
| Shoulder offset | 0.55 m right, 0.15 m up | `CameraSettings.shoulderOffset` |
| Distance behind pivot | 3.5 m | `CameraSettings.cameraDistance` |
| Field of view | 65° | `CameraSettings.fieldOfView` |

## Spaces (provisional, confirm in playtests)
| Space | Minimum | Comfortable | Why |
|---|---|---|---|
| Corridor width | 2 m | 3 m | The camera's shoulder offset and collision radius need room beside the player. |
| Corridor height | 2.2 m | 3 m | 2.2 m is the crawlspace stress case for turning over (capsule 1.8 m). At 3 m, walking on the roof still leaves camera room. |
| Room for walking on all faces | 8 m | 12–16 m | The camera needs distance from the opposite face to frame the player. |

## Drop heights (provisional)
There's no fall damage yet. These are the bands to design drops around once landing feedback and
the landing marker exist.
| Band | Height | Use |
|---|---|---|
| Safe | ≤ 4 m | Routine drops between surfaces. |
| Long | 4–8 m | Deliberate drops. Time to read the landing marker. |
| Lethal (if fall damage is added) | > 8 m | Only as an explicit hazard. |

## Ramps in the gym
20°, 35° and 50° are walkable and finish at a landing. 60° and 70° are not walkable (dark
material) and will be the "only with boots" slopes in Phase 1B.
