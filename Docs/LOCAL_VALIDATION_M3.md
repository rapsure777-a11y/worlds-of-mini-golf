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
| PlayMode tests | **Pending**: SteamVR was open (tests start OpenXR) |
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
4. The ravine reads more as a ditch than a "dramatic" ravine from the tee. Consider more depth or steeper rock walls (brief section 5: "visible terrain depth beneath the elevated crossing").
