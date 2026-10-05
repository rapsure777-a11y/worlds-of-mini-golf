# Hole 4 redesign plan: "Hollow Drop", short drop into the bowl

Status: **BUILT 2026-10-04 (Andrew chose: keep the drop, a smaller bowl, par 3). See DEVELOPMENT_LOG.md; the text below is the original plan.** Written 2026-10-04 after Andrew approved the proving-ground feel ("pretty good", with one open report of see-through rail ends). Andrew's choices: plan first, keep the hole's valley identity, finish in the roulette bowl, and merge the proving ground into the milestone branch locally (done: `d6c5f9d`, not pushed).

## Why change it

Andrew in the headset: Hollow Drop is "too long, too many turns to feasibly get there with the 4 hits", and the holes feel "flat, generic, with no obstacles". The design simulator agrees (five straight legs, par 4 unreachable even with perfect play). Cloud's Pass 2 proposed a curved Sinkhole Spiral (needs new curved-wall code); the proving ground already gives us a shorter route to a more interesting hole.

## The hole

Par **3**, about 9 m end to end (the built hole is about 14 m of lane with five legs). Two legs and one jump:

```
 top-down                                      side view (not to scale)
   tee lane (1.5 m wide)                       T____
        |                                            \  drop 24 cm (kept)
        |  drop                                       \____ run-up ___/ramp \  gap  /bowl\__O
        v                                                                      ^pit
     run-up, then ramp  --- gap --->  ( bowl, cup at the lowest point )
```

1. **Tee lane with the drop (kept).** About 3.5 m, widened to 1.5 m. The 24 cm drop is what makes this the valley hole: the ball arrives at the ramp already moving, so a medium putt is enough.
2. **Launch ramp** (the proving-ground `LaunchRamp`, 14 degrees) over a short gap with a pit under it.
3. **Roulette bowl** just past the gap, offset 0.6 m so the ball enters at an angle and orbits, with a notch and a low curb where it lands. The cup is the bowl's lowest point.

**Shot menu.** Safe: a controlled putt that stops on the run-up, then a second putt over the ramp. Bold: one firm putt from the tee, down the drop and over the jump. Signature: that putt holing out in one as the ball finishes its orbit (the sweep holed one of five landings).
**Recovery.** A soft putt rolls back, no penalty. A short jump falls in the pit: one stroke, back to the last rest spot. A ball that lands in the bowl cannot leave it (curb in the notch), and on the rim shelf it can be re-struck.
**Why par 3.** Tee putt, a correction, a tap in is three strokes; a good shot is two or one.

## What exists and what is missing

Exists (proving ground, tested): `LaunchRamp` geometry and flight, `RouletteBowl` with the entry notch, `JumpBowlSpec` / `ProvingGround.BuildJumpBowlHole` (lane, ramp, gap, offset bowl, pit), `GreenLayout.openEdges`. In the headset the jump-into-bowl was "pretty good".

Missing for a *real* hole (the work list):
1. **Composite holes.** `HoleFactory.Build` makes one `GreenLayout` and one cup. `HoleDefinition` needs an optional "pieces" hook so a hole can hang a bowl (and its cup) off the lane, plus a way to give `HoleFactory` the bowl's cup. Smallest change: an optional builder delegate on `HoleDefinition` that adds children and returns the cup.
2. **The drop on the first leg.** A height function on the lane layout before the ramp; the run-up must be flat and long enough (about 1 m) before the ramp so the speed is steady at the lip.
3. **Re-tuning from a sweep.** The drop adds speed, so the landing window shifts down (the plain launch hole lands from 3.2 m/s at the lip). Write the strike-speed sweep first, then pick drop height, run-up and gap so a medium tee putt lands.
4. **Dressing.** `JungleIsland.DressHole4` assumes the old footprint (a sunken basin ringed by jungle). It needs the new footprint, a sinkhole look for the bowl, and the plain proving-ground materials swapped for the theme's.
5. **Tests.** Hole 4 layout and no cliffs, drop speed, a strike sweep (rolls back / short penalty / lands / holes), and an update to the course-progression test, which currently drops the ball 40 cm short of the cup on a straight line and so must learn that Hole 4's cup is in a bowl.
6. **Rail ends.** Fix the see-through rail ends where a rail meets an open edge (`CourseGeometry.BuildWalls`), once we know where Andrew saw them.

## Decisions for Andrew

1. **Par 3** as planned, or par 4?
2. **Keep the 24 cm drop**, or flatten it so the hole is gentler for a first jump?
3. **Bowl size:** the proving ground uses a 2.0 m radius. Smaller is quicker to hole out but harder to land in.
4. Which hole showed the **see-through rails**?

## Not in scope

Holes 1-3 and 5-9, Graphics Pass 3, and cloud Claude's Sinkhole Spiral (kept as a later option; it needs curved walls).
