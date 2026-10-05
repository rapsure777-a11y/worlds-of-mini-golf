# Holes 5 and 6: The Sun Stair and The Waterwheel Mill

Branch `feature/holes-5-6-sun-stair-waterwheel-mill`, from `milestone-3-island-hopping` at `14b9757`. **Written in the cloud: never compiled or run in Unity, never seen in a headset.** Checked only by tree-sitter syntax parsing, by compiling against stub Unity assemblies (`Tools/CloudTypeCheck`, with the project's own `GolfBall`, `HoleController`, `CourseGeometry`... compiled for real; the stubs now cover the new code and tests), and by the design simulator for the parts it can model (`Tools/DesignSim/h56_check.py`). Holes 7-9, Graphics Pass 3 and the Holes 1-4 code are untouched.

## Status of the mechanics (what was reused, what is new)

Reused as proven, unchanged in behaviour: the waterwheel carrier (`WaterwheelCarrier`, trough dock, curb, wide scoop), `LaunchRamp` flight logic and open edges (`GreenLayout.openEdges`), `GolfBall` hold/release, the stuck-ball guard, the composite-hole hook (`HoleDefinition.buildExtras`, `HoleFactory`).

**Correction:** the brief lists "flow zones" as proven. There is no flow-zone code anywhere in the repo or its history (only the word in `HOLE_CONCEPTS_PASS2.md`). Neither hole needs one: failed jumps and falls end on a lower terrace (a normal playable rest), not in a channel. Nothing was invented for it.

New, reusable and small:

| Piece | File | What it is |
|---|---|---|
| `BankWall` | `Runtime/Obstacles/BankWall.cs` | Angled/sloped wall = one box collider along a segment between two surface points (rail that climbs a ramp, corner kicker, funnel guide). No physics change: the ball's existing wall rebound already uses the real contact normal. Also `FaceNormal` and `Reflect` (the model's rebound) for tests and for choosing kicker angles. |
| `WaterwheelHoleSpec` green + `BuildWheelPieces` | `Runtime/Obstacles/ProvingGround.cs` | The proven wheel hole can now end in a final green (`greenWidth`/`greenLength`; 0 keeps the proving ground's old behaviour exactly), exposes `AddToLayout` so a hole can compose it with its own lanes, and the wheel + curb + supports are built by one shared function (`BuildWheelPieces`) that the proving-ground hole and Hole 6 both call. No duplication. |
| `SunStairSpec` | `Runtime/Obstacles/SunStairSpec.cs` | Hole 5: layout, height function, wall plan, validation. |
| `MillHoleSpec` | `Runtime/Obstacles/MillHoleSpec.cs` | Hole 6: the wheel plus tee pad, mill road, final green. |
| `TempleIsland` | `Editor/Art/TempleIsland.cs` | Functional stage dressing only (no foliage, no polish): plateau terrain, plain stone masonry, a stepped temple mass with a sun disc, torches, hole signs. |

## Hole 5, The Sun Stair (par 4)

One rectangle-union green (5 x 10.3 m) with a height function. Terrace steps are single steep cells (they act as walls), so a ball that rolls off a terrace lands on the terrace below: a setback, never a penalty and never a reset. No pits, no out-of-bounds hazards.

```
 top-down (z up)                              side view (x = -2 lane; not to scale)
   +-----------------------------+                                   lip .44
   | kicker  UPPER (0.36)  kicker|  z 7.1-10.3                       ,-.           _____ upper 0.36 (cup in a sun dish)
   |        ( cup  0.4, 9.0 )    |                                    /   \_gap 0.5_/
   +-----+---------------+-------+                       ramp 1    / jump
   | JMP |  MIDDLE (0.18)| RAMP 2|  z 2.8-7.1             ____,--'' landing   middle 0.18
   | LND |               | up    |                  ___,-'
   |RAMP1|   (kicker >)   |       |            lower 0.0
   +-----+---------------+-------+  z 0-2.8   lower terrace (tee at x -2.0, z 1.2)
```

* **Safe route:** tee -> ramp 1 (left lane) -> the landing on the middle terrace -> a putt across it (the corner kicker at ramp 2's foot turns the ball up the right lane) -> ramp 2 -> upper terrace -> cup. Ramps are 7.5%, under the rest-hold limit, so a ball that stops part-way stays on the ramp and can be struck again.
* **Aggressive route:** from the landing, a firm putt (about 3.6 m/s) up the launch ramp flies a 0.5 m gap onto the upper terrace, skipping ramp 2. A very hard tee putt (about 5.5 m/s) carries through ramp 1, the landing and the jump in one stroke. The upper terrace edge is 8 cm below the lip, and a 2 cm lip on its open edge stops a ball rolling back off it.
* **Failed jump:** the gap floor is the middle terrace itself. A short jump lands there, in play, no penalty.
* **Angled walls:** rails that follow the jump ramp and ramp 2, the ramp 2 corner kicker, and two kickers in the upper terrace's back corners that turn a ball along the edge toward the cup.
* **Destination above you:** terraces rise 0.36 m in total (ramps are limited to what a putt can climb), so the masonry does the rest: stone bodies under the terraces and a three-tier temple behind the cup with a golden sun disc about 1.9 m above the tee level.
* Cup in a shallow 1.2 m sun dish with a 0.5 m flat apron (forgiving, not automatic).

Paper numbers (rolling model, no cliffs modelled; indicative): ramp 1 from the tee needs about 3.1 m/s; the jump from the landing about 3.6-4.0 m/s; ramp 2 from its foot about 3.0 m/s.

## Hole 6, The Waterwheel Mill (par 3)

Tee pad -> 0.7 m intake lane -> trough dock under the wheel -> the proven wheel (6 buckets, 12 s period: a bucket every 2 s, so little waiting) -> 3 m elevated aqueduct sloping down 9% -> a 2 x 3 m final green with the cup. The ride costs no stroke and cannot produce an out-of-bounds (carrier hold, as proven).

* **Mill road (the conventional route):** a 1 m wide, 7.5% ramp beside the lane that climbs to the same green. A ball can rest on it (under the rest-hold limit), so it is climbed in a few ordinary putts (paper: about three 3 m/s putts). A kicker at its top turns a ball onto the flat green instead of letting it rebound down the whole road (a rolling ball runs far downhill on a 7% slope). It costs a few strokes more than the wheel, so the wheel is never a progression blocker.
* **Recovery:** too fast -> the dock curb rebounds it onto the course (no penalty); too short -> it sits in the lane for a second tap; in the dock the trough holds it at the pick-up spot until the next bucket. Reset/out-of-bounds while carried cancels the hold cleanly (proven).
* Visibility: open aqueduct and a visible ball in the bucket; the whole route is in front of the player.

## World changes (so the holes exist in the generated scene)

* `TropicalCourse`: `Hole05`, `Hole06`, `Hole5Spec()`, `Hole6Spec()`, the Temple cluster's centre `(130, -38)` and radius 24, hole origins `(118, 2.0, -46)` and `(141, 2.2, -44)`.
* `TropicalWorld.Build`: a third island (`TempleIsland.CreateIsland`, terrain `IslandTerrain3.asset`), holes 5-6 flattened onto plateaus and dressed, and they are no longer treated as Starting Island holes. **One horizon change:** the distant island at bearing 330 degrees moved to 30 degrees, because the Temple Island now sits at about 135 m on that bearing.
* No pier between the Jungle and Temple islands (the hole transition teleports); dressing is deliberately bare.

## Files changed

New: `Runtime/Obstacles/{BankWall,SunStairSpec,MillHoleSpec}.cs` (+ `.meta`), `Editor/Art/TempleIsland.cs` (+ `.meta`), `Tests/PlayMode/Holes56Tests.cs` (+ `.meta`), `Tools/DesignSim/h56_check.py`, `Docs/HOLES_5_6.md`.
Edited: `Runtime/Obstacles/ProvingGround.cs` (wheel spec green, `AddToLayout`, `BuildWheelPieces`, `Clone`; `BuildWaterwheelHole` now calls it, behaviour unchanged for `greenWidth = 0`), `Runtime/Course/HoleDefinition.cs` (holes 5-6, Temple cluster data), `Editor/Art/TropicalWorld.cs` (temple island and dressing, horizon bearing), `Tools/CloudTypeCheck/*` (new files and stubs).
Core ball/geometry code (`GolfBall`, `HoleController`, `CourseGeometry`) is **not touched** in this branch.

## Tests (`Holes56Tests`, 26)

Deterministic: Temple cluster data; Hole 5 definition, terrace heights, jump geometry, ramp smoothness, wall plan and kicker direction; Hole 6 definition, road gentle/restable/reaches the green, aqueduct downhill and green flat; `BankWall` pose, polyline, and the rebound model (a 45 degree wall leaves at 37.5 degrees from the wall).
Physics (measured, with logged outcome tables): a real ball off a 45 degree wall; Hole 5 ramp 1 window, ramps hold a ball, jump window (lands / falls short with no penalty), real flight, falling off a terrace is penalty-free, ramp 2 kicker, ramp 2 climb, upper kicker, cup holing; Hole 6 wheel route end to end (1 stroke, no out-of-bounds, ends on the green), a hard putt bounces and the next attempt is carried, the road reaches the green in six putts with no penalty, cup holing.

## Unity-side steps for Local Claude

1. `git fetch origin feature/holes-5-6-sun-stair-waterwheel-mill && git checkout` it. Let Unity import (5 new scripts with `.meta`).
2. Expect a clean compile. If not, the likeliest places are `TempleIsland.cs` (the only new file not type-checked, it needs the art pipeline classes) and `TropicalWorld.cs`.
3. Run PlayMode `Holes56Tests`, then the full suite (previous 106/106 plus the new ones; `ObstacleProvingGroundTests` must still pass: the wheel code was refactored).
4. Rebuild the Tropical scene (`Gamebreak > Build Tropical Scene` / `SetupAndBuildAll`). The scene will now have 6 holes, a Temple Island (`IslandTerrain3`, `Temple_*` materials under `Worlds/Tropical/Kit/Materials`) and three scene tests worth watching: `TropicalScene_NoSceneryOnGreens`, `..._ProgressesThroughAllHolesAndFinishes` (it puts the ball 0.4 m before the cup, flat apron on both new cups), `Clusters_*`.
5. Build both players and play Holes 5 and 6 in the headset.

## Known risks that need Unity and the headset

1. **Never compiled.** See above.
2. **Steep-cell terraces.** Terrace steps are one-cell cliffs inside one mesh (a ball is expected to bounce off a face and to fall off an edge). A ball resting *on* the top edge, or contacts at the convex edges, are the places to look. If a cliff misbehaves, the fix is local (a longer riser, or a `BankWall` at the edge).
3. **Jump window and the landing speed** are paper numbers (3.6-4.0 m/s from the landing). The tests log the real window. Casual players cap near 3.8 m/s; if the jump is too hard, shorten the gap or raise `padDrop` in `SunStairSpec`.
4. **Ramp 1 and ramp 2 climb speeds** (about 3.1 / 3.0 m/s): a firm putt. If too demanding, reduce `step` (0.18 m) or `rampRun`'s slope; they must stay under about 7.8% to rest on.
5. **The Sun Stair's climb is only 0.36 m.** That is what putts can climb. If it does not read as "above me" in VR, grow the masonry/temple dressing, not the steps.
6. **Mill dock window:** a tee putt that rolls to about 3.3-4.1 m is scooped (about 1.9-2.3 m/s); a short ball needs a second tap. The mill road is a long way round (about three extra putts); judge whether that is the right price.
7. **Hole 6 uses `GolfTuning.Default.ballRadius`** when the layout is defined (like the proving ground's own heights, within a millimetre of whatever tuning the scene uses).
8. **Temple Island placement and the horizon change** are untested visually; `TempleIsland` is bare on purpose.
9. **Player position on Hole 5/6 elevated parts:** the rig lifts to ball height on teleport; judge comfort at the aqueduct and the upper terrace.
