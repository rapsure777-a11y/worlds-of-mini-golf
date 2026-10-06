# Holes 7 and 8: Lava Falls and Caldera Run

Branch `feature/holes-7-8-lava-falls-caldera-run`, from `milestone-3-island-hopping` at `825f8e5`. **Written in the cloud: never compiled or run in Unity, never seen in a headset.** Checked only by tree-sitter syntax parsing, by compiling the runtime code and the new tests against stub Unity assemblies (`Tools/CloudTypeCheck`; the stubs gained a few members), and by the paper simulator (`Tools/DesignSim/h78_check.py`, a 2D model with no flight and no cliffs). Hole 9, Graphics Pass 3 and Holes 1-6 are untouched. `VolcanicIsland.cs` and `TropicalWorld.cs` (Editor, need the art pipeline) were syntax-parsed only, not type-checked.

## What was reused, what is new

Reused unchanged in behaviour: `LaunchRamp`/`JumpBowlSpec` + `ProvingGround.BuildJumpBowlPieces` (ramp, open lip, pit, the bowl with its jump notch), `RouletteBowl` (no forces, no `Update`), `BankWall`, open edges, `GolfBall` hold/release and the stuck-ball guard, `OutOfBoundsSurface` (lava), the `buildExtras` hook.

| Piece | File | What it is |
|---|---|---|
| `VentTransport` (+ `VentTransportSpec`) | `Runtime/Obstacles/VentTransport.cs` | **The one new reusable component.** Intake -> captured -> short charge -> ride along a visible path -> exits with real velocity. A *hold* like the waterwheel, not simulated physics. About 250 lines; no geometry of its own; static `PathPoint`/`Ease`/`Smooth` are the testable maths. |
| `RouletteBowlSpec.gateAngleDegrees/gateHalfWidthDegrees/InNotch/NotchCurbRise` | `Runtime/Obstacles/RouletteBowl.cs` | A **second low-curb notch** for a ball that arrives by *rolling*. Same curb as the jump notch (2 cm above the rim shelf). Defaults leave every existing bowl byte-for-byte unchanged (gate width 0). |
| `LavaFallsSpec` | `Runtime/Obstacles/LavaFallsSpec.cs` | Hole 7: layout, height function, banks, lava boxes, vent pieces, validation. |
| `CalderaRunSpec` | `Runtime/Obstacles/CalderaRunSpec.cs` | Hole 8: the proven `JumpBowlSpec` plus the crater route, the gate lane and its stone approach. |
| `VolcanicIsland` | `Editor/Art/VolcanicIsland.cs` | Functional dressing only (see below). |
| `ProvingMaterials`, `WorldTheme` | untouched | Lava is re-skinned by the scene builder (boxes under a `Lava` parent), so no new theme fields. |

## Hole 7, Lava Falls (par 4)

```
 top-down (z up)                                         side view (x = 2.4 line; not to scale)
   +--------+  T3 final green (3.4 x 5.6 m), cup (0.4,18.6)                        ____ T3  0.80 m
   | cup  ^ |  <- ball leaves the cannon (2.4,14.5) at 2 m/s, +z          ,-----''     (chute rides ~1.2 m up)
   +--+  ^ -+     Lava Falls cliff + pool on the east / west edge        ,'
   ~~~~~ LAVA RIVER (4 m) ~~~~~   chute (visible, over the river)      ,'  mouth (dish, 0.045 deep)
   +--------+  T2 pad, mouth (2.4,9.3) in a dish, back wall at z 10        |__ T2 0.24 m
   |  <-(o) |  open SW corner -> lava lake                           ramp 7.5% ./
   |  causeway 1.2 m wide, 3.2 m run, 7.5% climb, OPEN west side -> lava lake
   +----+---+----------+  cross lane z 2..4 with a 45 degree bank at its west end
   |tee |   |bank/     |  tee pad z 0..2, tee (0,0.6)
```

* **Tier 1:** tee pad, a 45 degree bank across the whole pad width that turns a tee putt east along the cross lane (paper: a 2.2 m/s tee putt rests mid-lane near (0.75, 3.3); 2.8-3.4 m/s ends at the causeway foot, x 2.2-2.5), then a **causeway** (3.2 m, 7.5%: a ball can rest on it) beside a **lava lake** with an open edge.
* **Tier 2** (0.24 m up): flat pad with the vent mouth in a 0.45 m dish (10% slope, steeper than the rest-hold limit, so a ball can only come to rest at the mouth), 0.7 m in front of the back wall. The pad's SW corner is open to the lava.
* **Vent** (`VentTransport`): (1) a ball within 0.15 m of the mouth and slower than 2.4 m/s is captured; (2) it settles into the mouth (0.2 s); (3) the vent charges (0.6 s); (4) the ball rides a visible open chute up and over the lava river (1.6 s, eased); (5) it leaves the cannon on Tier 3 at 2.0 m/s straight up the green (it rolls about 3 m); (6) ordinary physics resumes; (7) no stroke, no `Struck`/`Stopped` event. Total 2.4 s. 1.5 s recapture lockout after an exit, 12 s safety timeout. A ball that arrives too fast is not captured; it rebounds off the back wall and passes again slower.
* **Tier 3** (0.80 m up): the final green, a real putting section. The exit rests the ball about 2.3 m from the cup, off its line (it cannot hole out from the exit). Lava along the green's west edge near the cup.
* **Hazards and recovery:** every lava edge is a plain open edge (no rail) over an out-of-bounds lava box: +1 stroke, ball back at its last rest spot. A reset or hole change during the ride cancels the hold cleanly. Nothing can trap the ball.
* Paper numbers: causeway from its foot needs about 3.2-3.8 m/s (a softer putt stays on the ramp, restable); from the tee a 2.2 m/s putt rests near (0.75, 3.3).

## Hole 8, Caldera Run (par 4)

```
 top-down (z up)
                    +--- gate lane (x -2.4..-1.3, z 5.4..6.2, flat, level with the curb) ===> [stone approach] ==> BOWL gate (NW)
    north bank '/'  |                                                                           r = 2.0 m, centre (0.8, 5.03)
   +---+--------+   |   climb (3.4 m, 0.11 m rise, 3%)  columns x -3.4..-2.4                    jump notch (SW) <- lands here
   |   |  ^     |   |                                                                           ^
   |   |  ^     |   |   crater floor z 0.5..1.5 <== tee putt WEST, south bank '\' turns it north  |  pit (gap 0.6 m, OOB)
   +---+--------+---+-------+  tee lane x -0.6..0.6 (z 0..2.6) -> launch ramp 14 deg (lip z 2.6) -+  (shortcut: tee putt NORTH)
```

* **Normal route:** tee putt west along the crater floor; the south bank turns the ball north up a 3.4 m climb (a 0.11 m rise, 3%, restable); the north bank turns it east into the gate lane (flat, 1.1 m); the lane ends at a flush **stone approach** (a flat mesh from the lane's end to the bowl's curb arc, with side rails) so the ball rolls across the 2 cm curb and steps down onto the rim shelf of the **2 m roulette bowl**. The gate is offset 0.77 m from the bowl's centre (about 23 degrees off radial, the same as the jump entry) and curves the same way round (clockwise), so the ball enters on a chord and orbits. Nothing pulls it in: the bowl has no forces. Paper (rolling into the gate, 400 balls per speed): 1.2-1.6 m/s rest on the apron (tap-in), 2.0-2.5 m/s mix of apron and a real putt (up to 31% hole out at 2.5), 3.5-4.0 m/s mostly longer putts (14-41% off the apron), none leave the bowl.
* **Shortcut:** a firm tee putt north (about 4.4 m/s) leaps the 0.6 m gap from the proven 14 degree ramp and lands on the rim shelf through the jump notch, tangentially. Fails: soft = rolls back down the ramp (no penalty); short = the pit (+1 stroke, back to the last rest spot); a ball in the bowl cannot roll out of either notch (the 2 cm curb).
* **Bowl:** proven physics, unchanged (cone 10%, rim shelf 6.5% restable, 0.3 m apron). Normal cup; the cup is the bowl's lowest point.
* **Strokes (paper):** tee west + one firm putt up the climb + one tap-in = 3 for a good player; typical 4-5; shortcut 2-3.

## World changes

* `TropicalCourse`: `Hole07`, `Hole08`, `Hole7Spec()`, `Hole8Spec()`, `VolcanicCluster` and `VolcanicCentre` (120, 44), radius 26; hole origins (114, 2.4, 32) and (129, 2.6, 40).
* `TropicalWorld.Build`: a fourth island (`VolcanicIsland.CreateIsland`, terrain `IslandTerrain4.asset`, basalt material through the new optional `IslandGen.terrainMaterial`), plateaus under both holes, dressing cases 7 and 8. **One horizon change:** the distant island at bearing 30 moved to 75 (the Volcanic Island is at about 128 m on the 20 degree bearing).
* Dressing (functional only, no colliders, no particles, no vegetation): dark basalt terrain, masonry under the tiers/causeway/climb, the hole's lava boxes re-skinned as emissive lava, a lava-falls cliff beside Tier 3, a crater rim of blocks round Hole 8, a few spires, torches, a few warm point lights (no shadows), hole signs.

## Files

New: `Runtime/Obstacles/{VentTransport,LavaFallsSpec,CalderaRunSpec}.cs`, `Editor/Art/VolcanicIsland.cs` (+ `.meta` for each), `Tests/PlayMode/Holes78Tests.cs` (+ `.meta`), `Tools/DesignSim/h78_check.py`, this file.
Edited: `Runtime/Obstacles/RouletteBowl.cs` (gate notch; behaviour unchanged when unused), `Runtime/Course/HoleDefinition.cs` (holes 7-8, volcanic cluster data), `Editor/Art/TropicalWorld.cs`, `Editor/Art/IslandGen.cs` (volcanic island and dressing wiring, `terrainMaterial`), `Tools/CloudTypeCheck/*` (new files and stub members), `DEVELOPMENT_LOG.md`.
Not touched: `GolfBall`, `HoleController`, `CourseGeometry`, `HoleFactory`, `ProvingGround`, `LaunchRamp`, `BankWall`, Holes 1-6.

## Tests (`Holes78Tests`, 43 new; expected Unity total 133 + 43 = **176**)

Deterministic (22): Volcanic cluster data and island fit; holes 1-8 consecutive, 7 and 8 do not overlap; Hole 7 definition, three tiers and a restable causeway, the mouth dish, lava placement and the river, the bend bank, the ride path, the exit roll-out maths; `VentTransport` path maths, easing, spline and timing; bowl `InNotch` regression and curb counts; Hole 8 definition, gate geometry (flush, tangential, same orbit sense, separate from the jump notch), route heights, banks, approach mesh, gate rails, extra areas.
Physics (21, measured, with logged outcome tables): vent capture/carry/release with no extra stroke and the phase order; exit velocity and roll-out; a fast ball is rejected and never trapped; reset during the ride; the safety timeout; the recapture lockout; Hole 7 tee putts, the causeway, both lava edges (penalty and return), the full tee-to-cup path, the cup; Hole 8 west tee putts, the climb holds a ball, rolling in through the gate across 7 speeds (the bowl keeps the ball), no invisible force, a ball struck back out through the gate stays in, the cup, the full normal route to the cup, the shortcut across 7 speeds, a failed jump leaves the route open.
`TropicalScene_ProgressesThroughAllHolesAndFinishes` (existing, scene-based) will cover holes 7 and 8 once the scene is rebuilt; both cups have a flat apron 0.4 m behind them along local -z.

## Cloud-side checks performed (not Unity verification)

Tree-sitter syntax parse of every new and edited file (0 problems); `Tools/CloudTypeCheck` `RuntimeAndTests` and `Editor` projects build (0 errors); paper numbers with `Tools/DesignSim/h78_check.py` (gate entry, tee bank, causeway, route); closed-form flight and roll-out maths. Nothing here ran a physics step.

## Unity-side steps for Local Claude

1. `git fetch origin feature/holes-7-8-lava-falls-caldera-run && git checkout` it. Let Unity import (4 new scripts + 1 test, all with `.meta`).
2. Expect a clean compile. Likeliest trouble: `VolcanicIsland.cs` (Editor, only syntax-parsed) and the `TropicalWorld.cs` wiring.
3. Run `Holes78Tests`, then the full suite. Read the logged tables (`[Test] hole 7 tee putt`, `... causeway`, `... hole 8 rolling into the bowl`, `... shortcut`).
4. Rebuild the Tropical scene. Expect 8 holes, a Volcanic Island (`IslandTerrain4`, `Volcanic_*` materials under `Worlds/Tropical/Kit/Materials`) and watch `TropicalScene_NoSceneryOnGreens`, `..._ProgressesThroughAllHolesAndFinishes`, `Clusters_*`.
5. Look at screenshots of both holes, then build both players for the headset.

## Risks Local should verify

1. **Never compiled.** See above.
2. **Vent capture window.** Capture radius 0.15 m, 2.4 m/s; the dish funnels slow balls. If a straight putt up the causeway misses too often, widen `dishRadius`/`captureRadius` in `LavaFallsSpec`/`NewVent()`.
3. **Causeway difficulty.** From its foot a ball needs about 3.2-3.8 m/s (a 3.2 m climb of 0.24 m plus 2 m of pad). Lower `tier2Rise` if casual players struggle; keep the slope under 7.85%.
4. **Open lava edges** on the causeway and Tier 2's corner are the main penalty source; check they read in the headset (the lava must be visibly lower and glowing). If too harsh, add a 3 cm kerb.
5. **Exit placement.** `EndHold` after a manual `Body.position` set (a pending `MovePosition` is lost when the body turns dynamic). Confirm the ball leaves the cannon cleanly and does not clip the cheek stones (decorative, no colliders).
6. **Gate seam.** The lane, the stone approach mesh and the curb are three separate colliders meeting flush at the gate level (the curb top is 2 cm above the rim). Check a slow ball (1.0 m/s) does not stall on the approach and that nothing snags at the lane end; the test table logs it. The approach and the bowl wall meet 2.5 degrees apart: check there is no visible or physical gap.
7. **Containment through the gate.** A ball struck back out through the gate relies on the same 2 cm curb as the jump notch.
8. **Tap-in rate.** Slow gate entries settle on the 0.3 m apron (the proven bowl's behaviour, paper 100% at 1.2-1.6 m/s). If the normal route feels too easy, flatten the cone slightly (`coneSlope` 0.09) or make the climb longer; do not add forces.
9. **Shortcut window.** Paper 3.4-3.7 m/s at the lip; the test logs the real tee-putt window (expect about 4.0-5.0). Casual players cap near 3.8 m/s: if the jump is out of reach, shorten the gap (0.6 -> 0.5) in `CalderaRunSpec.NewJump`.
10. **Island and horizon** placement are untested visually.
11. **Player comfort** on Tier 3 (0.8 m up) and while the ball rides 1.2 m above the river.

## Steam Frame checklist for Andrew

- Hole 7: does the tee bank send the ball east? Can you putt up the causeway without feeling it is a lottery? Does the mouth look like something that takes the ball? Watch the ride: is it clear where the ball went and where it comes out? Is 2.4 s too long or too short? Does the ball leave the cannon with believable speed? Is the final green a satisfying putt (lava on the west edge)?
- Hole 7 misses: roll off the causeway into the lava lake (stroke + back where you hit from); hit the mouth too hard (it should rebound, not get stuck).
- Hole 8: does the west route read (bank, climb, bank, gate lane)? Does the ball roll into the bowl and orbit? Is the cup a tap-in too often, or still a real putt sometimes?
- Hole 8 shortcut: can you find the ramp, and is the jump exciting but fair? What does a miss feel like (pit, penalty, back to the tee)? Does the landing orbit?
- Anything that snags, stalls or looks like the ball teleported. Any spot you can get stuck in.
