# Report: Hole 4 "Tiki Twister" approved, tiki crest fixed

**To:** Cloud Claude, HQ **From:** Local Claude (Andrew's PC), 2026-10-05
**Branch:** `milestone-3-island-hopping` (local commits ahead of origin: `2edf7dc` Hole 4 rebuild, `c425843` log note, `93246f2` crest fix, plus this report)

## Bottom line
Andrew played Hole 4 in the headset: **"Feels good. That's really good."** The tiki mask crest, the last art complaint, is fixed and confirmed: **"The masks look great now."** Holes 1-4 are built and headset-tested. Next is Temple Island (holes 5-6).

## Hole 4 "Tiki Twister" (par 3, Jungle Island east side)
- Layout: 1.2 m lane with a 24 cm drop, one 90 degree turn (corner wall), a second leg with a 1.4 m run-up, the 14 degree launch ramp, a **0.6 m gap** over a pit, and a 1.6 m-radius roulette bowl with the cup at its lowest point. About 9 m end to end (the earlier "Hollow Drop" was 14 m with five legs; Andrew found it too long, too flat and obstacle-free, then "way too easy" at the first par-3 version).
- Corner sweep (second shot from the corner): 2.4 m/s rolls back; 3.0-3.8 fall short in the pit (+1 stroke); 4.2-5.6 land in the bowl. A firm tee putt hits the corner wall and rebounds up the drop, no penalty.
- Code: `HoleDefinition.buildExtras` / `extraAreas`, `ProvingGround.BuildJumpBowlPieces`, `JumpBowlSpec` (`turn`, `turnZ`), `TropicalCourse.Hole4Spec()`, `JungleIsland.DressHole4`. `CourseGeometry.BuildWalls` now closes rail corner posts at open edges (Holes 1-3 unchanged).
- The roulette bowl is used as the **end of a hole**, per Andrew's direction. The waterwheel is still only in the proving-ground build (`Builds\ProvingGround`), not in the course; Andrew asked where it was, so it needs a hole.

## Tiki crest fix
- Cause: crest feathers were 0.008 m apart front to back but 0.025 m thick, so neighbours intersected, and the ones near 44 degrees lay along the diamond edge and sank into the shield.
- Fix (`Tools/Blender/hero_tiki.py`, `fan()`): each feather steps back by more than its thickness; on the mask the fan stands behind the shield, rooted below the diamond tip, root band removed. Poles use the same layering. Models regenerated (`HeroTikiMask_0`, `HeroTikiPole_0/1`); verified with Blender close-ups from four angles, then Andrew in the headset.

## Verification
| Check | Result |
|---|---|
| PlayMode tests | 107/107 at the last full run (before the crest fix, which changes models only; not re-run) |
| Scene rebuild + screenshots (rich) | Pass |
| PCVR + desktop builds | 0 errors |
| Headset (Andrew) | Hole 4 feel approved; masks approved |

## Open items
1. Temple Island holes 5-6: new island, `TropicalCourse.Clusters()` centre/radius, `Hole05/06`, a `TempleIsland` class, tests. Cloud's design sprint (`Docs/HOLE_DESIGN_SPRINT_001.md`, `HOLE_CONCEPTS_PASS2.md`) should drive these; the waterwheel and launch ramp are candidate features.
2. Work in obstacles (waterwheel) as features inside holes; Andrew's feedback is that holes feel generic without them.
3. `RESUME.md` was stale on Hole 4 and is refreshed alongside this report.
4. Known flaky test: `Swing_StrikesBallAlongFace` when run alone (passes in the full suite).
