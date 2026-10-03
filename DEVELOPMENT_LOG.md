# Development log

## 2026-10-03 (overnight): headset-free validation and foundations

**Done**
- **Tests: 32/32 pass** (editor PlayMode; also verified through the Unity CLI `unity test`). Latest additions: max-speed (9 m/s) rail hits at 0/30/60/80° never tunnel, and a firm shot into the dogleg corner stays in play. New since Milestone 1: multi-hole progression and scorecard totals, restart during advance delay, stroke limit, free reset, slow edge putt drops, glancing fast putt lips out, 45° rail rebound (leaves at −37.5°), ramp climb and roll-back, rail-top rest = out of bounds, stuck-ball safeguard, putter misuse (2 cm/s nudge ignored, follow-through no double hit, backswing strikes backwards with two-faced head, spawn-overlap ignored, 12 m/s swing caught and clamped), full scene load + hole 1 + progression through hole 2.
- **Builds:** `Builds/Windows/WorldsOfMiniGolf.exe` (PCVR, OpenXR) and `Builds/Desktop/WorldsOfMiniGolf_Desktop.exe` (OpenXR loader removed; never touches SteamVR). Zero compiler warnings.
- **Built-player smoke test** (`Tools/smoke-test.ps1`, or `-smoketest` on either exe): scripted swing of the real putter at 2.4 m/s gives a 3.24 m/s strike and a hole in one, then advances to hole 2 and opens the scorecard. Both builds pass. Without SteamVR the PCVR build falls back to desktop mode cleanly.
- **Desktop debug mode:** hold the left mouse button to ground the putter and swing with the mouse; WASD/right mouse, Q/E height, T stand at ball, R return, Tab scorecard, 1–9 jump to hole, N next, Backspace restart, F1 help panel. The IMGUI overlay shows FPS, ball and strike speed, and putter settings (also on the monitor during VR).
- **Session log** (always on): XR device and controller layouts, every strike (head speed, ball speed, face-vs-path, aim error), hole results, putter settings, frame-time percentiles. Written to `%USERPROFILE%\AppData\LocalLow\Gamebreak Labs\Worlds of Mini Golf\Sessions\`.
- **Rules:** a ball resting anywhere except a green (`PlayableSurface`) is out of bounds; a ball still creeping after 20 s is stopped in place.
- **Draft Hole 2 "Palm Corner"** (L-shaped dogleg, par 3), so progression can be tested in VR. Pending HQ creative approval.
- Scorecard rebuilt as a real grid (under/over par colours, current hole highlighted).
- VR input hardening: grip/trigger read from analog axes (Index-style grip click needs a hard squeeze); controller poses are read whenever bound. Verified the binding paths against the OpenXR package's Index profile.

**Bugs found and fixed**
- **Player build crashed on load** (`level0 is corrupted`): `ScoreUI.cs` contained two MonoBehaviours whose names didn't match the file. The editor tolerated it, but player builds could not resolve the scripts. Split into one class per file, and added a pre-build guard that blocks the build if any component script can't be resolved.
- Desktop build still pre-initialised OpenXR from boot.config (6 s stall, tries SteamVR). Now the loader is removed for that build and restored afterwards.
- A pending hole advance fired after a restart during the 3 s delay.
- Desktop "stand at ball" used the VR side-on stance (wrong for mouse putting); camera yaw was not synced on teleport.
- The smoke-test swing was sampled with a one-frame lag (strike speed drifted with frame rate). It's now driven before the putter samples the hand.

**Still not verified (needs the headset):** tracking, putter orientation on Frame controllers, SPI rendering in the HMD, haptics, comfort, VR frame timing, which interaction profile SteamVR selects.

## 2026-10-03: Milestone 1 build (First Physical Putt)

**Done**
- Unity 6000.3.9f1 project from the URP template; added OpenXR 1.18 / XR Management 4.7 / Input System 1.20. Removed collab, multiplayer center and visual scripting.
- `ProjectSetup` configures Windows x64, D3D11 only, Linear, PC URP asset (MSAA 4x), OpenXR loader with Single Pass Instanced and 7 controller profiles.
- Gameplay core: `GolfBall` (own rolling model), `Putter` (tracked, swept strike test), `Cup` (real hole in mesh), `HoleController` (strokes, OOB +1 and return, reset, stroke limit 10), `CourseController` (hole sequence, scorecard), `VRRig` (tracking, teleport arc, snap turn, grab-move, putter length/angle/twist, handedness, go-to-ball, desktop debug mode), wrist display, scorecard panel, synthesised SFX and haptics.
- `CourseGeometry`: welded green mesh from layout and height function, real cup hole and cup, rails around the outline.
- `SceneBuilder` generates `TropicalAdventure.unity`: Hole 1 "Beach Warm-up" (7 m lane, 8 cm rise, par 2) on a greybox island with primitive palms and rocks.
- Windows player builds (99 MB, `Builds/Windows/`).
- `Tools/WalkaboutProbe` measurement mod built and deployed to the Walkabout Mods folder; `analyze.py` turns recordings into tuning numbers.

**Tests (PlayMode, editor, no headset): 10/10 pass**
| Test | Result |
|---|---|
| Roll-out from 2 m/s | 3.046 m vs 3.056 m analytic |
| Putt at cup | holed, 1 stroke |
| 5 m/s over cup | skips; hops about 4 cm off the far lip and loses about half its speed |
| Perpendicular wall rebound | ratio 0.72 (= tuning) |
| OOB | returns to last rest spot, +1 stroke |
| 2% slope holds ball / 12% slope rolls it | pass |
| Putter swing 1.5 m/s | ball about 2.0 m/s straight down the face normal |
| 10° open face | launch 8.5° off line |

**Bugs found and fixed through tests**
- The contact normal from (body centre minus contact point) tilted up to 35° at speed, because PhysX contact points are from the start of the step. This was braking the ball on flat ground. Now uses the start-of-step centre.
- Rest detection compared total speed including the ground-hold push, so the ball never rested. Now uses surface speed.
- PhysX reports face normals on the cup rim edge, which our wall logic treated as a wall bounce. Fixed by the start-of-step normal.
- A rigid inelastic cup lip launched fast balls about 40 cm high. Upward speed added per contact is now capped (`maxContactLift` 0.5 m/s).
- Cups snapped to cell centres (5 cm off the requested spot). An even cup tile now snaps to grid vertices exactly.

**Not yet verified:** anything in the headset (tracking, putter orientation on Frame controllers, SPI rendering of all materials, comfort, frame rate).
