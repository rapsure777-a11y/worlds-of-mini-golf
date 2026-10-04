# Development log

## 2026-10-03 (evening): Tropical Adventure production begins, Hole 1 visual showcase (checkpoint)

- Baseline tagged `v0.1-prototype` (`afdf464`) before any art work.
- Built a reusable, code-generated tropical art kit (52 meshes), a shared palette texture, generated textures and three URP/SPI shaders (StylizedLit, StylizedWater, GradientSky).
- Generated island terrain with automatic hole-site levelling and paths, an ocean with baked shore distance, distant islands, clouds, gradient sky and warm lighting (no post-processing).
- Hole 1 now runs along the south beach (layout unchanged) and is fully dressed: tiki clubhouse, welcome/controls board, hole sign, torches, flower beds, pier, palms, sandstone backdrop. Hole 2 moved to its new site with basic dressing.
- Terrain and rocks count as out of bounds on contact (`OutOfBoundsSurface`).
- Iterated four times on desktop screenshots. Fixed crowded palms, grey pads, egg-shaped flames, noisy faceted cliffs, a green horizon (replaced Unity's procedural sky), washed-out sand, hidden blossoms and monotone greens.
- **Regression caught by the scene test:** after rotating Hole 1, two of its backdrop cliffs landed on Hole 2's green (their out-of-bounds colliders sent the ball back). Moved them; added an automatic obstruction scan in the world builder and the test `TropicalScene_NoSceneryOnGreens`.
- Tests 41/41; both builds succeed (118 MB); smoke test passes on the new Hole 1. Scene budget: 585k triangles, 13 materials, 532/566 renderers static-batched.
- **Not headset-verified:** custom shaders in SPI, VR frame time with the new world, auto putter length, wrist watch.
- Checkpoint write-up: `Docs/CHECKPOINT_HOLE1.md`. Holes 3–9 are on hold pending visual approval.

## 2026-10-03 (afternoon, test 4-5): Frame profile works; auto putter length

- Native Steam Frame profile accepted once XR_VALVE_frame_controller_interaction was requested. First build read hand pose from wrong bytes (offsets copied from the Index layout), so the putter swung erratically. Fixed with correct offsets (33/36/40/52/100/112) and layout tests. Andrew: 'worked perfectly', 'really, really smooth'.
- Feedback: putter about 20% short. Added Walkabout-style auto length: standing eye height (90th percentile of head height, so time bent over putts is ignored) x 0.62 (1.02 m at 1.65 m eye height). Grip + stick now sets a saved personal offset on top. Length only changes while the putter is still. Eye height, offset and length are in the session log. Tests 40/40.

## 2026-10-03 (afternoon, test 3): smooth at 120 Hz; Frame profile needs its extension

- **Performance fixed:** frame delta flat 8.33 ms (120 Hz) for the whole session, Unity GPU timer 1.6-7 ms. Earlier sessions spent about half their frames at 60 Hz. The VR render-settings change worked. Andrew: 'totally smooth'.
- **Steam Frame profile was rejected** (XR_ERROR_PATH_UNSUPPORTED). SteamVR fell back to Touch emulation, so right X/Y still merged into B. Cause: the profile is gated behind the OpenXR extension XR_VALVE_frame_controller_interaction, which SteamVR advertises but the feature did not request. Fixed by declaring the extension on the feature. Needs a headset check: the session log should list 'SteamFrameController' devices instead of 'OculusTouchController'.
- Left controller only connected 163 s into the session; left buttons worked once it did.

## 2026-10-03 (afternoon): first Steam Frame playtest feedback

**Andrew's verdict:** physics "absolutely great"; teleport, snap turn and grab-move great. Problems: B didn't return the ball, scorecard not seen, Y didn't swap hands.

**Diagnosis (session logs + SteamVR's Frame remapping file)**
- SteamVR exposes the Frame controllers as **Oculus Touch**. The Frame has A/B/X/Y all on the right controller and a D-pad on the left. In Touch emulation, right B, X and Y all become Touch B; left D-pad down = X; left D-pad up/left/right = Y. So the "X" and "Y" presses arrived as B. The scorecard and hand swap live on the left D-pad.
- B did fire (about 30 log entries), but it returned the ball to its last *resting* spot, which is where it already was, so nothing visibly happened.
- At the moment the controllers connected, the hand pose jumped from the floor origin to the hand, and the putter swept through the ball: an accidental stroke at a logged "36 m/s".
- Putter needed no adjustment (0.85 m, 0°, 0°). Strikes looked clean (e.g. head 1.96 → ball 2.58 m/s, 3° aim error).
- Performance: app GPU median 6.5 ms, p99 10.8 ms against an 8.3 ms budget at 120 Hz. SteamVR ran about half the frames at 60 Hz.

**Fixes**
- B returns the ball to where the last shot was played from (the tee before any shot). The stroke still counts; there's a sound and haptic.
- Putter ignores tracking jumps (>0.3 m in one frame, or head speed >15 m/s): no stroke when controllers connect.
- Scorecard appears automatically for 5 s after each hole and stays at course end.
- Controls sign beside the first tee with the Frame layout; test guide corrected.
- VR render settings: SSAO off, depth/opaque copies off, HDR off, 2 shadow cascades, low soft-shadow quality (MSAA 4x kept). The GPU effect still needs measuring in the headset.
- Session log now records every button press with its control path, CPU/GPU frame times, the refresh rates seen and compositor dropped frames.
- Tests: 34/34 (added return-to-shot-spot and tracking-jump tests).

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

## 2026-10-03: Graphical Pass 2, cloud integration (UNVERIFIED)
Branch `cloud/graphical-pass-2-integration`, written by Cloud Claude with no Unity/Blender/GPU available.

- HeroKit connected to `SceneBuilder`/`TropicalWorld`; splat terrain (`Hero_Terrain`); `Hero_Turf`/`Hero_Rail` on the course.
- Hole 1 rebuilt around Blender hero assets: tiki clubhouse, palms, terraced cliff wall with raycast-fitted waterfall, carved plunge pool, mist, mesas, boulders, sea arch, five-layer leaf-card jungle.
- Rendering: three URP presets (Lean = the old measured settings, Balanced = +depth/opaque, Rich = +HDR/post/4 cascades) with a runtime `QualityPreset` (`-quality`, F9); post-processing Volume; water depth keyword with a no-depth fallback; new `Gamebreak/Mist` shader.
- **Status:** syntax-parsed only (tree-sitter). Not compiled, not rendered, tests not run (41/41 baseline remains on `main`), no build, no benchmark, no headset. See `Docs/CLOUD_RETURN_REPORT.md` and handoff section G.
