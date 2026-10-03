# Worlds of Mini Golf

Original Unity PCVR mini-golf game inspired by (not copied from) Walkabout Mini Golf. Read `PROJECT_CONTEXT.md` first, then `DEVELOPMENT_LOG.md` and `LOCAL_TASKS.md`. `HANDOFF.md` is the original bootstrap note (tooling install details).

Rules that always apply:
- Never put Walkabout code, assets or extracted game files in this repo. Probe outputs stay in the game folder. Art must be original or permissively licensed; log licences in `ASSETS.md`.
- Target: Windows x64, Unity 6000.3.9f1, URP 17.3, Direct3D11, OpenXR, Single Pass Instanced, Linear. The headset is a Steam Frame streaming over PCVR; the game runs on the PC.
- Seven worlds on the roadmap; only World 1 (Tropical Adventure) is authorised. No multiplayer or progression systems yet.
- Golf systems (`Scripts/Runtime/Core`, `Player`) must stay world-agnostic; worlds are data (`WorldTheme`, hole definitions, set dressing).
- Scenes are generated: change hole layouts in `TropicalCourse`, then run `Automation.Setup`.
- Run the PlayMode tests after any physics change. Report editor-tested and headset-tested separately.
- Confirm before pushing to a remote or creating a public repo.
- Keep chat output small; write logs to `Logs/`.
