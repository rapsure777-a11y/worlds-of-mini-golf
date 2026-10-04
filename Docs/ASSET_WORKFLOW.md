# Environment asset workflow (reusable for all seven worlds)

Golf code is world-agnostic; a world is **data + dressing**. Pass 2 established this pipeline on Tropical Adventure.

## Pipeline
1. **Blender scripts (`Tools/Blender`)** generate meshes and baked PBR sets headlessly:
   `blender.exe -b --factory-startup --python Tools/Blender/<script>.py -- <out_dir>` (run from the repo root). `gblib.py` holds shared helpers (tileable bakes, vertex AO, decimation, FBX export). Outputs land in `Assets/_Game/Art/Generated/{Textures,Models}`.
2. **Naming convention:** every mesh object is `<Asset>__<MaterialSuffix>` (e.g. `HeroPalm_0__Leaves`, `HeroCliffWall__Rock`). FBX files carry no materials; `HeroKit.Instantiate` maps the suffix to a material. Textures are `<set>_{albedo,normal,height,mask}` (mask: G = occlusion, A = smoothness).
3. **Vertex data:** colour RGB = baked AO (or paint), alpha = wind weight. Leaf cards use the atlas table `LeafAtlas` (kept in sync between `leaf_atlas.py` and `HeroKit.cs`).
4. **Import:** `GeneratedAssetImporter` applies model/texture rules to everything under `Art/Generated`.
5. **Kit (`HeroKit` or a per-world equivalent):** creates materials (`Hero_*.mat`), leaf-card foliage meshes, and `Instantiate()` for models.
6. **World (`<World>World` + `Dresser`):** island/terrain with splat weights, per-hole `DressHoleN` in hole-local coordinates, hero set-pieces, then the obstruction scan (`RemoveObstructions`) and the `NoSceneryOnGreens` test.
7. **Quality:** every world uses the same `QualityPreset` (Lean / Balanced / Rich). New effects must be added to one tier at a time and benchmarked (`Tools/benchmark.ps1 -Quality ...`) before becoming default.

## Starting a new world
Copy the pattern, not the content: new Blender recipes (silhouettes and palette), a new texture set list, a new `<World>Kit`, a new `<World>World`, a new `WorldTheme` asset. Do not modify `Core/` or `Player/`. Log every asset in `ASSETS.md`.

## Rules
- Original or permissively licensed art only; no assets from reference games. AI-generated assets must be logged with tool, model, date and licence.
- Paid generation (`um fal ...`) needs Andrew's explicit approval and a `um fal price` quote first. See `Docs/UNIVERSAL_MODDER_OPPORTUNITIES.md`.
- Report "compiles/editor-tested" and "headset-tested" separately.
