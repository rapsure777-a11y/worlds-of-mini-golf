# Local validation: Milestone 3

Local Claude, on Andrew's PC (RX 7900 XT, Unity 6000.3.9f1, Blender 5.2). One section per Cloud Claude checkpoint.

## M3 checkpoint 1 + 1b (`e5e7e64`)

| Step | Result |
|---|---|
| Merge check | **`MusicPlayer.cs.meta` was deleted in the merge** (`6219263`/`345291f`). Restored the original (GUID `9bd4559…`) so scene references stay stable. No other `.meta` lost. |
| Blender `hero_jungle.py` | **Ran first time.** `HeroJungleTree_0/1.fbx`, `HeroVines_0.fbx` exported. |
| Compile | **0 errors** |
| Scene + rich screenshots | Pass: "Built … with 3 hole(s)". Clubhouse and waterfall unchanged. No `not found` / `Removed dressing` / exceptions. 24 shots, including `hole03_*`, the bridge and `archipelago_*`. |
| Scene stats (LOD0) | 1,047 renderers, **3,144k triangles** (2,493k shadow-casting), up from 2,059k after Pass 2 round 2 |
| Builds | PCVR 258 MB, desktop 254 MB, 0 errors |
| Smoke test (desktop) | **PASS** (Hole 1 in one, advances to Hole 2) |
| Benchmark, rich (desktop offscreen proxy) | GPU median **2.78 ms**, p95 3.02 ms (Pass 2 rich: 2.26 / 2.46) → `Docs/perf/benchmark_m3_cp1_rich.txt` |
| PlayMode tests | First run 63 passed, **5 failed**; after the fixes below: **68/68 pass** |
| Headset | Pending (Andrew) |

### Visual findings for Cloud Claude
**Good:**
- The archipelago reads well: two islands and a pier across the channel.
- The Jungle Island canopy is dense and layered, with hanging vines.
- The bridge spans the ravine with water below; the torches and hole sign work.

**To fix:**
1. **The cliff wall behind the landing pad shows its flat side/back to the tee** (`hole03_a_tee`: a large striped hexagonal slab). This is the Pass 2 cliff-wall problem again: rotate it so the terrace face points at the player, or sink and back it with mesas.
2. **The ravine walls are plain stretched grass** (`hole03_a_tee`, `hole03_g_bridge_side`). They need rock/sandstone splat on the steep walls, or hero boulders along the rims. The rock rule may not trigger because of how the channel is built.
3. **A rectangular seam in the sea** around the Starting Island (`archipelago_aerial`): a square seabed-tint boundary at that island's terrain extent. Probably not visible at player height; check `skipDeepSea`/extent.
4. ~~The ravine reads as a ditch~~: fixed locally (lane blend, see below). A deeper or rockier ravine is still welcome.

### Test failures and local fixes
| Test | Cause | Fix |
|---|---|---|
| `TropicalScene_HasJungleIsland_WithHoleThreeOnABridge` (1.59 m of ravine under the deck; needs at least 2 m) | **Real terrain issue.** The lane zones at both bridge ends had a 2.2 m feather plus a 0.5 m margin, which reached across the 3.6 m span and filled the ravine under the deck | `JungleIsland.ConfigureTerrain`: lane zone feather 1.0 m, margin 0.3 m. The ravine now shows a clear drop with water and the bents below the deck (`hole03_g_bridge_side`) |
| `TitleCard_Texts_AreFormattedForTheCard` | `AreaTitleCard.Spaced` put 4 spaces between words (3 appended plus the letter's own trailing space) | Append 2 for a word gap, giving 3 between words |
| `Scorecard_HasVolumeSliders_ThatAControllerTipCanDrag` | Test bug: the scorecard starts inactive and builds its sliders in `Awake` on first show | The test opens the scorecard with `VRRig.ToggleScorecard()` first (as pressing X does). Runtime behaviour was already correct |
| `Music_UserScaleMultipliesTheWorldLevel_Live`, `TrackEnd_LoopsByCrossfadingIntoItsStart` (Local's) | Test bug: a new `MusicPlayer` starts with its 4 s fade-in; a later `PlayFor` on the same clip correctly doesn't restart, so the tests read levels mid-fade | New `MusicPlayer.FadeInSeconds`; the tests set 0.05 s. In-game fade-in unchanged (4 s) |

After the fixes: scene rebuilt, both players rebuilt (0 errors), smoke test PASS.
