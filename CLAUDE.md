# GravBoots (Unity 6000.5.9f1, URP)

Magnetic-boots 3D puzzle and obstacle course. Design and roadmap: `docs/PLAN.md`. Level sizes:
`docs/LEVEL_METRICS.md`.

- Game code: `Assets/_Project/Scripts/Runtime` (namespaces `GravBoots.*`, asmdef `GravBoots.Runtime`).
- The player motor works for any up direction: no `Vector3.up` in `Player/` except where world gravity is defined. Keep orientation math in pure static helpers (see `Gravity/SurfaceAlignment.cs`) with EditMode tests.
- PlayMode tests step `PlayerMotor.Tick` by hand (`AutoSimulate = false`) at all 6 orientations. Add a case there for every movement promise.
- Run tests headless (Editor closed): `unity test . --mode EditMode --output Logs/test-EditMode.xml` (also `--mode PlayMode`).
- The test bed scene and Player prefab are generated: menu **GravBoots → Build Camera Test Bed**, or headless with `unity run . -- -executeMethod GravBoots.Editor.TestBedBuilder.Build` (no `-quit`; the CLI adds it). Change the builder rather than hand-editing the generated scene.

## Driving the open Editor (Pipeline package)
- Command names are `<group> <name>` as two words, e.g. `unity command editor set_auto_tick`, `unity command script eval '<C#>'`, `unity command console get -- --level error --tail 20`, `unity command runtime simulate_key -- --key W --action down`. Put command parameters after `--`. Check names with `unity command <group>` (for example `editor`, `editor.playmode`, `capture`, `console`, `script`, `runtime`).
- If every main-thread command times out, look for a modal dialog in the Editor (`xdotool search --pid <pid>` lists window titles). The Editor's main loop doesn't run while a modal is open.
- Don't hand-edit `.unity`/`.prefab` YAML while an Editor is reachable.
