# Local validation of the cloud integration pass (handoff section G5)

Run 2026-10-03 by local Claude Code on Andrew's PC (RX 7900 XT, Unity 6000.3.9f1), on `cloud/graphical-pass-2-integration` @ `5aeb390`. SteamVR was running, so nothing that starts OpenXR was run (see step 4).

| G5 step | Result |
|---|---|
| 1. Compile | **Pass.** 0 `error CS`, 0 `Shader error` (batch import with `-nographics`). Shaders then rendered without errors in steps 2 and 5. |
| 2. Regenerate + capture | **Pass after one fix** (below). Balanced, rich and lean all build and capture. `[Gamebreak] Waterfall: lip y 5.99, pool water y 0.91, sheet raycast-fitted.` No `Removed dressing` warnings, no exceptions. |
| 3. Visual check | Done, desktop screenshots only. See findings below. |
| 4. PlayMode tests | **Not run.** The editor test run starts OpenXR, which connects to SteamVR (`Runtime Name: SteamVR/OpenXR` in earlier `Logs/tests.log`), and SteamVR was running. Waiting for Andrew's go-ahead or for SteamVR to be closed. |
| 5. Builds | **Pass.** PCVR 167 MB, desktop 164 MB, 0 errors (previously 118/115 MB). |
| 5. Smoke test (desktop build) | **PASS.** 3.24 m/s strike, hole in one on Hole 1, advanced to Hole 2. |
| 5. Benchmark | See below. |
| 6. Headset | Not done (needs Andrew). |

## Fix made
**All hero rocks rendered flat white** (sea arch, cliff wall, mesas, boulders). Each rock FBX holds a single object, and Unity collapses it onto a root named after the file (`HeroMesa_0`). That loses the `__Rock` suffix, so `HeroKit.Instantiate` fell back to the white `Paint` material. Fixed in `HeroKit.cs`: the code now falls back to the mesh name (which keeps `HeroMesa_0__Rock`) and logs a warning if neither name has a suffix. After the fix the rocks show the banded sandstone with grass tops.

## Scene stats (balanced)
525 mesh renderers (489 static-batched), 78 unique meshes, 24 materials, 484k vertices, **750k triangles** (700k shadow-casting).

For comparison, `main` had 566 renderers, 13 materials and 585k triangles.

## Benchmark (desktop build, offscreen 4320×2160, 4× MSAA, 4 Hole 1 views)
| | GPU median | GPU p95 | CPU main median (tee) |
|---|---|---|---|
| Before Pass 2 (`main`) | 1.65 ms | 1.86 ms | 2.00 ms |
| Pass 2, lean | 1.70 ms | 1.86 ms | 2.21 ms |
| Pass 2, balanced | 2.25 ms | 2.40 ms | 2.74 ms |
| Pass 2, rich | 2.26 ms | 2.46 ms | 2.76 ms |

Files: `Docs/perf/benchmark_after_pass2_{lean,balanced,rich}.txt`.

Caveats:
- The benchmark does not record the active preset, so it is not proven that rich was fully applied.
- The offscreen target is always an HDR format, so HDR's cost is not measured.
- Compositor and streaming overhead is excluded.
- All presets are far inside the 8.33 ms (120 Hz) budget on this proxy. The real VR number must still come from a headset session log.

## Visual findings for HQ / Cloud Claude (not fixed; these need art decisions)
Screenshots: `Logs/shots_{balanced,rich,lean}/` (local only; `Logs/` is git-ignored).
- **Better:**
  - hero palms (dense fronds, coconuts);
  - the sea arch;
  - splat terrain (rippled sand, pebble path, soft blends);
  - the clubhouse thatch;
  - the waterfall on the cliff;
  - depth-tinted lagoon in balanced/rich; the lean water fallback looks fine too.
- **Worse than `main` at the tee:** the flowering bushes and lane-side beds are gone. The tee view is now plainer than the first checkpoint, and the promised layered jungle is not visible from the player's main viewpoints.
- **Hero boulders** beside the tee and at the cup end read as flat, half-buried slabs (too much sink or squashed). They look worse than the old rocks.
- **HeroCliffWall** reads as a huge tilted slab with a straight bottom edge floating above the ground in the from-cup and side views. It probably needs to be sunk or rotated, or replaced by stacked mesas.
- Old kit palms and rocks still sit next to the new hero ones, which mixes the two styles.
- Rich vs balanced: only a slightly stronger grade; bloom is barely visible.

## Second fix: the green was waving (reported by Andrew)
Andrew saw the putting green moving like water. Cause: `HeroKit.Surface()` gave every textured material a vertex wind sway of 0.04 m. Green and rail meshes have no vertex colours, so the shader read their wind weight as 1, and the whole turf and the rails rippled while the collider stayed still. The clubhouse wood, bamboo, thatch, totems and lanterns swayed the same way.

Fix: wind is now 0 by default. Only `Hero_Bark` (0.04; palm trunks carry a baked 0 to 0.2 weight) and `Hero_Leaves` (0.07) sway. New PlayMode test `TropicalScene_CourseSurfacesDoNotSway` fails if any material under a hole has non-zero wind. It compiles but has not been run yet (SteamVR). Both players rebuilt (0 errors).

## Round 2 validation (Cloud Claude `4b6ed28`)
| Step | Result |
|---|---|
| Compile | 0 errors |
| Scene build + rich screenshots | Pass. `Clubhouse site: hole-local (-1.8, -6.8)`; waterfall raycast-fitted (lip y 4.41). No dressing removed from greens. |
| Visual | Clubhouse now sits behind the tee, clear of the cliff, pool and waterfall, and framed at the end of the lane from the cup. Old kit rocks are gone (sandstone stacks and boulders everywhere). The waterfall reads cleanly. |
| Scene stats (LOD0 worst case) | **2,059k triangles (1,601k shadow-casting)**, up from 750k / 700k. Distant LODs should reduce what is drawn; VR timing will tell. |
| Builds | PCVR 207 MB, desktop 204 MB, 0 errors |
| PlayMode tests | **42/42 pass** (SteamVR closed) |
| Desktop smoke test | PASS |
| VR probe (`Tools\vr-probe.ps1`) | Pending: needs Andrew in the headset at 90 Hz, then 80 Hz |
