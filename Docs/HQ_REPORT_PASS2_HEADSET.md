# HQ report: Graphical Pass 2, first headset session

2026-10-03. Branch `cloud/graphical-pass-2-integration` (Cloud Claude's integration plus local fixes). Played by Andrew on the Steam Frame; checks run by local Claude Code on the Windows PC.

## Andrew's verdict
- "Works great. Works awesome, looks a lot better."
- At 90 Hz: "Feels very smooth."
- **The sea arch is the benchmark:** "If we could have the fidelity of that arch throughout the whole level, that would be amazing."

## What was tested

| Check | Result |
|---|---|
| Compile | 0 errors |
| Scene build + screenshots (lean / balanced / rich) | Pass |
| PCVR + desktop builds | Pass, 0 errors (167 / 164 MB) |
| Desktop smoke test | PASS (scripted putt, hole in one, advances to Hole 2) |
| Headset play, Holes 1–2 | **Played and confirmed by Andrew.** Putting, tracking and progression normal. |
| PlayMode tests | **42/42 pass** (incl. new `TropicalScene_CourseSurfacesDoNotSway`; run with SteamVR closed) |

## Bugs found and fixed this session (local)
1. **All hero rocks rendered white.** A single-object FBX loses its `__Rock` suffix on the root, so the white fallback material was used. `HeroKit` now reads the suffix from the mesh name.
2. **The putting green "waved like water"** (Andrew). Every textured material had 0.04 m vertex wind, and the green and rails have no wind weights, so the whole turf rippled. Wind now defaults to 0; only palm bark and leaves sway. New test `TropicalScene_CourseSurfacesDoNotSway`.
3. **Big plants rendered orange/white/red stripes** (Andrew). The new foliage meshes were saved with the same file names as the old kit's `BigLeaf0/1` and overwrote them. They are now saved as `Hero_*.asset`; the old meshes match `main` again. **Headset-confirmed by Andrew: "Plants are right now."**

## Open issues (not fixed)
- **The waterfall clips into the tiki clubhouse**, seen from Hole 2. The clubhouse sits almost on top of the cliff, pool and waterfall, and its surfboards crowd Hole 2's tee. It needs relocating.
- ~~Distant plants look white~~ **Resolved** by the foliage file-name fix (Andrew: "The plants aren't white anymore").
- ~~Tee view plainer than the first checkpoint~~ **Accepted by Andrew** ("The tee area is fine"); no change needed.
- **Hero boulders read as flat slabs, and the cliff wall floats** above the ground in places.
- **Old kit palms, rocks and mesas still sit beside the new hero assets**, mixing two styles.

## Performance and headset configuration
**Headset refresh modes** (SteamVR vrlink driver log): **72, 80, 90, 96, 108, 120, 144 Hz**. SteamVR was set to 120 Hz all day; Andrew switched it to **90 Hz** for the last run. SteamVR supersampling is a manual 1.0×. The per-eye render target is 2160×2160 (Single Pass Instanced).

Session-log results (Steam Frame, RX 7900 XT, Ryzen 5 9600X):

| Build / preset | Refresh set | Frames per min | Frame interval median / p95 | Time at full rate | CPU main thread median / p99 |
|---|---|---|---|---|---|
| Placeholder build, this morning (before the art) | 120 | ~7,200 | 8.3 / 8.3 ms | ~100% | n/a |
| `main` (art showcase, before Pass 2) | 120 | ~4,700 | 16.7 / 16.7 ms | ~47% | 1.0–1.1 / 1.8–2.5 ms |
| Pass 2, lean | 120 | ~4,880 | 8.3 / 16.7 ms | ~52% | n/a |
| Pass 2, balanced | 120 | ~4,800 | 8.3 / 16.7 ms | ~50% | n/a |
| Pass 2, rich | 120 | ~4,800 | 8.4 / 16.7 ms | ~50% | n/a |
| **Pass 2, rich** | **90** | ~4,770 | **11.1 / 22.2 ms** | **~87%** (rest at 45 Hz) | 1.1–1.6 / 2.4–2.7 ms |

Desktop offscreen benchmark (4320×2160, 4× MSAA), GPU median / p95:

| Build / preset | GPU median | GPU p95 |
|---|---|---|
| Before Pass 2 | 1.65 ms | 1.86 ms |
| Pass 2, lean | 1.70 ms | 1.86 ms |
| Pass 2, balanced | 2.25 ms | 2.40 ms |
| Pass 2, rich | 2.26 ms | 2.46 ms |

**Interpretation:**
- **Pass 2 did not cause the frame-rate drop.** The `main` showcase build drops the same way.
- Every session since the art showcase delivers about 80 frames per second, whatever the refresh rate, preset or build. Something costs about 12 ms per frame in VR: GPU rendering or the stream encode for the Frame. The desktop benchmark (about 2 ms) does not capture it.
- The CPU is not the limit (main thread about 1–2.7 ms at p99).
- **The game's "GPU (Unity timer)" figure is not valid in VR.** It tracks the frame interval (it read 8.2 ms on the locked-120 build and 12.3 ms on all today's builds). The OpenXR runtime GPU metric reads 0, so it is unavailable. We need a real per-frame GPU measure: SteamVR's frame timing, or GPU timestamp queries.
- SteamVR's compositor log shows almost no dropped frames and 0 reprojected. The half-rate frames are delivered cleanly, which matches Andrew's "very smooth" at 90 Hz.

**Against the revised target (stable 90 Hz on the high-quality preset):**
- **Close but not yet met:** about 87% of frames at 90 Hz and about 13% at 45 Hz.
- Options:
  1. Find the roughly 12 ms per-frame cost; recommended first.
  2. Use 80 Hz as the high-quality refresh, which is supported and likely stable today.
  3. Keep 90 Hz and trim distant-only cost (LODs on hero palms and rocks, foliage distance culling). Per the revision, don't cut near-field detail.
- 120 Hz stays available as a performance mode, but no preset currently reaches it.

## Recommended instruction for Cloud Claude
> Use **HeroSeaArch as the fidelity standard** for every rock, cliff and landform visible from Hole 1. Replace the remaining old kit rocks, cliffs and mesas. Make the boulders and the cliff wall sit naturally on the terrain. Move the tiki clubhouse so it does not clip into the waterfall or crowd Hole 2. Add simpler distant versions (LODs) of hero palms and rocks. Add a real VR GPU timing measurement to the session log. Target a stable 90 Hz on the rich preset (Andrew sets SteamVR to 90 Hz), with 120 Hz as an optional performance mode. Scope stays Hole 1 only (Holes 2–9 still need visual approval).

Cloud Claude cannot run Unity, Blender or the headset. Each iteration needs a local round trip on Andrew's PC: compile, screenshots, tests, build and a headset check.

## Decisions for Andrew / HQ
1. Should the high-quality preset run at 80 Hz if 90 Hz can't be locked, or keep pushing for 90?
2. Should arch-level fidelity extend island-wide now (still Hole 1 only for golf), or stay limited to what is visible from Hole 1?
3. Is paid fal generation (about $5–15, `FAL_KEY` not set) still declined? The Blender route produced the arch at $0.
