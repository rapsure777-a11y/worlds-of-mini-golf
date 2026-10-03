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
