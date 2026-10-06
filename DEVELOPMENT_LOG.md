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

## 2026-10-04: Milestone 3 checkpoint 1 (UNVERIFIED)
Branch `milestone-3-island-hopping`, written by Cloud Claude (no Unity/Blender). Cluster music system (`MusicDirector`, `GolfAudio`), island clusters as data, Jungle Island terrain with a ravine, Hole 3 "Jungle Crossing" with a playable wooden bridge, island pier, transition fade, Milestone 3 tests. Syntax-parsed only. See `Docs/MILESTONE3_HANDOFF.md`.

## 2026-10-03 (night): headset feedback round (M3 checkpoint 1)

Feedback from Andrew's first Jungle Island headset session. All fixes below are verified by tests (70/70) and screenshots; VR feel confirmed by Andrew for sliders, restart and trees.

- **Barrels and crates:** new Blender hero props (`Tools/Blender/hero_props.py` -> `HeroBarrel_0`, `HeroCrate_0/1`; original work, no third-party assets). Primitive colliders. Small crate now stacks on the big crate's actual position (was floating); pier crate no longer sunk into the planks; second barrel moved off the pier.
- **Jungle trees:** about 10 outward/upward-facing cards per branch cluster with minimum spacing and one shared, gentler wind weight per cluster (cards no longer slide through each other); clusters centred on the branch tips plus cards along the outer branch so foliage reads as attached.
- **Signs:** one solid board with a frame instead of gapped planks (the text no longer runs across seams).
- **Scorecard:** forgiving touch zones, a cursor dot, and Restart Hole / Restart Course buttons (course restart needs a second press within 3 s). The putter head is a pointer: rest it on a slider for 0.3 s and it slides relatively (never jumps to an end); rest it on a button for 0.7 s to press. Bug fixed: the idle hand was resetting the club's hold timer every frame.
- **"No sound" was a saved setting:** both volumes had been saved at 0 after the putter snapped the sliders to the left end. Reset, and the relative drag prevents a repeat. Separate user-side issue: two streaming sessions on the PC glitched the headset audio.
- **Course plan (Andrew):** two holes per zone for zones 1-4 (Starting, Jungle, Temple, Volcanic), then one long finale hole in the Summit zone: 9 holes. Hole 4 (second Jungle hole) is next.
- **Known flaky test:** `Swing_StrikesBallAlongFace` occasionally fails when run alone (timing-dependent); passes in the full suite.

## 2026-10-04: Hole 4, score celebrations, tiki props (editor-verified; headset pending)

- **Hole 4 "Hollow Drop" (par 4, Jungle Island east side):** S-shaped lane, 24 cm drop ramp, low basin, 10% climb to a raised cup; soft putts roll back without penalty. `TropicalCourse.Hole04`, `JungleIsland.DressHole4`; the ravine is now only cut for holes with a bridge. Tests: layout, no cliffs, drop, soft roll-back, firm putt tops the climb, holing out; the course-progression test plays 4 holes.
- **`ScoreCelebration`:** tiers from `TierFor(strokes, par)`: hole in one / albatross / eagle / birdie (confetti + label + fanfare + haptics), par (small sparkle), bogey or worse (quiet label). Particles use a bright copy of `Hero_Mist`. Tests: tier mapping, burst sizes, label content and cleanup.
- **Tiki props:** `Tools/Blender/hero_tiki.py` -> `HeroTikiPole_0/1`, `HeroTikiMask_0` (original work). Placed on holes 1, 2 and 4 via `TropicalWorld.Tiki` (capsule colliders, turned 180 degrees because of the FBX axis mapping).
- **Course plan recorded:** 9 holes in five zones (2+2+2+2+1); see `RESUME.md`.
- Tests: 78/78 at the last full run. Both players rebuilt.

## 2026-10-04 (later): Hole 4 confirmed, pushed; tiki fix; headset feedback

- **Hole 4 "Hollow Drop" exists and is on GitHub** (`milestone-3-island-hopping`; code in `HoleDefinition.Hole04`, dressing in `JungleIsland.DressHole4`, tests in the Milestone 3 suite). Holes 1-3 unchanged. 78/78 PlayMode tests pass, both players build with 0 errors. The cloud could not see it earlier only because it had never been pushed.
- **Tiki fix:** crest feathers (mask and bird-head pole) were tilted inward and crossed; the bird's eyes were buried in the head. Fixed in `hero_tiki.py`, models regenerated.
- **Andrew's headset feedback on Hole 4 (for the design sprint, nothing changed yet):** too long with too many turns to reach the cup in 4 strokes (not hard, just long); holes feel flat, generic, and lack obstacles. Hole redesign is deferred until the cloud reviews the real hole.

## 2026-10-04 (night): Hole 4 rebuilt as drop, ramp jump and bowl (par 3)

- **Andrew's calls:** keep the drop, smaller circle, par 3. The see-through railings he saw were on the proving ground's launch-ramp hole.
- **Hole 4 "Hollow Drop" is now:** 1.2 m lane with the 24 cm drop (z 1.4-4.0), a 1 m run-up, the 14 degree launch ramp (lip at z 5.6), a 0.3 m gap with a pit, and a 1.6 m-radius roulette bowl offset 0.5 m so the ball enters at an angle, with the cup at its lowest point. About 9 m end to end (was 14 m, five legs).
- **Code:** `HoleDefinition.buildExtras` / `extraAreas` (a hole can hang pieces and a cup off its lane), `HoleFactory` uses them, `ProvingGround.BuildJumpBowlPieces`, `JumpBowlSpec` (drop, `LaneLayout`, `BowlFootprint`), `TropicalCourse.Hole4Spec()`. The bowl wall and notch curb now stand on a solid base. Terrain plateau and foliage keep-out cover the bowl (`TropicalWorld.LayoutBounds(def)`); `JungleIsland.DressHole4` re-placed for the new footprint.
- **Rails:** `CourseGeometry.BuildWalls` now closes the rail corner posts where a rail meets an open edge (they were hollow, which looked see-through). Only runs for layouts with open edges, so Holes 1-3 are unchanged.
- **Tee putt sweep (Default):** 1.6-2.2 m/s stay on the lane; 2.6-3.0 fall short in the pit (+1 stroke); **3.4-4.6 land in the bowl with 1 stroke** (two end on the apron next to the cup). Tests: 106/106, scene and both players rebuilt, screenshots checked.

## 2026-10-04 (late night): Hole 4 renamed "Tiki Twister", given a turn and a longer jump; tiki crest fixed

- **Headset feedback:** Hole 4 was "way too easy" (the ramp was too close to the circle to miss); the tiki mask top still clipped slightly; "where is the water wheel?" (it only exists in the proving-ground build, not in the course yet). Andrew renamed the hole: **Tiki Twister**.
- **Hole 4 now:** 1.2 m lane with the 24 cm drop, then one 90 degree turn at the end of the first lane (corner wall), a second leg with a 1.4 m run-up, the ramp, a **0.6 m gap** (was 0.3), and the 1.6 m bowl. `JumpBowlSpec` gained `turn` / `turnZ` (the ramp frame is turned to run along hole +x); `HoleFactory` faces a composite hole down its first lane.
- **Corner sweep (second shot from the corner):** 2.4 rolls back; 3.0-3.8 fall short in the pit (+1); 4.2-5.6 land in the bowl. A firm tee putt hits the corner wall and rebounds up the drop (no penalty).
- **Tiki crest:** the feathers radiated from one point and cut through each other; now a layered fan on a root band, and the shield diamond is thinner than the board (no shared face, no shimmer). `Tools/Blender/hero_tiki.py`.
- Tests 107/107; scene and both players rebuilt; screenshots checked.

## 2026-10-05: Hole 4 "Tiki Twister" approved in the headset

- Andrew: "Feels good. That's really good." Hole 4 (drop, 90 degree turn, 0.6 m gap ramp, roulette bowl) is confirmed. `RESUME.md` is stale on Hole 4; next up is Temple Island (holes 5-6).

## 2026-10-05: Holes 5 and 6 written in the cloud (UNVERIFIED), branch `feature/holes-5-6-sun-stair-waterwheel-mill`

- **Hole 5 "The Sun Stair" (par 4)** and **Hole 6 "The Waterwheel Mill" (par 3)** on a new Temple Island, built from the proven mechanics: one stepped green with `BankWall` kickers/rails and a launch jump (Hole 5); the proven waterwheel feeding an aqueduct and a final green, with a gentle mill road as the conventional route (Hole 6). New reusable `BankWall`; the wheel hole spec gained an optional final green and a shared `BuildWheelPieces`. Terrace steps are steep cells in one mesh, so a miss lands on a lower terrace (no penalty). `TempleIsland` is bare functional dressing.
- No Unity here: syntax-parsed and type-checked against stubs only; `Holes56Tests` not run. Details, risks and integration steps in `Docs/HOLES_5_6.md`. The brief's "flow zones" do not exist in the repo and were not needed.

## 2026-10-05: Cloud Holes 5-6 integrated locally (Sun Stair, Waterwheel Mill)

- Merged `origin/feature/holes-5-6-sun-stair-waterwheel-mill` (`1617027`) into `milestone-3-island-hopping`. Compiled clean on the first try (0 errors); scene builds with 6 holes and a Temple Island; both players build (0 errors).
- One test fix: `HolesOneToThree_HaveNoOpenEdges_AndHoleFourHasOnlyTheRampLip` asserted zero open edges on every hole except 4; Hole 6 legitimately has the wheel dock's open edge, so it now checks holes 1-3 only.
- Full PlayMode suite **133/133** (Holes56Tests 26, proving ground and Holes 1-4 unchanged). Desktop smoke test PASS. Screenshots of holes 5-6 reviewed (rich). Headset not yet tried on 5-6.
- Not done (out of scope): Holes 7-9, Graphics Pass 3.

## 2026-10-05 (later): Headset verdict on Holes 5-6; Hole 3 is par 4

- Andrew played Holes 5 and 6 in the headset: "They play great. No issues." Only change requested: Hole 3 Jungle Crossing becomes par 4 (done, test updated, 133/133).

## 2026-10-06: Holes 7 and 8 written in the cloud (UNVERIFIED), branch `feature/holes-7-8-lava-falls-caldera-run`

- **Hole 7 "Lava Falls" (par 4)** and **Hole 8 "Caldera Run" (par 4)** on a new Volcanic Island (centre (120, 44)). Hole 7: three tiers, a banked tee putt, a lava-lake causeway, and a new small reusable `VentTransport` (capture, 0.6 s charge, visible 1.6 s ride over a lava river, exit at 2 m/s onto the final green; a hold, no stroke). Hole 8: a crater route (two banks, a 3% climb, a gate lane) rolling into the proven 2 m roulette bowl through a new second curb notch (`RouletteBowlSpec.gate*`), plus an optional launch-ramp jump into the same bowl.
- No Unity here: syntax-parsed and type-checked against stubs only; `Holes78Tests` (43 new, expected total 176) not run. Details, risks, Unity steps and the headset checklist in `Docs/HOLES_7_8.md`. Hole 9, Graphics Pass 3 and Holes 1-6 untouched.

## 2026-10-05 (evening): Cloud Holes 7-8 integrated (Lava Falls, Caldera Run)

- Merged `feature/holes-7-8-lava-falls-caldera-run` (`1accad0`; fast-forward from `825f8e5`). Compiled clean; scene builds with 8 holes and a Volcanic Island; both players build (0 errors); desktop smoke PASS (8-entry scorecard).
- One test fix beyond Cloud's work: `TropicalScene_HasJungleIsland_WithHoleThreeOnABridge` still asserted Hole 3 par 3 (the scene had been stale when par 4 went in); now par 4.
- Full PlayMode suite **176/176** (Holes78Tests 43). Measured: vent captures 3.2-4.0 m/s putts from the causeway foot and rejects fast balls without trapping; ride 2.4 s, exit 1.98 m/s, no extra stroke; Hole 8 gate entry puts every 1.5-4.0 m/s ball in the bowl near the cup; shortcut lands in the bowl from 4.2 m/s, pit (+1) below 3.8, one hole in one at 5.0.
- Headset pending for Holes 7-8. Hole 9 and Graphics Pass 3 not started.

## 2026-10-05 (night): Hole 8 harder shortcut, inner ring

- **Andrew's feedback on Caldera Run:** the tee shortcut was far too easy (an eagle off the tee) and gave no reason to take the long route. Asked for a smaller circle, a harder gap, and then a blocker inside the circle that a short landing hangs up against (another stroke), without affecting the long route's entry.
- **Done:** bowl radius 2.0 -> 1.5, jump gap 0.6 -> 1.0 m (the shortcut now needs about 5.0 m/s; 4.6 and below fall in the pit, +1). New `RouletteBowlSpec.ring*` fields: a low (1.2 cm, under a ball radius) ring at r 0.9 round the cup, open for 110 degrees centred on the gate. A first attempt at a hurdle on the rim shelf did nothing (the jump lands moving mostly inward, not along the shelf); a 2-3 cm ring stopped everything (a step near the ball's radius cannot be rolled over), so 1.2 cm.
- **Measured:** gate entries 1.5-4.0 m/s unchanged (end r 0.13-0.17 from the cup); normal route 4 strokes; shortcut at 5.0/5.6 m/s lands in the bowl but stays outside the ring (r 0.95-1.17), costing another stroke. Tests 177/177 (one new ring test; the normal-route test strikes at least 1.6 m/s when outside the ring).

## 2026-10-06 (later): Hole 9 "Summit Sanctuary" written in the cloud (UNVERIFIED), branch `feature/hole-9-summit-sanctuary`

- Par 5 finale on a new Summit island (50, 80), highest ground of the course. **No bowl** (Andrew: Holes 4 and 8 already use them). Stages: Summit Approach, Sanctuary Climb (east kicker, 5.9 m climb over a waterfall chasm, corner kicker), Hero Shortcut (the Sun Stair's launch jump onto the Overlook, skips the detour), Final Approach (Shrine Lane with a 0.7 m gate, kicker, Landing) and **the Sky Bridge**: a rail-less 3.2 m ridge over an abyss to the cup on the Altar. Only existing mechanics (height function, `BankWall`, launch jump, open edges, OOB boxes); no runtime system changed.
- No Unity here: syntax-parsed and type-checked against stubs only; `Holes9Tests` (24 new, expected total 200) not run. Details, risks, Unity steps and the headset checklist in `Docs/HOLE_9.md`. Graphics Pass 3 not started.

## 2026-10-05 (late night): Cloud Hole 9 integrated (Summit Sanctuary)

- Merged `feature/hole-9-summit-sanctuary` (`40937f2`) onto the local tip (Hole 8 ring included). Compiled clean; scene builds with 9 holes and the Summit island; both players build (0 errors); desktop smoke PASS (9-entry scorecard).
- One test fix: `Hole9_ShrineLane_AGoodPuttThreadsTheGate...` asserted the ball passes the gate by 0.2 m with a 2.1 m/s putt; it rested at +0.19 m (it did pass the gate), so the putt is 2.4 m/s. No gameplay change.
- Full suite **201/201** (24 Holes9Tests + my Hole 8 ring test). Measured: hero jump from Terrace 1 needs 4.0 m/s (3.6 and below fall in the pit, +1; 4.0-5.4 land on the Overlook); Waterfall Climb from its foot: 3.4 m/s rests part-way up, 4.0+ reaches the top; Sky Bridge putts 2.8-3.6 m/s reach the Altar (3.2 holes); tee putts 2.4-3.8 m/s rest on the approach.
- Headset pending for Hole 9. Graphics Pass 3 not started.
