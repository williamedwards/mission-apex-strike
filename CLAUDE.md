# GravBoots (Unity 6000.5.9f1, URP)

Magnetic-boots 3D obstacle course. Design and roadmap: `docs/PLAN.md`.

- Game code: `Assets/_Project/Scripts/Runtime` (namespace `GravBoots.*`, asmdef `GravBoots.Runtime`).
- Keep orientation/gravity math in pure static helpers (see `Gravity/SurfaceAlignment.cs`) with EditMode tests.
- Run tests headless: `unity test . --mode EditMode --output Logs/test-results.xml` (also `--mode PlayMode`).
- With the Editor open, drive it via the Pipeline package: `unity status`, `unity command`, `unity command eval '<C#>'`. Don't hand-edit `.unity`/`.prefab` YAML while an Editor is reachable.
