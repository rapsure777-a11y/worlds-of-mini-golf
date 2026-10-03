# Cloud handoff: Worlds of Mini Golf, Graphical Pass 2

**Status of this branch: UNVERIFIED work in progress.** It compiles in the Unity editor. Nothing on it has been seen in a rendered scene, run through the test suite, built or played.

| | |
|---|---|
| Repository | https://github.com/rapsure777-a11y/worlds-of-mini-golf |
| WIP branch | `graphical-pass-2-wip` |
| WIP code checkpoint | `e3b34bdff33a2ca71f70fe2aba6f86b67b951432` ("WIP (UNVERIFIED): Graphical Pass 2 in progress"). This document is committed on top of it. |
| Last known-good build | `main` @ `9001494` (Hole 1 visual showcase checkpoint). 41/41 tests pass, both builds succeed, smoke test passes, desktop screenshots reviewed. Its art has **not** been checked in the headset. |
| Last headset-verified gameplay | `f55ede1` (Steam Frame pose offsets fixed; Andrew: "worked perfectly", locked 120 Hz) |
| Tag | `v0.1-prototype` = `afdf464` (adds auto putter length + wrist watch; both built, not headset-tested) |
| Written | 2026-10-03, by local Claude Code on Andrew's PC |

Read `CLAUDE.md`, `PROJECT_CONTEXT.md`, `DEVELOPMENT_LOG.md` and `Docs/CHECKPOINT_HOLE1.md` as well. This file covers what they don't: the state of Graphical Pass 2.

---

## A. Current project state

### Technical configuration
- Unity **6000.3.9f1**, URP **17.3**, OpenXR **1.18** + XR Management 4.7, Input System 1.20.
- Windows x64, **Direct3D11 only**, **Linear** colour, OpenXR **Single Pass Instanced**, Mono scripting backend.
- PC URP asset is set up by `ProjectSetup.ConfigureQuality()` (`Assets/_Game/Scripts/Editor/ProjectSetup.cs`):
  - MSAA 4×, render scale 1, shadow distance 25 m, **2 cascades**, low soft-shadow quality;
  - **HDR off, depth texture off, opaque texture off, SSAO off.**
  - These were chosen deliberately after the first VR playtest. The URP template defaults measured 6.5 ms median / 10.8 ms p99 GPU and made SteamVR drop to 60 Hz. With the lean settings the game held a flat 8.33 ms (120 Hz).
- Hardware: AMD Radeon RX 7900 XT. Headset: **Steam Frame**, streaming over SteamVR (PCVR); the game runs on the PC.
- Tools on the PC:
  - Unity: `C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe`; Unity CLI: `%LOCALAPPDATA%\Unity\bin\unity.exe`.
  - Blender 5.2: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`.
  - Universal Modder: `%USERPROFILE%\.local\bin\um.exe`.

### Gameplay systems
All under `Assets/_Game/Scripts/Runtime`. Pass 2 changes **none** of them.

| System | Files | Status |
|---|---|---|
| Ball physics: own rolling model, 5/7 g on slopes, rolling resistance `a0 + k·v`, wall restitution from pre-step velocity, `maxContactLift` 0.5 m/s, 120 Hz | `Core/GolfBall.cs`, `Resources/GolfTuning.asset` | Tests + **headset-verified** ("absolutely great") |
| Putter: follows the controller exactly, swept box-vs-ball strike, tracking-jump guard (0.3 m / 15 m/s) | `Core/Putter.cs` | Tests + **headset-verified** |
| Auto putter length: 0.62 × 90th-percentile standing eye height, plus a saved personal offset | `Player/PutterSizing.cs` | Tests only, **not headset-tested** |
| Cup, hole flow, strokes, out of bounds (+1 stroke, ball returned), stroke limit 10 | `Core/Cup.cs`, `HoleController.cs`, `CourseController.cs`, `PlayableSurface.cs`, `OutOfBoundsSurface.cs` | Tests + headset (2 holes) |
| Locomotion: teleport, snap turn 30°, grab-move, go-to-ball (A), ball back to last shot spot (B) | `Player/VRRig.cs`, `HandInput.cs` | **Headset-verified** |
| Scorecard (auto-shows 5 s after each hole) | `UI/ScorecardPanel.cs` | Headset-verified |
| Wrist watch status on the free hand (swaps with putter hand) | `UI/WristDisplay.cs` | Built, **not headset-tested** |
| Desktop debug mode, session log, smoke test, GPU benchmark | `Debug/`, `UI/DebugOverlay.cs` | Desktop-verified |

### Course content
- Hole 1 "Beach Warm-up": par 2, a 7 m lane with an 8 cm rise. It is the hole being dressed for the visual benchmark.
- Hole 2 "Palm Corner": a draft dogleg with basic dressing.
- Holes 3–9 do not exist and **must not be started** before visual approval.

### Automated tests
- `Assets/_Game/Tests/PlayMode`: CourseTests, DiagnosticTests, FrameLayoutTests, GolfPhysicsTests, PutterEdgeTests, PutterSizingTests.
- **Latest verified result: 41/41 pass, on `main` @ `9001494`.** They have not been run on this branch.

### Steam Frame controllers
- Native OpenXR interaction profile: `Assets/_Game/Scripts/XR/SteamFrameControllerProfile.cs` (asmdef `Gamebreak.MiniGolf.XR`).
  - Path `/interaction_profiles/valve/frame_controller`.
  - It **must** request the extension `XR_VALVE_frame_controller_interaction`, otherwise SteamVR rejects it with `XR_ERROR_PATH_UNSUPPORTED` and falls back to Touch emulation.
- The custom device layout's state offsets must match the native packing:
  - binary = 1 byte, values of 4 bytes or more aligned to 4;
  - a pose expands to isTracked, trackingState, position, rotation, velocity, angularVelocity;
  - pose offsets are 33/36/40/52/100/112.
  - Wrong offsets made the putter swing erratically. `FrameLayoutTests` guard this.
- Under Touch emulation (the fallback), right B, X and Y all arrive as Touch B, and the left D-pad arrives as X (down) and Y (other directions). The native profile exposes `frameX`/`frameY` (`HandInput.cs`, handled in `VRRig.cs`).
- Mapping:
  - A = go to ball; B = ball back to last shot spot;
  - X = scorecard; Y (hold) = swap putter hand;
  - stick = teleport/turn; left grip = grab-move; right grip + stick = putter length/angle.
- Other enabled profiles: Index, Touch, Quest Touch Plus/Pro, Vive, Reverb G2, KHR Simple.

---

## B. Graphical Pass 2 progress

**Goal (HQ brief "Graphical Quality Pass 2"):** a generational jump from "Walkabout-level" to Crash Bandicoot 4-style richness on Hole 1. That means foliage, palms, cliffs, terrain, normal-mapped materials, architecture, layered jungle, distant scenery, ocean, waterfalls, lighting and atmosphere. Some VR headroom may be spent, but frame pacing must stay stable.

**Approach:** Blender (headless Python) generates higher-fidelity hero meshes and baked PBR texture sets. Unity editor code turns them into materials and places them in the generated scene. Everything is original and procedural; no third-party or AI assets have been used yet.

### B1. Blender pipeline: `Tools/Blender/` (complete, outputs generated and committed)
Run any script with:
```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python Tools/Blender/<script>.py -- <out_dir>
```
The output directory is optional. Run from the repo root so the default relative paths resolve. Cycles uses the AMD GPU (HIP). Each script takes seconds to a few minutes.

| Script | What it does | Output (committed) | Status |
|---|---|---|---|
| `gblib.py` | Shared library (detail below) | (none) | Done |
| `bake_textures.py` (`-- out [size]`, default 1024) | Nine tileable PBR sets: sand, lawn, sandstone, path, wood, thatch, bark, bamboo, turf | `Assets/_Game/Art/Generated/Textures/<name>_{albedo,normal,height,mask}.png` (36 files) | Generated. Looked at as images only; sandstone and path were redesigned once |
| `leaf_atlas.py` | 2048² foliage atlas drawn with numpy | `leaves_albedo.png` (RGBA, alpha = cut-out), `leaves_normal.png` | Generated; image-reviewed only |
| `hero_palm.py` | Three coconut palms: curved segmented trunk, crown boss, coconuts, 18–20 V-folded fronds each | `Models/HeroPalm_{0,1,2}.fbx` | Generated; never seen in Unity |
| `hero_rocks.py` | Sandstone formations, roughly 9k–24k faces each (decimation targets 9k–16k), vertex AO | `Models/HeroMesa_0`, `HeroMesa_1` (tiered mesas, 9 m and 6.5 m), `HeroSeaArch` (7 m span), `HeroCliffWall` (16 × 7 m terraced face), `HeroBoulders_0/1` | Generated; never seen in Unity |
| `hero_clubhouse.py` | Tiki clubhouse (detail below) | `Models/HeroTikiClubhouse.fbx` | Generated; never seen in Unity |

**`gblib.py` contents:**
- `reset_scene()` (Cycles on HIP) and `srgb()`.
- A node-graph helper `G`: math ops, `torus()` (4D torus mapping makes textures tile seamlessly), 4D `noise()`/`voronoi()`, ramps, `emit`.
- `bake_plane()`: EMIT-bakes albedo, height and mask. numpy then derives the normal map (from height) and cavity occlusion.
- Mesh helpers: `mesh_object`, `join`, `box_uv`, `ensure_color_attr`, `bake_vertex_ao` (AO into the `Col` attribute, alpha preserved), `decimate`.
- `export_fbx_objects`: Y-up, `axis_forward=-Z`, `apply_unit_scale`, `FBX_SCALE_ALL`, `bake_space_transform`, LINEAR vertex colours, no materials.

**Texture sets:**
- `_mask` holds G = occlusion, A = smoothness.
- `_height` is kept for future parallax and blending.

**Leaf atlas:**
- Cells (pixels, origin bottom-left, leaf base at the bottom, pointing +V):
  - `palm_frond` (0,0,512,1024), `palm_dry` (0,1024,512,1024);
  - the 512² cells are `broadleaf` (512,0), `monstera` (1024,0), `fern` (1536,0), `bush_a` (512,512), `bush_b` (1024,512), `grass` (1536,512), `hibiscus` (512,1024), `plumeria` (1024,1024), `flowers_purple` (1536,1024), `banana` (512,1536), `ivy` (1024,1536), `bush_c` (1536,1536).
- **The same table is duplicated in C#** (`LeafAtlas` in `HeroKit.cs`); keep the two in sync.

**Palm models:**
- Objects are `HeroPalm_N__Bark` (trunk, boss, coconuts) and `HeroPalm_N__Leaves` (fronds UV-mapped into the `palm_frond`/`palm_dry` cells).
- Vertex RGB = baked AO; vertex alpha = wind weight.
- Heights are 4.6 / 5.6 / 3.8 m.

**Clubhouse model:**
- Objects:
  - `__Wood` (deck, posts, beams, bar; ~870 faces), `__Thatch` (layered roof; ~2k), `__Bamboo` (rails, poles; ~3.4k);
  - `__Totem` (carved poles with painted vertex colours; ~8.7k), `__Lantern`;
  - `__Paint` (extruded "TIKI CLUB" 3D sign text and surfboards, coloured by vertex colour).
- The deck is centred at the origin. The front faces Blender −Y, which **should** be Unity −Z (unverified).

All models are named `<Asset>__<MaterialSuffix>`. Unity assigns materials from the suffix, so the FBX files carry no materials.

### B2. Unity import: `Assets/_Game/Scripts/Editor/Art/GeneratedAssetImporter.cs` (written; ran once)
An `AssetPostprocessor` for everything under `Assets/_Game/Art/Generated/`:
- **Models:** no materials or animation, Mikk tangents, readable (for MeshColliders), no mesh compression, `useFileScale`.
- **Textures:**
  - `*_normal` → NormalMap; `*_height`/`*_mask` → linear; everything else → sRGB;
  - aniso 8, max 2048, compressed HQ;
  - `leaves_*`: Clamp wrap, `alphaIsTransparency`, `mipMapsPreserveCoverage` (reference 0.5).

The handoff import (`Logs/handoff_import.log`, local only) imported all 48 generated files with no errors. Their `.meta` files are committed. **Import settings were not inspected afterwards.**

### B3. Shaders: `Assets/_Game/Art/Shaders/` (written, compile untested on GPU)

| Shader | File | New/changed | Notes |
|---|---|---|---|
| `Gamebreak/StylizedLit` v2 | `StylizedLit.shader` + `StylizedCommon.hlsl` | Changed | See "StylizedLit v2" below. Defaults keep the old look, so existing kit materials should be unaffected |
| `Gamebreak/TerrainSplat` | `TerrainSplat.shader` | New | See "TerrainSplat" below |
| `Gamebreak/RockTriplanar` | `RockTriplanar.shader` | New | See "RockTriplanar" below |
| `Gamebreak/StylizedWater` v2 | `StylizedWater.shader` | Changed | **Requires the URP depth and opaque textures**, which are currently OFF (see risks) |
| `Gamebreak/Waterfall` | `Waterfall.shader` | New | See "Waterfall" below |
| `Gamebreak/GradientSky` | unchanged | | |

**StylizedLit v2 adds:**
- toggles `_UseNormalMap` (`_NORMALMAP`), `_UseMaskMap` (`_MASKMAP`) and `_AlphaClip` (`_ALPHATEST_ON`, also applied in the shadow and depth passes);
- properties `_BumpMap`, `_BumpScale`, `_MaskMap`, `_OcclusionStrength`, `_Cutoff`, `_Translucency`, `_TranslucencyColor`, `[HDR] _EmissionColor` and `_Cull` (Off for two-sided leaves; back faces use the flipped normal);
- leaf flutter in the wind sway, weighted by vertex alpha.

**TerrainSplat:**
- Vertex colour = weights: R sand, G lawn, B rock, A path.
- Sand, lawn and path are projected from above. Rock is triplanar.
- Layers blend by height map.
- Below `_SeaLevel` the terrain gets a seabed tint. `_MacroTex` adds large-scale variation.
- Main properties: `_Tiling` (tile size in metres per layer), `_Smooth`, `_NormalStrength`, `_BlendSharpness`, `_LawnTint`.

**RockTriplanar:**
- Triplanar sandstone with grass/moss on upward faces (`_TopCoverage`, `_TopSoftness`).
- Vertex colour R = baked AO.
- No UVs needed.

**StylizedWater v2:**
- Depth-based absorption (shallow to deep), refraction of the opaque texture, intersection foam.
- Output is opaque.
- **It requires the URP depth and opaque textures**, which are currently OFF.

**Waterfall:**
- Alpha-blended sheet; UV.y is the flow direction.
- Scrolling streaks (`_NoiseTex`, `_Speed`, `_StreakScale`), lip and plunge foam, soft edges.

**Texture requirements:** every StylizedLit hero material needs the `<set>_albedo`, `_normal` and `_mask` textures from `Generated/Textures`. TerrainSplat needs the sand, lawn, sandstone and path albedo + normal. RockTriplanar needs sandstone (rock) and lawn (top).

### B4. `Assets/_Game/Scripts/Editor/Art/HeroKit.cs` (written, compiles, **never executed**)
```csharp
public static class LeafAtlas {                 // mirrors leaf_atlas.py CELLS
    public static readonly Dictionary<string, RectInt> Cells;
    public static Vector2 UV(string cell, float u, float v);   // u,v 0..1 inside a cell, 3 px inset
}
public class HeroKit {
    public const string TexDir = "Assets/_Game/Art/Generated/Textures";
    public const string ModelDir = "Assets/_Game/Art/Generated/Models";
    public Material Terrain, Rock, Wood, Thatch, Bamboo, Bark, Totem, Lantern, Paint, Leaves, Turf, Rail, Waterfall, PoolWater, Mist;
    public static HeroKit Build(TropicalKit kit);   // creates/updates materials + foliage meshes, SaveAssets
    public Mesh Foliage(string name);
    public GameObject Instantiate(string model, Transform parent, Vector3 pos, float yaw, float scale = 1f,
                                  bool colliders = false, bool outOfBounds = false, bool shadows = true);
}
```
- **Materials** are written to `Assets/_Game/Worlds/Tropical/Kit/Materials/Hero_*.mat`:
  - `Hero_Terrain` (TerrainSplat) and `Hero_Rock` (RockTriplanar);
  - StylizedLit with normal and mask maps: `Hero_Wood`, `Hero_Thatch`, `Hero_Bamboo`, `Hero_Bark`, `Hero_Totem` (tinted wood), `Hero_Turf`, `Hero_Rail` (wood);
  - `Hero_Paint` (white, vertex-coloured) and `Hero_Lantern` (emissive);
  - `Hero_Leaves`: atlas, alpha test 0.45, two-sided, translucency, wind;
  - `Hero_Waterfall`, `Hero_PoolWater` (a copy of the kit water with stronger absorption and no waves), `Hero_Mist` (URP Particles/Unlit, transparent, with a generated `soft_dot.png`).
- **`Instantiate`:**
  - instantiates and unpacks the FBX;
  - maps each child `…__Suffix` (a Unity `.001` suffix is stripped) to Bark/Leaves/Rock/Wood/Thatch/Bamboo/Totem/Lantern/Paint (unknown suffixes fall back to Paint);
  - optionally adds MeshColliders (never on Leaves or Lantern) and `OutOfBoundsSurface`;
  - marks everything batching-static.
- **Leaf-card foliage meshes**, saved to `Kit/Meshes/<name>.asset` with tangents recalculated:

  | Mesh | What it is | Atlas cells |
  |---|---|---|
  | `LeafBush0`–`2` | Outward cards with spherical normals | bush_a/b/c |
  | `FlowerShrub0`–`2` | Bush cards plus flower cards | bush cells + hibiscus/plumeria/flowers_purple |
  | `BigLeaf0`–`1` | Arching leaf ribbons | broadleaf, monstera |
  | `Banana0` | Upright arching leaves with stems | banana |
  | `Fern0` | Arching fronds | fern |
  | `GrassClump0`–`1` | Four crossed cards with up normals | grass |

  Vertex alpha = wind weight. Leaves are expected to use `Hero_Leaves`.
- The missing-file exceptions mention `Tools/blender-assets.ps1`. **That script does not exist yet.** Run the Blender scripts individually (B1).

### B5. Terrain splat weights: `Assets/_Game/Scripts/Editor/Art/IslandGen.cs` (written, never executed)
- New `public bool splat;` (default **false**, so current behaviour is unchanged).
- When true, `BuildTerrain()` writes `Splat(x, z, h, n)` weights to the vertex colours and UV0 = world XZ:
  - sand near the coast and below the sand line (kept off hole sites);
  - path from path zones;
  - rock on slopes with `n.y` below about 0.86 (kept off hole sites);
  - lawn takes the remainder;
  - priority order: rock, path, sand, lawn.

### B6. Measurement (complete)
- `Assets/_Game/Scripts/Runtime/Debug/GpuBenchmark.cs`: run the built player with `-benchmark`.
  - Renders 4320×2160 offscreen (HDR target, 4× MSAA, FOV 100, about two Steam Frame eyes) from four Hole 1 viewpoints.
  - Times them with FrameTimingManager and writes `Benchmark/benchmark.txt` next to the exe.
- `Tools/benchmark.ps1 [-Label x]` runs it against the desktop build and copies the result to `Docs/perf/benchmark_<x>.txt`.
- **Baseline before Pass 2** (`Docs/perf/benchmark_before_pass2.txt`): **GPU median 1.65 ms, p95 1.86 ms; CPU main ≈2.0 ms.**
- This is an offscreen proxy. It excludes the compositor and streaming, and it was taken with the lean URP settings.

### B7. Universal Modder / fal
- `um` 0.2.0 is installed and works. If its uv trampoline breaks, set `$env:UV_PYTHON_INSTALL_DIR="$env:USERPROFILE\uvpy"`.
- **`FAL_KEY` is not set** in the process or user environment, so **no paid generation has been done**. `um fal price` also needs the key.
- Useful recipes:
  - textures: `fal-ai/z-image/turbo/tiling`;
  - PBR: `fal-ai/patina/material`;
  - 3D: `fal-ai/trellis-2`, Hunyuan 3D v3.1 Pro;
  - images: nano-banana-2.
- Rough estimate for a Hole 1 hero pass: **$5–15**. This is not confirmed; confirm with `um fal price` once a key exists.
- Andrew must approve any paid work. Any AI-generated asset must be logged with its licence in `ASSETS.md`.

### Completeness summary
| Component | State |
|---|---|
| Blender scripts + generated textures/models | Complete; outputs exist and are committed; **never viewed in Unity** |
| Importer | Complete; ran once without errors; settings not inspected |
| Shaders | Written; Unity parsed them at import. GPU variant compilation, SPI stereo and visual correctness are **untested** |
| HeroKit / LeafAtlas / foliage generators | Written, compiles, **never run** |
| IslandGen splat | Written, compiles, **off by default, never run** |
| Scene integration (SceneBuilder/TropicalWorld) | **Not started** |
| Waterfall feature | **Not started** (shader + materials only) |
| URP/HDR/post-processing changes | **Not started** |
| Runtime quality presets | **Not started** |
| Docs: ASSETS.md, asset-generation workflow, before/after checkpoint | **Not started** |

---

## C. Exact remaining implementation

### 1. Connect HeroKit into scene generation
`SceneBuilder.BuildTropicalScene()` (`Assets/_Game/Scripts/Editor/SceneBuilder.cs`, around lines 21–74) currently does:
```csharp
var kit = Art.TropicalKit.Build();
...
Art.TropicalWorld.Build(kit, defs, null);
```
- Add `var hero = Art.HeroKit.Build(kit);` right after `TropicalKit.Build()`. It needs `kit.Water` and `TextureGen` outputs (`Grain.png`, `FoamNoise.png`), which `TropicalKit.Build()` creates.
- Change the `TropicalWorld.Build` signature to `Build(TropicalKit kit, HeroKit hero, List<HoleDefinition> defs, Transform parent)`.
- Pass `hero` into `Dresser`: add a `public readonly HeroKit Hero;` field so dressing code can call `d.Hero.Instantiate(...)` and place foliage meshes.
- `Dresser.Place(string mesh, Material mat, ...)` looks meshes up via `Kit[mesh]`. Add an overload that takes a `Mesh` directly, or register the hero foliage meshes in the kit.

### 2. Terrain splatting
In `TropicalWorld.Build`: `var island = new IslandGen { ..., splat = true };`. In `BuildTerrainAndSea`, use `tr.sharedMaterial = hero.Terrain;` instead of `kit.Terrain`.

### 3. Turf and rails
In `SceneBuilder`, `theme.green = hero.Turf; theme.wall = hero.Rail;`.
- Green UVs are local metres (`CourseGeometry.cs` around line 73). With `_BaseMap` scale 1.2, one texture tile spans about 0.83 m.
- Rail UVs are (length in metres, 0..1).
- Check that the turf doesn't hide the ball's line or shimmer at grazing angles, since it is the surface you putt on. Consider a lower `_BumpScale`.

### 4. Replace placeholder scenery (Hole 1 only)
Work in `TropicalWorld.DressHole1(Dresser d, HoleFrame f, HoleDefinition def)`. `f.L(x, z)` gives hole-local positions: x is across the lane (+ = right, sea side) and z is down the lane.
- **Palms:** replace `d.Palm(...)` in the `palms` array with `hero.Instantiate($"HeroPalm_{v % 3}", root, f.L(x, z) at ground height, yaw)`. The lean is built into the mesh along Blender +X, so choose yaw so seaward palms lean out. `Dresser.Palm` is also used by Hole 2 and the island scatter; switching those too is fine for world consistency but optional for the benchmark.
- **Clubhouse:** replace the `"TikiHut"` placement (local −4.4, −1.6, facing the start) with `HeroTikiClubhouse`. Check its footprint against the green's keep-out (`LayoutBounds` + margins) and the welcome sign at (−1.7, −1.1). The hut collider should stay plain out of bounds (no `OutOfBoundsSurface` needed on decks people walk on).
- **Rocks/cliffs:**
  - Replace `Cliff0/1/2` and `RockLarge0`/`RockMedium*` with `HeroCliffWall` (backdrop, around local (−9.5, 2), facing the lane), `HeroMesa_0/1` (inland skyline), `HeroBoulders_0/1` (near the lane) and `HeroSeaArch` (in the lagoon, sea side, around x +12…+20; legs sink 1.2 m below the waterline by design).
  - Use `colliders: true, outOfBounds: true` for anything the ball can reach.
  - `RemoveObstructions()` deletes any dressing collider found on a green. Watch the log for `[Gamebreak] Removed dressing`.
- **Foliage:** replace the inland planting (`BigLeaf*`, `FlowerBush*`, `Bush*`), the sea-side beds and the `Grass*` scatter with `LeafBush*`, `FlowerShrub*`, `BigLeaf0/1`, `Banana0`, `Fern0` and `GrassClump*`, using `hero.Leaves`. Add a deeper **layered jungle** band behind the inland rail: ground cover, then shrubs and bananas, then palms and mesas. That layering is a key ask in the brief.
- The rail-side beds must not overhang the lane: the ball and putter need a clear view and swing.

### 5. Waterfall, pool and mist
Nothing exists yet. Suggested design:
- Place `HeroCliffWall` behind Hole 1 at local around (−9.5, 2), face toward the lane. It sits on the raised backdrop mound `island.mounds.Add((frames[0].L2(-8.5f, 0.5f), 7f, 1.6f))`.
- **Sheet:** a curved ribbon mesh down a notch of the cliff face, built with `MeshBuilder`. UV.y runs along the flow (0 at the lip, 1 at the plunge); about 1.2–2 m wide, 5–6 m tall. Material `hero.Waterfall`, shadows off.
- **Pool:** a raised pool at the base. Either add a negative "basin" zone to `IslandGen` or use a rock ring from `HeroBoulders`. Put a water disc at the pool level with `hero.PoolWater`. The ocean is at y = 0 and the pool sits above it, so it needs its own surface.
- **Mist:** a `ParticleSystem` at the plunge using `hero.Mist`:
  - about 20–40 particles, size 0.6–1.5 m, slow upward drift, lifetime 2–3 s, local space;
  - keep overdraw low for VR.
- Out of bounds: the pool water and cliff need `OutOfBoundsSurface` colliders where the ball could reach.

### 6. URP, HDR, depth/opaque textures, shadows, post-processing
Edit `ProjectSetup.ConfigureQuality()`, then run `Gamebreak/Setup/Configure Project for PCVR` (or `Automation.Setup`):
- **Depth texture ON and opaque texture ON (required by StylizedWater v2).** Alternatively, give the water shader a fallback path that works without them.
- HDR ON, needed for bloom and tonemapping.
- Shadows: 4 cascades, about 35–40 m distance, medium or high soft shadows.
- In `SceneBuilder`:
  - add a global `Volume` with a `VolumeProfile` asset containing Tonemapping (ACES or Neutral), Bloom (low intensity, threshold about 1.1), Color Adjustments and White Balance;
  - set `UniversalAdditionalCameraData.renderPostProcessing = true` on the rig camera.
- The earlier measurement showed these template-style settings cost about 5 ms GPU in VR and dropped SteamVR to 60 Hz. Add each change, measure it with the benchmark, and keep a lean fallback. The brief asks for adjustable **runtime quality presets**, logged in the session log; nothing exists yet. A small `QualityPreset` runtime component that toggles URP asset values or swaps between two URP assets would satisfy this. Golf code must stay world-agnostic.

### 7. Verify import, orientation, scale and materials
For each FBX:
- confirm the up axis (no −90° X rotation on the root after `bake_space_transform`);
- confirm metre scale (palm about 4–6 m tall, clubhouse deck at human scale, the 16 m cliff wall);
- confirm the clubhouse front faces −Z;
- confirm the vertex colours came through (AO darkening; totem and sign paint colours);
- confirm tangents exist.

Then check visually: no pink materials; leaf alpha cut-out is crisp; two-sided leaves are lit correctly on both faces; triplanar rocks show no stretching; no splat seams. A small gallery capture (each hero asset on a turntable in front of the camera) in `Automation` would help.

### 8. Compile, test, benchmark, compare
1. `powershell -File Tools\art-shots.ps1`: regenerates the scene and writes `Screenshots/*.png` plus `Logs/art.log` and `Logs/scene-stats.txt`. Iterate until it looks right. Compare against `Docs/checkpoints/hole1/*.jpg` from the same viewpoints (`Automation.CaptureScreenshots` defines them).
2. `powershell -File Tools\run-tests.ps1`: all 41 must pass. `TropicalScene_NoSceneryOnGreens` is the one most likely to catch a dressing problem. Fix the cause; never delete tests.
3. Run `Automation.SetupAndBuildAll` (both players), then `Tools\smoke-test.ps1`, then `Tools\benchmark.ps1 -Label after_pass2`.
4. Write `Docs/CHECKPOINT_HOLE1_PASS2.md`: before/after images, the hero asset list, test and build results, before/after benchmark numbers, and what is not headset-verified.
5. Update `ASSETS.md` (all Pass 2 assets: original, generated by the `Tools/Blender` scripts). Write the reusable asset-generation workflow doc for all seven worlds (Blender commands, recipe structure, the material-suffix convention, the `um fal` options and cost process). Update `DEVELOPMENT_LOG.md` and `PROJECT_CONTEXT.md` (art pipeline section).
6. Run the VR build only on Andrew's PC, with Andrew's agreement (see D).

---

## D. Risks and blockers

**Known problems on this branch now**
- **The ocean is probably rendered wrongly on this branch.** StylizedWater v2 samples the camera depth and opaque textures, but they are disabled (C6). Expect wrong colours, black, or flat water until step 6 is done or the shader gets a fallback. `main` uses the old water and is unaffected.
- The scene asset was **not regenerated** on this branch. Building now would produce the `main` scene with the new water shader and nothing else.

**Untested assumptions (high likelihood of needing fixes)**
- **Shader variant compilation** was not exercised. The import ran with `-nographics`, and the shaders have never been rendered. Expect typos or URP 17 API issues in `TerrainSplat`, `RockTriplanar`, `Waterfall` and the v2 changes: stereo macros, shadow-caster passes with alpha clip, `DeclareOpaqueTexture` in SPI.
- **FBX orientation and scale.** The settings are standard, but the result has never been looked at.
- **Leaf cut-outs.** Alpha-test plus 4× MSAA in VR can shimmer. Alpha-to-coverage is not implemented. Mip coverage preservation is set, but mip colour bleeding at the transparent edges of atlas cells was not checked.
- **HeroKit `Mist` material.** It is set up by hand on URP's particle shader (keywords, blend modes) and may render opaque or not at all.
- **`EditorUtility.CopySerialized` when foliage meshes are rebuilt** (the second run of `HeroKit.Build`) is untested.
- **The splat thresholds** (sand line, slope 0.86 for rock) are guesses. Expect tuning.
- **`TerrainSplat` cost:** sand, lawn and path from above, rock triplanar (3×), normals for each, plus macro, comes to about 15 texture samples per pixel on a large on-screen surface. Rocks are triplanar too, and leaves add alpha-test overdraw.

**Performance**
- VR budget: 8.33 ms at 120 Hz. The previous attempt with HDR, depth/opaque, SSAO and 4 cascades caused 60 Hz reprojection.
- The benchmark is an offscreen proxy, so real VR frame time can only be confirmed in the headset; the session log records it.
- Brief: aim for native refresh where practical; avoid unstable pacing.

**Needs the local Windows PC (Cloud Claude cannot do these)**
- Anything in the Steam Frame: shader correctness in SPI stereo, comfort, real VR frame timing, controls.
- Running Unity at all, if the cloud environment has no Unity 6000.3.9f1 Windows editor and licence. That covers compile checks, tests, screenshots, builds, the benchmark and the smoke test.
- Re-running the Blender scripts (needs Blender 5.2; Cycles bakes use the AMD GPU via HIP, though CPU also works but is slower).
- `um`/fal generation (needs `FAL_KEY` and Andrew's spending approval).
- **Never launch the PCVR build while SteamVR is running without asking Andrew.** It appears in his headset.

Realistically, Cloud Claude can write and review code and docs. Each change still needs a local Unity run to compile, render and test. Plan for that round trip, or keep changes small and well-checked.

**Repository notes**
- The generated textures and models (~52 MB) are committed directly; the largest file is 3 MB, so there is no LFS.
- `Library/`, `Logs/`, `Builds/`, `Screenshots/` and Python `__pycache__/` are ignored.
- Commits use the noreply email `237634688+rapsure777-a11y@users.noreply.github.com`.

---

## E. Validation commands (Windows, repo root)

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"

# Open / compile / import only (no scene changes). Look for "error CS" in the log.
& $unity -batchmode -nographics -quit -projectPath . -logFile Logs\compile.log

# Configure project + regenerate the scene
& $unity -batchmode -quit -projectPath . -executeMethod Gamebreak.MiniGolf.Editor.Automation.Setup -logFile Logs\setup.log

# Regenerate the world and capture review screenshots -> Screenshots\*.png, Logs\art.log, Logs\scene-stats.txt
powershell -File Tools\art-shots.ps1

# PlayMode tests (one line per test; XML in Logs\)
powershell -File Tools\run-tests.ps1
# or: & "$env:LOCALAPPDATA\Unity\bin\unity.exe" test . --mode PlayMode --output Logs/cli-results.xml --non-interactive

# Build PCVR + desktop players (about 4 min each; do not pass -nographics)
& $unity -batchmode -quit -projectPath . -executeMethod Gamebreak.MiniGolf.Editor.Automation.SetupAndBuildAll -logFile Logs\build.log
#   -> Builds\Windows\WorldsOfMiniGolf.exe (PCVR), Builds\Desktop\WorldsOfMiniGolf_Desktop.exe (no XR)

# Smoke test the desktop build (scripted putt, hole in one, advance) -> Builds\Desktop\SmokeTest\report.txt
powershell -File Tools\smoke-test.ps1

# GPU benchmark the desktop build -> Builds\Desktop\Benchmark\benchmark.txt (+ Docs\perf\benchmark_<label>.txt)
powershell -File Tools\benchmark.ps1 -Label after_pass2

# Blender assets (regenerates into Assets/_Game/Art/Generated)
$blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
foreach ($s in "bake_textures","leaf_atlas","hero_palm","hero_rocks","hero_clubhouse") { & $blender -b --factory-startup --python "Tools\Blender\$s.py" }
```
The editor menu has the same entry points: `Gamebreak/Setup/Configure Project for PCVR`, `Gamebreak/Build Tropical Scene`, `Gamebreak/Capture Review Screenshots`, `Gamebreak/Scene Stats`, and `Gamebreak/Build Windows Player (PCVR)` / `Build Desktop Debug Player`.

| Log | Path |
|---|---|
| Batch Unity logs | `Logs\*.log` (art.log, setup/build logs; `handoff_import.log` from this handoff) |
| Test results | `Logs\` (XML from run-tests / CLI) |
| Player log | `%USERPROFILE%\AppData\LocalLow\Gamebreak Labs\Worlds of Mini Golf\Player.log` |
| Session logs (strikes, holes, buttons, CPU/GPU frame times, dropped frames) | same folder, `Sessions\session_*.txt` |
| Smoke test / benchmark | `Builds\Desktop\SmokeTest\`, `Builds\Desktop\Benchmark\` |
| Unity editor log | `%LOCALAPPDATA%\Unity\Editor\Editor.log` |

---

## F. Creative direction

**Worlds of Mini Golf** is Gamebreak Labs' original PCVR mini-golf game.
- **The putting should feel like Walkabout Mini Golf:** a physical putter that follows your hand, believable rolling and rails, comfortable locomotion.
- **Each nine-hole course is set in a world inspired by a celebrated video game's environment.** The inspirations are art direction only; no assets or IP from those games or from Walkabout are used.
- Guiding principle: **"Make the golf feel familiar. Make everything surrounding it feel extraordinary."**

| # | World | Inspiration | Status |
|---|---|---|---|
| 1 | **Tropical Adventure** | Crash Bandicoot 4: It's About Time | In development; only Hole 1 authorised for the graphical pass |
| 2 | Cherry Blossom Village | Ghost of Tsushima | Not started |
| 3 | Gothic Castle | Bloodborne / Castlevania | Not started |
| 4 | Underground Kingdom | Skyrim's Blackreach | Not started |
| 5 | Hanging Gardens | Assassin's Creed Mirage | Not started |
| 6 | Lost Underwater City | Subnautica | Not started |
| 7 | Enchanted Labyrinth | Elden Ring | Not started |

**World 1 direction (Crash 4 inspired):**
- Lush, saturated, chunky-stylised jungle island.
- Sandstone mesas and sea arches, layered jungle depth, palms with big expressive fronds.
- Tiki architecture with carved totems and lanterns, turquoise lagoon, waterfalls.
- Warm sun with soft atmosphere, and rich distant scenery.

**The priority is environmental geometry and graphical fidelity substantially beyond Walkabout's,** while keeping the existing putting mechanics, controller mapping, scoring and locomotion exactly as they are. Gamebreak will spend some performance headroom for a substantial visual improvement, provided VR stays consistently comfortable: stable frame pacing, native refresh where practical, and LODs and quality settings where needed.

**Scope rules:**
- Only World 1 is authorised, and only Hole 1 for this graphical pass.
- **Do not begin Holes 2–9 before the visual benchmark is approved.**
- No multiplayer, no other worlds.

---

## Executive summary for ChatGPT HQ

**Accomplished (Graphical Pass 2, partial)**
- A reusable, scriptable Blender asset pipeline. It produced:
  - nine tileable PBR texture sets;
  - a 14-cell foliage atlas;
  - three hero palms;
  - six sandstone hero formations (two mesas, sea arch, 16 m cliff wall, two boulder groups);
  - a detailed tiki clubhouse with carved totems, lanterns and a 3D sign.
- New URP shaders (terrain splatting, triplanar rock, waterfall), plus upgraded lit and water shaders (normal and mask maps, cut-out leaves, translucency, depth-based water).
- Unity importer rules, a `HeroKit` that builds the materials, leaf-card foliage and model placement, and terrain splat weights.
- A GPU benchmark with a recorded baseline (1.65 ms median GPU).
- Everything is preserved on `graphical-pass-2-wip`.

**What remains**
- All scene integration: hero assets into Hole 1, splat terrain, turf/rails, waterfall/pool/mist, URP HDR/depth/post-processing, quality presets.
- Then visual iteration, tests, builds, benchmark, before/after screenshots and docs (section C, steps 1–8).

**Actually tested**
- The editor scripts compile (batch import, 0 errors).
- The generated textures were reviewed as images.
- Nothing else in Pass 2 has been tested: no rendering, no test run, no build, no headset.
- The playable game on `main` (`9001494`) remains the verified baseline: 41/41 tests, builds and smoke test pass. Gameplay and controls were headset-verified up to `f55ede1`.

**Broken or unverified**
- The ocean on this branch most likely renders wrongly until the depth/opaque textures are enabled.
- All new shaders, materials, models and HeroKit are unverified in a rendered scene.
- The auto putter length and wrist watch still await a headset test.

**Recommended Cloud Claude starting point**
- Check out `graphical-pass-2-wip` and read sections B–D.
- Begin with C1–C3 (HeroKit wiring, splat terrain, turf/rails) and C6 (URP settings, because the water depends on it). These are small, mechanical code changes.
- Then C4–C5 (dressing and waterfall).
- Every change needs a local Unity run on Andrew's PC to compile, screenshot and test. Unless the cloud environment has a Windows Unity 6000.3.9f1 editor, schedule local verification between cloud sessions.

**Decisions needed from Andrew**
1. **FAL_KEY / paid generation:** approve or decline about $5–15 of fal generation (to confirm with `um fal price`) for AI-assisted hero textures and models, or stay fully procedural (current approach, $0).
2. **Performance target:** native 120 Hz versus accepting 90 Hz on the Steam Frame if the richer scene needs it. This decides how far HDR, post-processing, shadows and foliage density can go.
3. **Who verifies:** whether local Claude/Codex or Andrew runs the Unity compile/test/screenshot round trips for Cloud Claude's changes.
4. Still open from the first checkpoint: rail style (wood, bamboo or stone per hole), and approval of Hole 2's draft layout.
