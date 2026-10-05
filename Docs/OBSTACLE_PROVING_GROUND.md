# Obstacle Proving Ground

Branch `feature/obstacle-proving-ground` (from `ab5ccab`, the head of `milestone-3-island-hopping`). **Not merged. Not compiled or run in Unity** (the cloud has no Unity). Everything below was type-checked and syntax-checked against stubs (see §8) and sized with the design simulator, nothing more. Success is how these feel in VR; the first job of integration is to find out.

Three reusable mechanics, each a small modular component with tunable numbers, a demonstration hole built by the real hole code, and PlayMode tests:

| | Mechanic | Component | Demo hole |
|---|---|---|---|
| A | Launch ramp: open rail edge, real airborne arc, forgiving landing | `LaunchRamp` | Proving 1 |
| B | Waterwheel carrier: bucket scoops, lifts and releases the ball | `WaterwheelCarrier` | Proving 2 |
| C | Roulette bowl: sloped round bowl, cup at the lowest point | `RouletteBowl` | Proving 3 |

Holes 1–4, the Tropical scene, the build settings, shaders, materials, terrain and Graphics Pass 3 are untouched (§6). Holes 5–9 are not started.

---

## 1. Integration steps for Local Claude

1. `git fetch origin feature/obstacle-proving-ground && git checkout feature/obstacle-proving-ground`.
2. Open the project. Unity imports 5 new runtime scripts, 1 editor script, 1 test file (all with `.meta` files and fixed GUIDs). **Expect a clean compile.** If anything fails, the most likely spots are the three edited core files (§6); tell me the exact error.
3. Run PlayMode tests (SteamVR and the game closed): the new fixture is `Gamebreak.MiniGolf.Tests.ObstacleProvingGroundTests` (26 tests). Then the full suite to confirm the previous 78/78 still passes. Read the **logged tables** (`[Test] launch sweep`, `[Test] bowl strikes`...): they are the real windows and are more useful than pass/fail.
4. Build the demo scene: menu **Gamebreak > Proving Ground > Build Scene (Default tuning)** (or Easy / Hard), or batch:
   `Unity.exe -batchmode -quit -projectPath <repo> -executeMethod Gamebreak.MiniGolf.Editor.ProvingGroundSceneBuilder.BuildDefaultBatch [-provingPreset Easy|Default|Hard]`.
   It writes `Assets/_Game/Worlds/ProvingGround/ObstacleProvingGround.unity` and materials beside it. It reuses `TropicalTheme.asset` materials if that asset exists and never touches the Tropical scene or the build settings.
5. Try it: press Play in the editor (desktop rig), or **Gamebreak > Proving Ground > Build Windows Player (PCVR, proving ground only)** -> `Builds/ProvingGround/ObstacleProvingGround.exe`, SteamVR streaming first, one streaming session only. The three holes play in order (the course advances after each hole); the scorecard's *Restart Hole* repeats one.
6. Tune with the knobs in §3, rebuild the scene, repeat. Report the numbers that felt right and I will fold them into the defaults.

The demo scene deliberately has no quality preset, post volume, music or title cards. Players stand on a floating tee area over a distant ground slab at y = -0.65 (touching it is out of bounds).

---

## 2. What was built

### Shared plumbing (small, additive)

- **`GolfBall` hold API**: `TryHold(holder)`, `MoveHeld(pos)`, `EndHold(velocity)`, `IsHeld`, `Holder`. A held ball is kinematic, the rolling model pauses, `Strike` is ignored, no `Struck`/`Stopped` event fires. `PlaceAt` (reset, out-of-bounds return, hole change) always cancels a hold and restores the body. Continuous-dynamic detection is swapped to discrete while held (not valid on a kinematic body) and restored on release.
- **`HoleController`**: one line, so the 20 s stuck-ball safeguard ignores a held ball.
- **`GreenLayout.openEdges`**: thin rectangles marking boundary edges that get a plain vertical skirt instead of a rail. Empty by default, so every existing hole's mesh is identical.
- `ProvingKit` (boxes with the zero-friction course material, flag, hole wrapper), `ProvingGround` (builds the demo holes with the real `HoleController`/`Cup`/`CourseGeometry`; used by both the scene builder and the tests, so what is tested is what is demonstrated).

### A. Launch ramp

Layout (hole space, +z along the lane): tee, flat run-up, ramp rising at `rampAngleDegrees`, **open lip with no rail** (`openEdges`), a short gap, then a landing platform shaped as a shallow dish around the cup, with a low 2 cm lip on its open near edge (lower than a landing arc, so flights clear it, but it stops a ball rolling back into the gap). Under the gap is a pit floor tagged `OutOfBoundsSurface`: falling short costs the usual one stroke and returns the ball to its last rest spot. **The jump is the ball's own Rigidbody physics**: the existing `GolfBall` already keeps separating velocity off a ramp and applies only gravity when ungrounded; nothing is scripted and `GolfBall` physics is unchanged.

Sizing (frictionless flight maths with the real rolling model; hole 1 sim does not model flight): with the defaults (14°, 0.6 m run, 0.3 m gap, pad 6 cm below the lip, tee 1 m before the ramp) a strike of about **3.0 m/s and up lands**, 2.4 to 2.9 falls in the gap, 2.0 and below rolls back, and anything up to at least 5 m/s should still land on the 2 m platform.

### B. Waterwheel carrier

A wheel (plane = carrier XY, axle = Z) with N buckets always turning at a fixed rate; it never waits for the ball. Each physics step it checks the ball against the bucket pockets:

- **Capture**: ball centre within `captureRadius` of a pocket **and** relative speed under `maxCaptureSpeed` -> `TryHold`. The ball eases into the pocket over `captureBlendSeconds`. A ball that is too fast or has no bucket in reach is simply not captured and stays an ordinary rolling ball (it bounces off the dock backstop).
- **Carry**: the held ball follows its pocket. No stroke, no stop event, no out-of-bounds (it never touches anything).
- **Release**: when the bucket reaches `releaseAngleDegrees`, `EndHold(releaseDirection * releaseSpeed)` hands the ball back to physics on the elevated channel. A `maxHoldSeconds` safety net releases a ball held too long.
- **Abandoned or repeated attempts**: reset, an out-of-bounds return and a hole change all go through `PlaceAt`, which cancels the hold; the carrier notices on its next step and forgets the ball. Disabling the carrier releases a held ball.

Demo layout: a 0.7 m intake lane ends in a dock under the axle (ball rests there until a bucket comes); the lane's last metre uses the normal rails, so the dock is self-contained. The elevated exit channel starts just past the wheel, slopes down 9%, and ends at a cup, so a ball released at 0.9 m/s rolls to it. Lift is about 0.9 m. The wheel is decorative (no colliders).

### C. Roulette bowl

A radial surface mesh with a flat apron round the cup (the lowest point, cup pit cut into the mesh like a normal cup), a **cone** sloping up to a gentler **rim shelf**, and a circular wall of boxes. **No Update, no FixedUpdate, no forces, no attraction.** The cone is above the ball's rest-hold slope (about 7.85%), so a ball can never rest on it; the shelf is below it, so a ball can rest there to be struck again. The tee is on the shelf: strike roughly along the wall and the ball enters the cone, orbits, loses speed through the ordinary rolling model and settles toward the cup.

Design simulator (assumed player scatter, no lip-outs, indicative only): a cone of 10% with a 6.5% shelf, 2.0 m radius, gives roughly 50 to 60% holed for gentle to medium strikes (1.5 to 2.5 m/s), the rest ending on the apron or the shelf; hard strikes (4 m/s) mostly end on the shelf. **It is very sensitive to the shelf slope** (5% almost never holes, 7% holes 65 to 99%), which is why both slopes are tunable and the Easy/Hard presets differ only there.

---

## 3. Tunable parameters

All are public serialized fields (Inspector) on the component's spec. Geometry specs have a **Rebuild** context-menu item (edit mode; in play mode the hole re-begins). Capture/release/timing fields of the waterwheel apply live.

### `LaunchRampSpec`
| Field | Default | Effect / guidance |
|---|---|---|
| `approachLength` | 1.4 | Flat lane before the ramp. Tee is `teeZ` (0.4) from the back, so the run-up is about 1 m. |
| `width` | 0.8 | Lane/ramp/lip width. |
| `rampRun`, `rampAngleDegrees` | 0.6, 14 | Steeper hops higher but costs speed (needs a harder putt); flatter flies flatter and farther. 10 to 20 is the useful range. |
| `gap` | 0.3 | Shorter = more forgiving. Easy 0.2, Hard 0.5. |
| `padDrop` | 0.06 | How far the platform's near edge is below the lip. More drop = longer flight, more forgiving. |
| `padWidth`, `padLength` | 1.6, 2.0 | Landing platform. |
| `cupFromPadFront`, `dishSlope`, `cupApron` | 1.1, 0.09, 0.30 | Dish round the cup. Keep `dishSlope` above 0.08 so a ball cannot rest on it. |
| `padLipHeight` | 0.02 | Low lip stopping rollbacks. Raise if balls roll back into the gap; lower if landings catch it. |
| `gapDepth`, `railHeight` | 0.30, 0.12 | Pit depth; rail height. |

### `WaterwheelCarrierSpec` (+ `WaterwheelHoleSpec` for the lane)
| Field | Default | Effect / guidance |
|---|---|---|
| `periodSeconds`, `bucketCount` | 14, 4 | A bucket every 3.5 s. Faster wheel = shorter wait but harder to feel. |
| `captureRadius` | 0.11 | Bigger = easier scoop. |
| `maxCaptureSpeed` | 1.6 | Relative speed limit. Raise to accept harder putts. |
| `captureBlendSeconds` | 0.15 | How the ball eases into the bucket. |
| `releaseAngleDegrees` | 50 | Where the bucket tips out. Lift is `pocketRadius (1 + sin angle)`. If you change it, the exit channel must move too (the scene builder derives it; `Validate()` flags a bad combination). |
| `releaseSpeed`, `releaseDirectionLocal` | 0.9, (1,-0.08,0) | Roll-off speed. Below about 0.3 the ball will not leave the bucket. |
| `pocketRadius`, `wheelHalfWidth` | 0.5, 0.45 | Wheel size. |
| (hole) `exitSlope`, `exitLength`, `wheelZ` | 0.09, 2.8, 3.0 | Exit channel and wheel position. Keep `exitSlope` above about 0.08 so the ball keeps rolling. |

### `RouletteBowlSpec`
| Field | Default | Effect / guidance |
|---|---|---|
| `radius`, `shelfWidth`, `apronRadius` | 2.0, 0.5, 0.30 | Size. Under about 1.8 m the sim never holed (the ball stops on the apron). |
| `coneSlope` | 0.10 | Keep above 0.0785 (no rest on the cone). Higher = faster, more automatic. |
| `shelfSlope` | 0.065 | **The main feel knob.** Below 0.0785 a ball can rest on the shelf; nearer 0.0785 more strikes run in and hole. Easy 0.07, Hard 0.06. |
| `wallHeight`, `segments`, `teeAngleDegrees` | 0.14, 96, -90 | Wall, roundness, tee position. |

---

## 4. Tests (`ObstacleProvingGroundTests`, 26)

| Area | What is asserted |
|---|---|
| Regression | Holes 1–4 have no open edges. An open edge has no rail at the lip but keeps a skirt (and a closed edge does have the rail). Every preset spec is self-consistent (slopes above/below the rest-hold limit, valid release geometry). |
| Hold API | Freezes the ball, no stroke, no `Stopped` event, strikes ignored, releases with the requested velocity and then ordinary rolling; `PlaceAt` cancels; the 20 s stuck-ball safeguard does not take a held ball; an out-of-play ball cannot be held. |
| Launch | The ball is airborne for a real flight (>= 12 steps); **vertical motion follows -g (second difference of height) and horizontal speed is constant**; the apex clears the lip; it lands on the pad with no penalty. A strike-speed sweep: soft rolls back, short falls in the gap, firm lands, a landing window at least 1 m/s wide, and falling short only happens below the landing speeds. A short jump returns to the last rest spot with exactly one penalty. Repeated attempts keep the ball state sane. Ramp angle is tunable (20° hops visibly higher than 10°). |
| Waterwheel | Capture -> carry (height gained, still held, 1 stroke, in play) -> release at the tip-out point (ball ordinary physics, 1 stroke, **zero out-of-bounds returns**) -> holed or resting on the channel. A too-fast ball is not captured and a second attempt succeeds. A reset while held cancels the hold at once, adds no penalty, and the next attempt works. Out-of-bounds while held penalises exactly once and returns the ball. Period is tunable (carry time scales with it). |
| Roulette bowl | The cup is the lowest point and the surface never dips below the rim. A ball placed on the cone rolls down (it cannot rest there). A ball on the rim shelf rests (it can be re-struck). Eight strikes: **mechanical energy never rises** (nothing pulls the ball), none comes to rest on the cone, and some end holed or on the apron. The bowl implements no Update/FixedUpdate/trigger/collision methods and has no rigidbody. Presets build with a cup and a tee on the shelf. |

Outcome-sensitive tests measure the real ball and assert relationships, so a retune does not break them; their logged tables show the real windows. The thresholds I could not verify (the first thing to check if a test fails): the 1 m/s landing window, the launch gap/landing speeds, and that a strike at 2.0 m/s reaches the waterwheel dock.

---

## 5. What to look for in the headset (and what to turn)

**Launch ramp**: Does a firm putt feel like launching, with a clear arc you can see and follow? Is the landing satisfying rather than fiddly? Too hard to clear: shorten `gap`, add `padDrop`, flatten the angle. Too easy / no drama: widen the gap, steepen the ramp. If the ball catches at the lip or the platform edge: check `padLipHeight` and the rail at the pad sides first.

**Waterwheel**: Is the scoop readable (does the bucket visibly take the ball)? Is the ride long enough to enjoy and short enough not to bore (period)? Does it feel fair when you miss (ball sits in the dock and waits)? Is the exit satisfying? If the ball visibly snaps into the bucket: raise `captureBlendSeconds`. If you cannot get it in: raise `captureRadius` / `maxCaptureSpeed`, lower the period.

**Roulette bowl**: Does the ball orbit and slow naturally? Does it ever feel magnetic or too automatic? Mostly holing: lower `shelfSlope` and `coneSlope`. Never holing: raise `shelfSlope` toward 0.07. Ball resting somewhere surprising: check the logged table for a "DEAD ZONE" (it should be impossible).

Also note: the **player's reach**: the waterwheel channel is 0.9 m up; the rig lifts to ball height on teleport (`TeleportToBall`). Judge whether playing from up there is comfortable.

---

## 6. Changes to existing files (exhaustive)

| File | Change | Effect on Holes 1–4 |
|---|---|---|
| `Core/GolfBall.cs` | Added `IsHeld`, `Holder`, `TryHold`, `MoveHeld`, `EndHold`; `Strike` returns early when held; `PlaceAt` cancels a hold; `ForceStop` ignores a held ball. | None: all new paths are gated on `IsHeld`, which is false unless a carrier sets it. The rolling physics is untouched. |
| `Core/HoleController.cs` | `FixedUpdate`: `if (ball.IsHeld) { m_MovingTime = 0f; return; }` after the kill-plane check. | None unless a ball is held. |
| `Course/CourseGeometry.cs` | `GreenLayout.openEdges`; `BuildWalls` calls `EdgeOrSkirt` instead of `Edge`. | `openEdges` is empty for every existing hole, so `EdgeOrSkirt` always calls `Edge`: identical meshes. `HoleDefinition.cs` is unchanged; a test asserts no existing hole uses open edges. |

Everything else is new: `Runtime/Obstacles/*` (5 files + metas), `Editor/ProvingGroundSceneBuilder.cs`, `Tests/PlayMode/ObstacleProvingGroundTests.cs`, `Tools/CloudTypeCheck/*`, `Tools/DesignSim/roulette2.py`, this document.

A correction to the earlier sprint notes: the fixed timestep is **120 Hz** (`GolfTuning.physicsRate`, applied by `GolfPhysicsBootstrap`), not the 50 Hz project default. Flight and roulette numbers above use 120 Hz.

---

## 7. Known risks and open questions

1. **First Unity compile.** Never compiled in Unity. Type-checked against stubs only (§8), so a real-API difference could still bite.
2. **The lip.** A ball rolling off a convex mesh edge goes through PhysX edge contacts. `maxContactLift` (0.5 m/s) bounds a kick, but this is the most likely place for an unexpected hop or catch. The flight test measures it directly.
3. **Landing window.** Sized with my flight model, which ignores the small speed loss at the flat-to-ramp kink (about 3%). The sweep test logs the real window.
4. **Dock window.** A strike that stops the ball between about 2.7 and 3.1 m down the lane gets scooped (about 1.7 to 2.1 m/s), which is a narrow-ish window for a casual player; a short ball just needs a second tap. If it feels fussy, lengthen the dock (more rest room under the bucket path) or raise `captureRadius`.
5. **Roulette sensitivity** (see §2C): small slope changes flip between "never holes" and "always holes". Tune by feel.
6. **Held ball visuals.** The ball does not roll visibly while held (kinematic). Judge whether it reads well; a fake spin could be added to the ball visual if needed.
7. **Out-of-bounds while held** waits `returnDelay` (0.6 s) holding the ball in the bucket before returning it, by design.
8. **Decorative clipping.** The wheel's rims and the intake rails/floor clip at the dock. Cosmetic; Graphics Pass 3 would fix it.
9. Integration into real holes later needs: `openEdges` in the hole's layout (jumps), a carrier placed over a dock (wheel), and a radial bowl as a hole's green (the bowl builds its own mesh and cup; `HoleFactory` does not know about it).

---

## 8. How this was checked in the cloud (and what that does not prove)

- Every new C# file was parsed with tree-sitter (0 syntax errors).
- `Tools/CloudTypeCheck/` compiles the new runtime code, the tests (against real NUnit) and the editor builder with .NET 8 against hand-written Unity stubs, with the project's own classes (`GolfBall`, `HoleController`, `Cup`, `CourseGeometry`, `HoleFactory`...) compiled for real. All three build clean. That checks names, signatures and syntax, not behaviour, and a stub can be laxer than Unity.
- Design simulator (`Tools/DesignSim/roulette2.py`) for the bowl; a closed-form flight model (and the simulator's rolling model for the dock) for the jump and the dock. These informed defaults only.
- No test was executed. No scene was built. Nothing was seen in a headset.

Stop for Local Claude integration. Do not merge into `milestone-3-island-hopping` until the feel is judged.

---

## 9. Local integration results (2026-10-04, real Unity 6000.3.9f1)

- **Compile:** clean on first import (no errors in the three edited core files or the new code).
- **Tests:** `ObstacleProvingGroundTests` 24/26 on the first run, **26/26 after one fix**. Full suite **104/104** (the previous 78 plus 26). Holes 1-4 unchanged.
- **Fix (genuine integration bug), `WaterwheelCarrier`:** on release the ball was still inside its old bucket's `captureRadius` and slower than `maxCaptureSpeed`, so the very next physics step scooped it back up (re-held three steps after release, so the ball was carried round again). Added a 1.5 s re-capture lockout after a release (`RecaptureLockoutSeconds`). No tuning values were changed.
- **Logged windows (Default tuning):**
  - Launch ramp: rolls back at 1.6-2.0 m/s, falls in the gap at 2.4-2.8, **lands from 3.2 m/s up to at least 5.2** (landing window wider than 2 m/s). Short jump: 2 strokes, returns to the tee. 20 deg ramp hops 5.5 cm over the lip vs 1.7 cm at 10 deg (4.2 m/s).
  - Waterwheel: capture, carry of 6.2 s at the 14 s period (3.2 s at 7 s), release onto the channel, holed in 1 stroke, 0 out-of-bounds returns.
  - Roulette bowl (shelf 0.065, cone 0.10): 4 of 8 strikes holed, 2 on the apron, 2 on the shelf, none on the cone, max energy gain 0.000 J/kg (no attraction). At 1.5 m/s a strike 4 or 8 degrees off the wall holes; 0 degrees ends on the shelf.
- **Builds:** scene `Assets/_Game/Worlds/ProvingGround/ObstacleProvingGround.unity` (Default tuning); player `Builds/ProvingGround/ObstacleProvingGround.exe` (PCVR, 0 errors).
- **Not yet verified:** anything in the headset (flight look, the lip, the scoop, how the bowl feels, best shelf slope, rig height at the wheel channel). Runtime behaviour above is from PlayMode tests only.

### 9b. First headset session (Andrew, 2026-10-04) and the waterwheel rework

**Feedback:** (1) everything looked far too high; (2) the mechanics are great but should be *features inside* holes, and the roulette bowl belongs at the *end of a course*, not as a whole hole; (3) the waterwheel buckets did not look like the ball could get in, and parts clipped.

- **Height:** the session log shows eye height 0.97 m and the putter auto-sized to 0.66 m, so SteamVR's floor was calibrated while seated (the world sits about 0.7 m too high). Not a game bug: stand up, reset the floor/recenter in SteamVR, then launch. No code change.
- **Design direction (recorded, nothing built):** launch ramp and waterwheel become obstacles within holes; the roulette bowl becomes the final stage of a course.
- **Waterwheel changes** (no tuning-value changes beyond these):
  - Buckets are now open scoops: the leading wall is omitted (the ball rolls in), a trailing back wall carries it, and side cheeks form the tray.
  - Axle raised by `TrayClearance` (4.5 cm) so the tray floors pass above the dock floor instead of through it.
  - Dock is a wide pad (`dockWidth` 1.3 m, `dockLength` 0.8 m) so the wheel's arms and rims clear the rails; `wheelHalfWidth` 0.45 -> 0.5.
  - The full-height end rail sat right in the buckets' path, so it is gone: the dock end is an open edge closed by a 2.6 cm curb (`DockCurbHeight`: above the ball radius so the ball cannot roll over it, below the trays' lowest edge). Verified in tests: a 4 m/s putt is still stopped by the curb.
  - Tests unchanged: 26/26, full suite 104/104. Still needs a look in the headset.
