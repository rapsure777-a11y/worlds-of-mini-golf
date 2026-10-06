# RESUME: where we stopped (2026-10-06)

Project: `C:\Users\fence\Projects\WorldsOfMiniGolf`. Read this, then `CLAUDE.md` and the last entries of `DEVELOPMENT_LOG.md`.

## State
- **Gameplay is complete: 9 holes, all headset-approved** on `milestone-3-island-hopping` (tip `491452c`, tests 203/203 at that point). Pars: 2,3,4,3,3,3,4,3(Hole 8 is now par 3 on the graphics branch only),5. Hole 8 shortcut was made harder and has an inner ring (1.5 cm).
- **Graphics Pass 3 is in progress on `feature/graphics-pass-3-tropical-adventure`** (latest `0b98d00`, 222/222 tests, both players built). NOT merged into production. It contains Cloud's 3A (PBR turf/rails/ball/putter, biome atmosphere, foliage planner, fences) plus my 3A fixes and 3B (modelled architecture, rock kit, lava, rails). It also carries two small gameplay-adjacent changes that production lacks: Hole 8 par 3 and audio master gain 0.9. Merge only when Andrew approves the look.
- Andrew's last verdict on 3B: temples good, trees on Holes 1-2 are the quality target for everything else, still "PS2" elsewhere.

## Open graphics items (his list)
1. Verify in the headset: rails (stone blocks were wound inside-out, fixed), lava (now molten + flow), basalt (darker), Hole 9 rock ground (came out purplish-grey, may need warming), temple grounding (rubble rings), mill house adult scale (doorway ~2 m).
2. Still primitive: jungle cliffs/mesas (Holes 3-4), water (foam, mist, shoreline), Starting Island clubhouse and docks, lava falls cliff, tree quality outside Holes 1-2.
3. Strike sound felt late (DSP buffer cut to 256); sound cut out once (master gain 0.9 applied). Ask if either recurs.
4. Perf: VR frame time of 3B NOT measured. Scene ~5350 mesh renderers, 4.4M triangles. Check `Sessions\session_*.txt` frame lines after a headset run.

## How to work
- Close the game before rebuilding. `Tools\art-shots.ps1 -Quality rich` rebuilds the scene and writes `Screenshots\` (hole shots, `*_stand` eye-level shots for holes 7-8, `showcase_*` hero assets). `Tools\run-tests.ps1`, `SetupAndBuildAll` for both players.
- Blender (Store build) kits: `Tools\blender-run.ps1 Tools\Blender\hero_temple.py` (also `hero_summit.py`, `hero_rockkit.py`, `hero_start.py`; `preview.py` renders quick previews). Library: `archlib.py`.
- Tiki, rails, chamfer: `ChamferMesh` (runtime), `Gp3Dressing` (rails/fences/lava/cloud sea), `Dresser.Crag/Footing/Grounded`.
- Andrew uses talk-to-text; he tests in the headset and reports. Ask before launching ("I wasn't ready"), launch only when he says so. Shell is PowerShell 5.1; long commands go to the background after ~10 min.
