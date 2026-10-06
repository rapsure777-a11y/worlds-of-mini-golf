# Graphics Pass 3: Tropical Adventure visual quality pass

Branch `feature/graphics-pass-3-tropical-adventure`. **Gameplay geometry is untouched** (routes, cups, banks, bowls, bridges, hazards, collision). Nothing here was compiled or run in Unity: Cloud has no Unity, Blender or GPU. Local must validate (see "Local steps").

Art direction: `Docs/ArtDirection/GraphicsPass3/01..08*.png`.

## What changed
| Area | Change |
|---|---|
| Putter | Shaft and grip untouched. The "Head" cube stays as the strike box (transform, `Tuning.headSize`) with its renderer removed; a visual child `HeadVisual` (`GolfVisualMeshes.PutterHead`) fills exactly the box: chamfered satin steel, bright bevels, dark milled insert, white alignment line, tapered hosel with ferrule. |
| Cup | Pit mesh, collider and `Cup` mechanics unchanged. New liner material (`Gp3_CupLiner`) and a visual-only metal rim ring (`CupRim`, flush, outside the pit, no collider). |
| Turf | Retuned vivid generated turf texture (`gp3_turf_*`), tiling 1.6. |
| Rails | Generated stone-block texture; rail UVs are now continuous world-space (geometry identical). |
| Ball | Dimple normal map; stripe and physics unchanged. |
| Materials | Generated PBR sets (albedo/normal/mask/glow) written as PNGs to `Art/Generated/Textures/gp3_*` at scene build: turf, sandstone, limestone (dry/wet), basalt with glow rims, lava, rope, sun relief, cloth banner, brushed metal, cup liner, ball dimples. Materials `Gp3_*`. |
| Shader | `StylizedLit` gained an optional `_EmissionMap` (default white, behaviour unchanged for existing materials). |
| Lighting | `BiomeAtmosphere`: per-hole presets (start, jungle, temple, volcanic, summit) easing sun colour/height, ambient, fog and a runtime copy of the sky material over about 2.2 s. |
| Foliage | `ScatterPlanner` (pure, deterministic): clustered, layered (canopy, understory, ground, flower accents), dominant species per clump, scale and yaw variance, real gaps. `Gp3Dressing.Foliage` plants per island, never inside dresser keep-outs and not within 2 m of any tee or cup. |
| Fences and props | `FencePlanner` (pure): wood posts and sagging rope outside the rails of holes 1-8 (no fence on Hole 9: rail-less identity, Altar clearance). Braziers on every other corner post at the temple holes. |
| Lava | Volcanic lava boxes and falls re-skinned with emissive `Gp3_Lava`. |
| Vista | Cloud sea ring below the Summit island. |

All steps run through `Gp3.Run` (try/catch, switch `Gp3.Enabled`): a visual failure logs and the scene still builds.

## Performance
Few new materials (about 15), all `StylizedLit`; no additional lights, no new transparency (cloud sea reuses the existing cloud material); foliage uses the existing leaf cards with cull fractions; decorative parts cast no shadows except braziers' stems. Per-object cost is higher than the old hand dressing: watch draw calls in the headset. The primitives are static-batched.

## Tests (cloud type-checked only, not run)
`GolfVisualTests` (head inside strike box, rim flush and outside the pit, `SetRim` visual-only, `CreateGreen` surface identical, world-space rail UVs, a swing still strikes) and `Gp3PlannerTests` (determinism, keep-out, layers, families, negative space, fence posts outside the green and not across joins, atmosphere presets). Planner statistics were also run for real with a small shim (`Tools/CloudTypeCheck/PlannerRun`).

## Local steps
1. Open in Unity 6000.3.9f1 and let it compile. Expect possible first-compile fixes in `StylizedLit` (`_EmissionMap`) and the new editor files; they were written blind.
2. Run the scene builder (it generates `gp3_*` textures and `Gp3_*` materials).
3. Run the full test suite (203 expected before plus the new tests). `TropicalScene_NoSceneryOnGreens` must stay green.
4. In the headset check, close up: putter head, cup rim and liner, turf, ball; rail texture seams; fence posts not in the way at tee or cup.
5. Walk each island for foliage density and framerate; check lava glow, sun/fog changes between holes.

## Visual risks
- Generated textures were previewed only as PNGs, not lit in-engine.
- Emissive lava only glows through the emission map; no real lights.
- Fence posts follow every exterior rectangle edge and may look busy next to composite holes; tune `FencePlanner.Posts` spacing/outset or drop a hole in `Gp3Dressing.Dress`.
- Palms need the Blender `HeroPalm_*` FBXs (skipped with a warning if missing).
- Not done in this pass: water shader tweaks, distant volcano/mountain silhouettes, per-biome rock material swaps (`Gp3_Rock*` exist but are not yet applied), hanging vines, banners, waterwheel and sun-wheel upgrades.
