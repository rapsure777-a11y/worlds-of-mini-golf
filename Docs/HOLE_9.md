# Hole 9: Summit Sanctuary (the finale)

Branch `feature/hole-9-summit-sanctuary`, from `milestone-3-island-hopping` at `b1c0b9d`. **Written in the cloud: never compiled or run in Unity, never seen in a headset.** Checked only by tree-sitter syntax parsing, by compiling the runtime code and tests against stub Unity assemblies (`Tools/CloudTypeCheck`), and by the paper simulator (`Tools/DesignSim/h9_check.py`: 2D, no flight, no cliffs). Holes 1-8 are untouched except one existing test (see Files). `VolcanicIsland`-style Editor dressing (`SummitIsland.cs`) and `TropicalWorld.cs` were syntax-parsed only.

**Creative change honoured: there is no bowl.** Hole 9 builds no `RouletteBowl` (a test asserts it). Its identity is elevation, exposure and one long, honest putt over a cliff. No new mechanics or framework: one rectangle-union green with a height function, `BankWall` kickers, the Sun Stair's proven launch jump, open edges over out-of-bounds drops.

## Stages (par 5)

```
 top-down (z up, not to scale)                                   levels: tee 0 | T1 .20 | Overlook .38->.50 | Merge .50 | Landing .62 | Altar .72
                          Landing  SKY BRIDGE (no rails, abyss both sides)  ALTAR
   Shrine Lane (0.7 gate) +-------+===========================+--------+
   x -1..0.4, z 13.9..19  | kick. | bridge 1 m x 3.2 m, +0.10 | cup *  |   5 THE SKY BRIDGE
          ^ ^ ^           +-------+===========================+--------+
   +------+------+---------+  Merge Terrace z 12.5..13.9 (corner kicker turns the climb ball west)
   |Overlook     | chasm | climb |   4 FINAL APPROACH = Shrine Lane (west)
   | (hero land) | (OOB) | x 3.4..4.4, 5%, open west edge
   +--[gap .5]---+       |   2 SANCTUARY CLIMB (east, then north)      3 HERO SHORTCUT: ramp lip z 7.6 -> Overlook
   |ramp| Terrace 1 (z 5..6.6) east kicker --> climb
   +----+---------------+
   |  1 SUMMIT APPROACH: 2 m lane, z 0..5, +0.20 at 5%, tee (0,0.8), faces the sanctuary
```

1. **Summit Approach:** broad 2 m lane rising 0.20 m (5%, restable), the sanctuary straight ahead and above.
2. **Sanctuary Climb (safe route):** Terrace 1, then east; an angled kicker turns the putt north up the 4.4 m-wide-less **Waterfall Climb** (1 m lane, 5.9 m, 5% to 0.50 m, open west edge over the waterfall chasm = out of bounds); a second kicker turns the ball west onto the Merge Terrace.
3. **Hero Shortcut:** from Terrace 1 a launch ramp (the Sun Stair's 14.6 degree ramp, 0.5 m gap, landing 8 cm below the lip) jumps the chasm side straight onto the **Overlook**, then north across the Merge Terrace into the Shrine Lane. Saves about 7 m of detour (the safe route is about 31 m, the hero route about 24 m): normally a stroke.
4. **Final Approach:** the Shrine Lane: a 0.7 m **shrine gate** to thread, a calm 3% rise, a kicker that turns the ball east onto the railed **Landing**, where the ball can rest and be struck again. Both routes use it.
5. **The Sky Bridge:** the finale. A 1 m wide, 3.2 m long ridge climbing 0.10 m with **no rails** and an abyss on both sides (+1 stroke and back to the last rest spot), ending at the **Altar** (2 x 2 m, flat, the highest ground in the course) with the cup on the bridge's line. A straight, exposed putt: a great one holes, a good one leaves a real final putt, a soft one stops on the bridge, a crooked one falls.

## Par rationale

Safe route about 31 m of putting with four direction changes: expert 4-5, typical 6. Paper numbers: tee putt 3.0-3.6 m/s reaches Terrace 1; east kicker 2.8-3.4 m/s sends 98% up the climb; the climb needs 4.0-5.2 m/s from its foot (soft putts stay, restable); a 2.6 m/s bridge putt rests short or on the Altar, 3.0-3.4 m/s holes 15-18% and leaves the rest on the Altar. Four-plus real putting decisions: approach power, east/kicker line, climb power, merge/shrine-gate line, bridge speed and line.

## Hero shortcut

Optional, visible, tempting: from Terrace 1 aim north up the ramp and fly the 0.5 m gap (expect about 3.8-4.4 m/s from 0.7 m before the ramp: logged by the test; the Sun Stair needed 3.6-4.0). Soft = rolls back down (no penalty). Short = the pit under the gap (out of bounds: +1, back to the last rest spot). Long = lands further up the Overlook or the Merge Terrace. The failed attempt leaves the safe route fully open (tested).

## Environment (`SummitIsland.cs`, functional only: no colliders, particles or vegetation)

Summit island at (50, 80), radius 24, hole origin (47, 3.4, 70): the highest ground. Levelled plateau, masonry under every level, a narrow stone spine under the Sky Bridge, a cloud bank under it (water/cloud re-skin of the hazard boxes), a stepped sanctuary with pillars, a golden disc and an arch framing the bridge, a tall summit cliff, a waterfall into the chasm, torches, warm point lights, and a low sun disc over the sea as the sunset cue. Final sky, clouds, ruins detail, particles: deferred to Graphics Pass 3. Horizon: the distant island at bearing 75 moved to 160.

## Files

New: `Runtime/Obstacles/SummitSanctuarySpec.cs`, `Editor/Art/SummitIsland.cs`, `Tests/PlayMode/Holes9Tests.cs` (+ `.meta` each), `Tools/DesignSim/h9_check.py`, this file.
Edited: `Runtime/Course/HoleDefinition.cs` (Hole 9, `SummitCluster`, `SummitCentre`, radius 24), `Editor/Art/TropicalWorld.cs` (summit island wiring, horizon bearing), `Tests/PlayMode/Holes78Tests.cs` (the course-length test now expects holes 1..9), `Tools/CloudTypeCheck/*`, `DEVELOPMENT_LOG.md`.
Not touched: any runtime system (`RouletteBowl`, `LaunchRamp`, `BankWall`, `GolfBall`, `HoleController`, `CourseController`, ...), Holes 1-8.

## Tests (`Holes9Tests`, 24 new; Holes78 count unchanged; expected Unity total 176 + 24 = **200**)

Deterministic (10): definition and no-bowl; Summit cluster data, island fit, highest ground; length and hero saving (route polylines); elevation stage by stage and restable climbs; sightline from the tee; jump geometry; open edges over drops and the rail-less bridge; kickers turn the ball as needed; cup on a flat Altar; Holes 1-8 unchanged.
Physics (14, measured, tables logged): approach and slope hold; east kicker; climb window (soft stays, firm reaches the Merge Terrace); chasm edge (+1, returned); corner kicker; shrine gate and kicker; Sky Bridge putts; crooked putt falls and recovers; the cup; hero jump window and failures; failed jump leaves the safe route open; safe route tee-to-cup; hero route tee-to-cup; **nine-hole course run** (progression, scorecard of 9, `Finished`, `CourseFinished` once). The existing scene-based `TropicalScene_ProgressesThroughAllHolesAndFinishes` covers Hole 9 after a scene rebuild (flat Altar 0.4 m behind the cup).

## Cloud-side validation (not Unity)

Syntax parse of all new/edited files (0 problems); `CloudTypeCheck` `RuntimeAndTests` and `Editor` builds (0 errors); paper simulator `h9_check.py`; closed-form route lengths and kicker reflections.

## Unity-side steps for Local Claude

1. Fetch and check out the branch; let Unity import (3 new scripts, all with `.meta`).
2. Expect a clean compile; likeliest trouble: `SummitIsland.cs` and the `TropicalWorld.cs` wiring (Editor, only syntax-parsed).
3. Run `Holes9Tests`, then the full suite (expected 200). Read the logged tables.
4. Rebuild the Tropical scene: 9 holes, a Summit island (`IslandTerrain5`, `Summit_*` materials). Watch `TropicalScene_NoSceneryOnGreens`, `..._ProgressesThroughAllHolesAndFinishes`, `Clusters_*`.
5. Screenshots, then both players.

## Risks Local should verify

1. Never compiled (see above).
2. **Hero jump window and feed:** the ball is struck from 0.7 m before the ramp; casual players cap near 3.8 m/s. If out of reach, shorten the gap (0.5 -> 0.4) or lower `step`.
3. **Overlook landing:** the landing sits 8 cm below the lip and its near edge is an open edge with a 2 cm lip box (as the launch pad). Check a ball rolling back from the Overlook does not drop into the pit.
4. **Kicker lines:** the east kicker (Terrace 1) and corner kicker (climb top) are paper-tuned; a ball that misses the east kicker's band rebounds west.
5. **Climb difficulty:** 4.0-5.2 m/s from its foot, a 5.9 m climb. Lower `mergeHeight` or shorten `climbEndZ` if too demanding (keep under 7.85%).
6. **Sky Bridge:** 1 m wide, rail-less; check the visual edge reads in the headset (cloud bank beneath), that a straight putt is fair, and whether it is too punishing (add a 3 cm kerb if so). Falling off costs +1 and returns the ball to the last rest spot (the Landing).
7. **Shrine gate** (0.7 m): ensure it reads as precision, not frustration.
8. **Water/cloud re-skin:** hazard boxes under `Hazards` are re-skinned by name; confirm nothing renders as the default material.
9. Island and horizon placement untested visually; player comfort on the 0.72 m Altar and the exposed bridge.

## Steam Frame checklist for Andrew

- Does the tee view say "climb to that sanctuary"? Is the safe route obvious (east, north, west, shrine gate)?
- Hero jump: easy to find? Tempting but fair? Does a miss feel like a fair penalty and leave you somewhere playable?
- Is the climb too long or too demanding? Does the shrine gate feel precise rather than annoying?
- The Sky Bridge: does it feel like a finale? Is the drop readable? Does a good putt leave a real last putt? Does a miss (+1, back on the Landing) feel fair?
- Anything that snags, stalls or teleports; any spot you can get stuck in. After Hole 9, does the scorecard finish the course properly?
