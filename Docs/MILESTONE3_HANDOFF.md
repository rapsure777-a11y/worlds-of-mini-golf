# Milestone 3 handoff (Cloud Claude -> Local Claude)

Running document, one section per checkpoint. Brief: `Docs/MILESTONE3_BRIEF.md`; baseline: `Docs/MILESTONE3_BASELINE.md`.
**Everything below is UNVERIFIED**: written without Unity or Blender. Syntax-parsed only (tree-sitter). Nothing compiled, rendered, tested, built or measured.

---

## M3 checkpoint 1: cluster music, archipelago framework, Jungle Island + Hole 3 "Jungle Crossing", Hole 2 -> 3 transition

### Run first (Blender, local)
`blender -b --factory-startup --python Tools/Blender/hero_jungle.py` (run from the repo root; output `Assets/_Game/Art/Generated/Models`):
`HeroJungleTree_0/1.fbx` (buttressed trees with branching canopy and vines) and `HeroVines_0.fbx`. The world builder **skips these with a warning if missing** (hero palms stand in for the trees), so the scene still builds without them. Material suffixes follow the existing convention (`__Bark`, `__Leaves`). Expected: trees 11.5 m / 9.5 m; vertex alpha = wind weight; origin at the tree base, vines hang down from the origin. If the script errors, tell Cloud Claude (it was written blind against `gblib.py` and `hero_palm.py`).

### Then, in order
1. Compile (`error CS`, `Shader error`).
2. `Tools\art-shots.ps1 -Quality rich` (new shots: `hole03_*` including `_g_bridge_side`, `_h_bridge_from_elbow`, `_i_ravine_below`, plus `archipelago_aerial`, `archipelago_from_start_island`). Log lines to look for: `Clubhouse site:` (Hole 1, unchanged), `Waterfall: ... raycast-fitted`, `Hero model '...' not found` (Blender not run yet), `Music ... not imported yet` (expected until Andrew's files exist).
3. `Tools\run-tests.ps1` (SteamVR closed): Local's `MusicTests` + the existing suite + new `Milestone3Tests` (16) + the renamed `TropicalScene_ProgressesThroughAllHolesAndFinishes` (now plays 3 holes).
4. Builds, smoke test (it plays Holes 1-2: check it still advances, and that hole 3 follows), benchmark.
5. Headset with Andrew: bridge feel, the fade into the Jungle Island, crossfade (needs `JungleTheme.ogg`).

### Music (already imported by Local Claude, commit `8ac0042`)
All five tracks are in `Assets/_Game/Audio/Music` (`IslandExploration`, `JungleTheme`, `TempleTheme`, `VolcanicTheme`, `SummitTheme`). Rebuild the scene (`Automation.Setup`) so `WorldTheme.musicClusters` is regenerated from `TropicalCourse.Clusters()`; the Jungle cluster then plays from hole 3 with a crossfade (3 s, plus the 2.5 s loop crossfade).

### What was built
| Area | Files | Notes |
|---|---|---|
| Cluster data | `Course/IslandCluster.cs`, `Course/HoleDefinition.cs` (`TropicalCourse.Clusters()`, `ClusterOf`, `HoleDefinition.cluster`) | `start` = holes 1-2, `jungle` = holes 3-4 (Hole 4 "Hollow Drop" built, see DEVELOPMENT_LOG.md). Clusters hold id, name, hole numbers, music file name, island centre/radius. Reusable by every world |
| Music | Local's `Feedback/MusicPlayer.cs` + `WorldTheme.musicClusters` (adopted; my duplicate `MusicDirector` was dropped in the merge with `8ac0042`), `Feedback/GolfAudio.cs` (new) | `SceneBuilder` now derives `theme.musicClusters` from `TropicalCourse.Clusters()` (one source of truth for hole grouping and track names; Temple/Volcanic/Summit are registered as data only). `GolfAudio.MusicScale` (default 1, max 1.8) multiplies the world's 0.55 level live in `MusicPlayer` (default behaviour and Local's tests unchanged); `GolfAudio.SfxVolume` (default 1) scales every effect in `GolfFeedback`. Desktop keys (in `MusicPlayer`): F5/F6 music -/+, F7/F8 effects -/+, saved in PlayerPrefs. **No VR settings UI yet** (decision below) |
| Transition | `UI/TransitionFade.cs` | When the next hole is in another cluster: fade to black 0.5 s just before the teleport, fade in 0.9 s after. Unlit quad on the camera using `Gamebreak/Mist` |
| Surface | `Course/CourseGeometry.cs`, `HoleFactory.cs`, `WorldTheme.deck`, `GreenLayout.deckAreas` | A third submesh with the bridge-deck material (`Hero_Deck`). Same collider/physics; rails unchanged. Existing holes unaffected (2 submeshes) |
| Hole 3 | `HoleDefinition.cs` `Hole03()` | Par 3, 12 cm rails. Lane: A tee lane (1.2 x 3.4 m, +z) -> B wide elbow (4.0 x 2.4 m) -> **bridge** (3.6 x 1.2 m along +x, crest +0.18 m, i.e. needs about 1.6 m/s to cross) -> D landing pad (3 x 3 m) -> final lane (1.2 x 2.8 m) with a 2.5% lean toward the east rail. The cup (9.4, 8.5) is not in line from the pad: aim up the pad's east side or bank off the lane's east rail (test: `Hole3_BankShotOffTheLaneRail_EntersTheFinalLane`). A soft putt rolls back into the elbow without penalty; leaving the course costs the usual +1 and returns the ball |
| Archipelago | `Editor/Art/TropicalWorld.cs` (`Build(kit, hero, defs, clusters, parent)`) | One `IslandGen` per cluster, one `Dresser` per island, shared dressing root; one ocean for the whole archipelago (polar mesh centred between islands, 110 x 220, depth = shallowest island). Starting Island generation is unchanged |
| Jungle terrain | `Editor/Art/IslandGen.cs`, `JungleIsland.cs` | `IslandGen` gains `channels` (ravines: trench with flat floor and end taper), plateau zones, `Zone.heightFn` (lane areas follow the hole's height function), `skipDeepSea`. Jungle Island: centre (64, -62), radius 25, hills 6.5 m, ravine along hole-local z at x = 5.2 (half-width 3.5 m, 3.3 m deep) draining to the sea so real water sits under the bridge. Floor and walls splat to sand/rock |
| Bridge | `JungleIsland.BuildBridge` | Procedural (no Blender): stringers, floor beams, three bents with posts to the ravine floor, rungs, X braces, rope handrails with sag; `Hero_Wood`/`Hero_Thatch` with world-scale box UVs. Visual only, no colliders, follows the hump |
| Jungle dressing | `JungleIsland.DressHole3`, `DressIsland` | Name board and hole sign, torches at the bridge ends, canopy trees (hero trees or palms), ravine-rim boulders, terraced cliff wall behind the landing pad, mesas, hanging vines, five layers of ground cover/shrubs/bananas/big leaves, shoulder foliage. Foliage stays 0.9 m clear of every lane area |
| Pier | `JungleIsland.BuildPier` | Boardwalk across the channel between the islands with torches (visual/teleport-walkable). Players are carried between islands by the hole transition (fade + teleport), so nothing can strand them |
| Tools | `Automation.cs`, `FrameCostProbe.cs` | New capture shots; the probe treats `IslandTerrain*` |
| Tests | `Tests/PlayMode/Milestone3Tests.cs` | Cluster data and music names, user music scale + volume independence, Hole 3 layout (connectivity, bridge width, no cliffs, hump, lean), bridge physics (firm putt crosses; +-14 deg putts stay on the deck; soft putt rolls back; final-lane cup; bank shot), scene wiring (3 materials on the surface, 2 m of ravine under the deck,  |

### Design choices to review
- Hole 3 faces east (yaw 90) so the player looks away from the Starting Island and can look back at it. Origin (46, 2.6, -56).
- Bridge hump kept low (0.18 m) so the crossing is a read, not a barrier; rails raised to 12 cm for the bridge's recovery.
- The ravine reads as a tidal inlet (ocean plane shows through where the floor is below sea level): no separate water mesh.
- New meshes use the `Hero_` prefix (`Hero_BridgeWood`, `Hero_BridgeRope`); the Jungle terrain asset is `IslandTerrain2` (Pass 2 lesson).

### Known risks / likely first fixes
- Compile: `JungleIsland.cs` (tuple-array foreach deconstruction, local functions), `IslandGen` channel code, `MusicPlayer` (scale/hotkeys), `TransitionFade` (`Configure` signature), `Milestone3Tests`.
- The plateau/lane zones and the ravine are tuned blind: check the ravine walls (steepness, rock splat), the abutment gap where lane B/D meet the deck, and that the tee area is not on a steep embankment. Tunables: `RavineHalfWidth/Depth`, plateau `feather`/`height`, lane zone margin in `JungleIsland.ConfigureTerrain`.
- Bridge supports: posts run to `Ground()` - 0.3 m; if the floor is under water they should still read as piles.
- Jungle tree/vine FBX orientation and scale are unchecked (same conventions as the palms).
- `TransitionFade` quad: should be invisible when alpha is 0 (renderer disabled). In VR confirm it fades in both eyes and does not clip.
- Frame cost: the Jungle Island adds terrain (skipDeepSea trims it), about 300 leaf-card objects (each with a cull LODGroup), 3 hero rocks groups and 9 trees. No LOD for trees yet. Compare `Logs/scene-stats.txt` with the 2.06M-triangle Pass 2 figure.

### Addendum to checkpoint 1 (Andrew's decisions): area title cards and VR volume controls
Both are UNVERIFIED like the rest.

**Area title card** (`UI/AreaTitleCard.cs`, `UI/TitleArt.cs`, data on `IslandCluster`: `tagline`, `accent`)
- Shown on first arrival (hole 1) and whenever the next hole is on a different island: Starting Island, Jungle Island, (planned) Temple, Volcanic, Summit. Not shown between holes of one island.
- Look: a 2.7 m-wide world-space card about 3 m ahead and 0.55 m above eye level, placed once at arrival (it does not follow head turns): world name in letter-spaced accent capitals, the island name in 128 pt ivory serif bold with a soft drop shadow, an ornamental divider (hairline fading outward with a hollow diamond and two dots) that draws outward from the centre, an italic tagline, and "HOLES 3 - 4"; a dark soft-edged banner and a radial glow tinted with the island's accent behind it. All art is generated at runtime (`TitleArt`), no assets. Timeline: 1.1 s delay (so the fade from black finishes), 1.4 s fade-in while settling from 107% to 100%, 3.8 s hold, 1.6 s fade-out while drifting up 22 cm. A soft C-E-G-C bell arpeggio plays (synthesised, scaled by the effects volume).
- Taglines/accents: set in `TropicalCourse.Clusters()` (e.g. Jungle: "Deep green, and deeper secrets", green accent). Font: an installed serif (Georgia, then Palatino Linotype, Book Antiqua, Times New Roman) via `Font.CreateDynamicFontFromOSFont`, else Unity's built-in; set `TitleArt.UseOsFont = false` to force the built-in.
- To judge locally: desktop capture shows it only mid-animation, so check in the headset (or play the desktop build and advance with N). Tunables are constants at the top of `AreaTitleCard` (`CanvasW/H`, `UnitScale`, timings, distance/height in `Place()`).

**VR volume controls on the scorecard** (`UI/ScorecardVolumeControls.cs`, small hooks in `VRRig`, `ScorecardPanel` is taller)
- Press X (Frame X / off-hand primary) as usual: below the scorecard there are **Music** and **Effects** sliders. Touch a slider with the tip of either controller (about 8 cm in front of the grip) and **hold the trigger** to drag; releasing the effects slider plays a short blip so the level can be judged. Haptic ticks on touch and at each 5% step. The card holds still (no follow, no auto-hide) while a hand is within 50 cm of it.
- The music slider shows the **audible** level: the world's 0.55 times the user scale (default shows 55%, max 99%). The effects slider is 0-100%. Both persist (`GolfAudio`, PlayerPrefs). Desktop keys remain F5/F6 and F7/F8.
- No existing mapping changed: the trigger is only read for the sliders while a fingertip is on one; the card only moves differently while pinned. New `VRRig` members: `ScorecardPinned`, `IsTriggerPressed(hand)`, `HapticFor(hand, amp, dur)`.
- Check in the headset: reachability and size of the sliders at the card's distance (1.1 m, 25 cm below eye height), that the fingertip offset feels right (`hand.forward * 0.08f` in `Update`), and that the hover highlight reads. Tunables: `RowTop`, `TrackLeft/Right`, `TouchDepth`, `PinDistance`.

**Tests added** (`Milestone3Tests`): slider mapping; dragging both sliders with a simulated fingertip (including no change when hovering 20 cm away or without the trigger); card text formatting; every cluster has a tagline and a readable accent; the card appears on arrival, not for hole 2, appears for hole 3 with the right text, becomes fully visible, is not raycast-blocking, and leaves on its own.

### Decisions for Andrew / HQ
1. (Done) VR volume control on the scorecard and a title card per area, as requested. Please judge size, timing and the serif font in the headset.
2. Whether the title card should also appear when *returning* to an earlier island (it does if a hole ever goes back; none do now).
