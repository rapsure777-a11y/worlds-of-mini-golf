# Cloud return report: Graphical Pass 2 (Hole 1)

**To:** ChatGPT HQ, Local Claude (Andrew's PC) **From:** Cloud Claude, 2026-10-03
**Branch:** `cloud/graphical-pass-2-integration` (PR against `graphical-pass-2-wip`, not `main`) **Base:** `d864125`

## 1. Bottom line
All integration work in handoff section C1-C6 is **written**. **None of it has been compiled, rendered, tested, built, benchmarked or seen in a headset**: the cloud image has no Unity, Blender, GPU or .NET. Only a C# *syntax* parse (tree-sitter, 42 files, 0 errors) was run. Treat this as a large, careful, unverified patch. Expect a first compile-fix round, then a visual-tuning round. No test, gameplay, input or XR file was changed; the 41/41 baseline is still only proven on `main`.

## 2. What exists now
| Area | Result |
|---|---|
| HeroKit wired in | `SceneBuilder` builds it; `TropicalWorld.Build(kit, hero, defs, parent)`; `Dresser.Hero` + `Leaf()/Model()/PlaceMesh()` |
| Terrain | `IslandGen.splat = true`, `Hero_Terrain`; carved pool basin (`IslandGen.basins`) |
| Course | `theme.green = Hero_Turf` (bump 0.5), `theme.wall = Hero_Rail` |
| Hole 1 set | hero clubhouse, 7 hero palms, terraced cliff wall, 2 mesas, 3 boulder groups, sea arch, 5-layer leaf-card jungle (kit palms/rocks/foliage removed from Hole 1) |
| Waterfall | raycast-fitted sheet down the cliff, carved plunge pool + water disc, mist particles |
| Pipeline | 3 URP presets (Lean = old settings, Balanced = +depth/opaque, Rich = +HDR/post/4 cascades), runtime `QualityPreset` (`-quality`, F9, PlayerPrefs), global post Volume, session-log line |
| Water | `_GB_WATER_DEPTH` keyword: real depth/refraction in Balanced/Rich, baked-shore fallback in Lean |
| Docs | handoff section G, `ASSET_WORKFLOW.md`, `UNIVERSAL_MODDER_OPPORTUNITIES.md`, `ASSETS.md`, log, context |

## 3. Decisions Local Claude should know (so nothing is rediscovered)
- **Default preset is Balanced**, not Rich: Lean is the only tier with a measured 120 Hz result; Balanced only adds the two texture copies the water needs; Rich adds the expensive stuff. Benchmark all three (`Tools\benchmark.ps1 -Label x -Quality lean|balanced|rich`).
- `PC_RPAsset` **is** Balanced (so the editor/captures match the default). `PC_Lean_RPAsset` and `PC_Rich_RPAsset` are created by `ProjectSetup.EnsureQualityAssets()` (called by both setup and scene build).
- **Cliff wall moved** from the handoff's local (-9.5, 2) to (-9.5, -2.5) at scale 0.85: Hole 2's green sits at roughly local x -5…-10, z 5.4…9.6 and the wall would have been deleted by `RemoveObstructions`. Pool at (-7.4, 1.0); clubhouse at (-5.6, -1.8) (old spot overlapped the crates).
- **Orientation assumptions** (unchecked): model -Z = front (clubhouse faces the tee, wall faces the lane); palm lean = model +X. If mirrored, flip `PalmLeanSign` / the yaw constants in `DressHole1` (all hero yaws are in that one method).
- Hero palms are Hole 1 only (unknown triangle count, no LODs). Island/Hole 2 keep kit palms.
- Mist uses a new `Gamebreak/Mist` shader instead of hand-tuned URP particles. New `.meta` files were written with fixed GUIDs.
- `TropicalPostProfile.asset` is regenerated on every scene build; tune values in `SceneBuilder.BuildPostProfile`.
- The runtime asmdef now references the URP runtime assemblies (needed by `QualityPreset`).

## 4. First things to do locally (in order)
1. Compile (`-nographics`, grep `error CS` / `Shader error`). Likely spots: `SceneBuilder.BuildPostProfile` (URP 17 volume param names), `TropicalWorld.BuildWaterfall` (ParticleSystem modules), the five shaders with and without `_GB_WATER_DEPTH`, `ProjectSetup.ApplyLevel`.
2. `Tools\art-shots.ps1 -Quality balanced`, then `rich`, `lean`. Log must say `Waterfall: ... raycast-fitted`; no `Removed dressing` warnings for hero pieces; no exceptions from HeroKit.
3. Review shots against `Docs/checkpoints/hole1/` using the checklist in handoff C7 plus: waterfall hugging the cliff, pool level vs banks, sea arch seated, palms leaning the right way, rails/turf not shimmering.
4. `Tools\run-tests.ps1` (41/41; watch `TropicalScene_NoSceneryOnGreens`), builds, smoke test, benchmarks for each preset. Then headset, with Andrew's OK.
5. Write `Docs/CHECKPOINT_HOLE1_PASS2.md` (before/after images + numbers) once real renders exist.

## 5. Risks, ranked
1. **Compile errors / shader errors** (unverified API names and HLSL).
2. **GPU frame time**: depth+opaque copies under 4x MSAA in Balanced; HDR + bloom + 4 cascades in Rich. Prior template mix cost ~5 ms and forced 60 Hz. Fallbacks: accept 90 Hz, depth-only water, or shipping Lean.
3. Alpha-tested foliage overdraw/shimmer; TerrainSplat and RockTriplanar sample counts.
4. FBX orientation/scale and palm lean (see §3); Blender assets were never viewed in Unity.
5. Runtime URP-asset swapping in XR is unproven (applied once in `Awake`; if it misbehaves, pick the preset per launch).

## 6. Not done
Compile/test/render/build/benchmark/headset verification; before/after checkpoint doc; LODs; alpha-to-coverage; lantern lights; Holes 2-9, other worlds, gameplay (out of scope by instruction). No paid generation was run and `FAL_KEY` was never touched.

## 7. Files
See handoff section G1 for the full table. New: `QualityPreset.cs`, `Mist.shader`, three docs. Heavily changed: `TropicalWorld.cs`, `SceneBuilder.cs`, `ProjectSetup.cs`, `StylizedWater.shader`.


---

# Round 2 (after the first headset session, `Docs/HQ_REPORT_PASS2_HEADSET.md`)

Written by Cloud Claude, again with **no Unity/Blender/GPU**: syntax-parsed only (tree-sitter, 0 errors), **not compiled, rendered, tested or measured**. Decisions from Andrew: **80 Hz is an acceptable high-quality refresh if 90 Hz cannot be locked; scope is what is visible from Hole 1 (not island-wide fidelity work).**

## What changed
| Item (from the HQ instruction) | Change |
|---|---|
| Clubhouse overlapped the cliff/pool/waterfall and crowded Hole 2's tee | `ChooseClubhouseSite`: tries five spots behind the tee (local x -0.8…-4, z -6…-7), requires the whole 5x4 m footprint on dry land, free of keep-outs, >= PoolRadius+4 m from the pool and east of the cliff volume; logs `Clubhouse site:`. Barrel/crates moved to the beach side |
| Boulders flat, cliff wall floating | New `Dresser.Seat()`: after placing, shifts the model so every vertex in its lowest 35 cm sits just under the ground beneath it (clamped to 1.6 m). Used on the wall, mesas, boulders and all scatter rocks. Boulders also get `yStretch` 1.45 (rounder stones). `BuildWaterfall` now takes lip/base from the seated wall's renderer bounds |
| Old kit rocks, cliffs, mesas beside hero assets | Every `RockSmall/Medium/Large`, `Cliff*` placement (Hole 1 beach, island scatter, mountain crags, coast) now goes through `Dresser.Rock()` using the hero boulders (small/medium, scaled) or mesas (large) |
| Old kit palms beside hero palms | Palms within 26 m of Hole 1 and all Hole 2 palms are hero palms; farther island palms stay on the cheap kit palms |
| LODs | `MeshLod.Cluster` (editor vertex-clustering simplifier) + `HeroKit.AddRockLods`: rocks get LOD1 (22% tris) and LOD2 (6%) and are culled when tiny; the wall uses lodScale 0.6, the sea arch 0.45 (stays full detail much further out, being the fidelity standard). Scenery colliders use the LOD1 mesh. Palms, leaf cards: single-LOD distance culling (`HeroKit.AddCull`). LOD meshes are saved as `Kit/Meshes/HeroLod_*.asset` |
| Shadow cost | Shrubs, ferns, ground cover, mesas, arch and small rocks no longer cast shadows; palms, bananas, wall, clubhouse, medium/large rocks still do |
| Real VR GPU measurement | See below: the Unity GPU timer is not valid in VR, so `FrameCostProbe` measures by ablation. Session log also gains a "pacing at N Hz" line and labels the invalid GPU timer |
| Target | 90 Hz on Rich with 80 Hz fallback (SteamVR is set by Andrew). The code does not force a refresh rate; the pacing line and probe report tell which rate is stable |

## Measured facts that shaped this (from the repo, not the headset)
Triangle counts of the hero FBX files (parsed directly): palms about 4.4-4.5k each (cheap), boulders 18k each, mesas 28k, sea arch 28k, cliff wall 32k, clubhouse 33k. The local scene stats were 750k triangles, 700k shadow-casting. So the rocks, not the palms, were the heavy hero meshes, and almost everything was casting shadows. Note `SceneStats` now counts LOD0 only (worst case at close range).

## The ~12 ms mystery: how to find it
`FrameCostProbe` (new): start with `-probe` (use `Tools\vr-probe.ps1 -Quality rich -Label rich90`) or F10 on desktop, stand at the tee looking down the lane, ~3 minutes. For each stage it logs median/p95 frame interval and the share of frames at full vs half refresh: baseline, shadows off, terrain shadows off, MSAA off / 2x, render scale 0.7, no depth/opaque copies, hide leaf cards, hide rocks, hide terrain, hide water, hide all dressing. A big drop in interval or jump in full-rate share on one stage identifies the cost. Run it at the target refresh (90, then 80). Output goes to `Sessions/probe_*.txt` and `Docs/perf/probe_<label>.txt`. Built players only (it edits the live URP asset).

## Not done / caveats
- Nothing compiled or rendered; the new LOD generator, `Seat()` and clubhouse chooser are unproven. Likeliest first fixes: LOD0 hysteresis popping, clustered LOD1/LOD2 looking too coarse (tune `HeroKit.LodKeep` and thresholds), a seated rock sinking too much (tune `seatExtraSink` / `maxShift`).
- I did **not** change the Blender scripts. If the boulders still read as slabs after `yStretch`, change `boulders()` in `Tools/Blender/hero_rocks.py` (taller profile) and regenerate.
- The waterfall sheet is fitted to LOD0; at LOD1 distances (> about 35 m) the rock surface can differ slightly.
- Terrain still casts shadows (80k triangles); the probe has a stage for it. Palm LODs (geometry) were not made: palms are cheap, they are only distance-culled.
- 120 Hz remains a performance mode only; nothing here claims to reach it.

## Local validation, in order
1. Compile; look for `error CS` and for warnings `Clubhouse site`, `LODs skipped`, `Waterfall: ... raycast-fitted`.
2. `Tools\art-shots.ps1 -Quality rich`: check clubhouse clear of cliff/pool/waterfall and Hole 2's tee; wall and boulders sitting on the ground; no old kit rocks; LOD0 look unchanged vs the last render.
3. `Logs/scene-stats.txt` (LOD0 triangles) vs 750k before.
4. Tests (42), builds, then `Tools\vr-probe.ps1` at 90 Hz and again at 80 Hz, plus the normal session log for the pacing line.
