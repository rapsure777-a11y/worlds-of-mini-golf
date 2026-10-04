# Universal Modder (`um`) integration opportunities for World Forge

Status: **nothing was generated and nothing was purchased in Pass 2.** `FAL_KEY` is not configured; no paid call was made or attempted. This is a planning note only. Any use needs Andrew's approval and a `um fal price` quote first, and every AI asset must be logged in `ASSETS.md` with model, prompt, date and licence.

Candidate uses, roughly in order of value for the Pass 2 look (recipes from the handoff, B7; prices unconfirmed):
| Opportunity | Recipe | Replaces / augments |
|---|---|---|
| Tileable terrain/rock/bark textures with more painterly variety | `fal-ai/z-image/turbo/tiling` | Procedural Blender bakes for sand, sandstone, bark |
| PBR maps from a hero albedo | `fal-ai/patina/material` | Numpy-derived normal/mask maps |
| Hero props (idols, ruins, statues, boats) | `fal-ai/trellis-2`, Hunyuan 3D v3.1 Pro | Hand-scripted Blender props; needs retopology, UV and AO bake before import |
| Concept/reference boards per world | nano-banana-2 | Art direction for the other six worlds |
| Skybox / distant-scenery plates | image models | Procedural sky, kit distant islands |

Integration points that already exist: drop generated textures into `Art/Generated/Textures` with the `<set>_{albedo,normal,height,mask}` names (the importer and `HeroKit.Surface` pick them up); drop models into `Art/Generated/Models` with `__Suffix` object names. Suggested guard rails: a `Tools/um-generate.ps1` wrapper that requires an explicit `-Approved` flag and writes a provenance JSON beside each output; generated meshes must pass a triangle budget and the normal import/orientation checks.

Not recommended yet: generating anything before Hole 1's procedural pass is verified in Unity, since it is unknown which surfaces will actually need replacing.
