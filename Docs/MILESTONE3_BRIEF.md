# GAMEBREAK LABS
## Worlds of Mini Golf — Milestone 3
### Tropical Adventure: Island-Hopping Course Expansion

> Committed to the repo by Local Claude on 2026-10-03 so Cloud Claude can read it. The brief below is HQ's text, verbatim. Answers to Cloud Claude's open questions are in the **Local Claude addendum** at the end; those are suggestions, not HQ requirements.

**Repository:**
https://github.com/rapsure777-a11y/worlds-of-mini-golf

**Assignment:** Expand our existing PCVR mini-golf game by developing Holes 3, 4 and 5 while establishing a reusable island-cluster system for the eventual nine-hole Tropical Adventure course.

---

## 1. Development Context

We have successfully completed Graphical Pass 2 and established a graphical quality standard that Andrew has tested and approved in the Steam Frame.

The current development version features:
- Working physical putting mechanics.
- Native Steam Frame controller support.
- Comfortable VR locomotion.
- Scoring and hole progression.
- Improved Blender-generated environmental assets.
- Hero-quality tropical scenery, including the signature sea arch.
- Rich, Balanced and Lean graphical presets.
- Background music.
- Successfully tested 90 Hz operation using the Rich graphical preset.
- 43/43 passing automated tests at the latest reported checkpoint.

The current gameplay foundation should be preserved.

**Before development begins:**

Read the latest project documentation, development log, Graphical Pass 2 handoff, asset workflow and headset validation reports.

Use the latest locally validated Graphical Pass 2 integration, not the original unfinished WIP checkpoint.

Ensure Local Claude first preserves the approved version in GitHub and establishes a stable development baseline.

Create a new branch for this milestone.

Do not overwrite the approved version or introduce unrelated changes.

---

## 2. Creative Vision

Our guiding principle remains:

**Make the golf feel familiar. Make everything surrounding it feel extraordinary.**

We want the satisfying, accessible physical putting experience associated with games like Walkabout Mini Golf, surrounded by considerably richer environments inspired by visually spectacular adventure games.

Tropical Adventure takes broad stylistic inspiration from Crash Bandicoot 4: It's About Time.

The environment should be colorful, expressive, lush, exaggerated and richly detailed.

All game assets and environments must remain original.

We are not trying to reproduce another game's proprietary locations, models or characters.

### Graphical standards

Graphical Pass 2 is now our minimum accepted production baseline.

In particular, **HeroSeaArch is our reference for the fidelity of prominent environmental geometry**.

New cliffs, architectural structures, rock formations and major landmarks should achieve comparable quality.

Prioritize:
- Strong environmental silhouettes.
- Detailed, polished geometry.
- Surface variation and material depth.
- Rich, layered vegetation.
- Convincing terrain integration.
- Attractive lighting and water.
- Substantial scenery visible beyond the putting lanes.
- A cohesive, high-quality stylized aesthetic.

Do not revert to the earlier simplistic procedural art style.

Use the established Blender and Unity asset-production pipeline.

---

## 3. Overall Course Structure — Tropical Archipelago

We are embracing the existing small-island arrangement, with Holes 1 and 2 sharing the Starting Island.

The complete Tropical Adventure course will contain nine holes distributed across an interconnected tropical archipelago.

**Island clusters should be flexible.**

An environmental cluster may contain one, two or three holes, depending on geography, gameplay progression and creative requirements.

Do not force an arbitrary number of holes onto every island.

Our current preferred arrangement is:

| Area | Holes | Environmental Theme |
|---|---|---|
| Starting Island | 1–2 | Beach, clubhouse, turquoise lagoon and sea arch |
| Jungle Island | 3–4 | Dense rainforest, ravines, bridges and hidden cove |
| Temple Island | 5–6 | Ancient ruins, elevation, stone terraces and waterfalls |
| Volcanic Island | 7–8 | Dramatic volcanic formations and hazardous-looking landscapes |
| Summit Sanctuary | 9 | Large, spectacular standalone finale overlooking the archipelago |

This is a creative direction rather than a permanently fixed geographical template.

Three-hole islands are acceptable if they improve the experience.

Hole 9 may occupy its own larger island and offer a more ambitious, extended putting challenge.

### Environmental continuity

The archipelago should feel like one connected destination rather than a series of unrelated scenes.

Players should be able to recognize distant islands, landmarks and structures as they progress through the course.

Where practical, allow players to look back toward previously completed areas.

Use comfortable, visually interesting connections between islands, such as:
- Wooden bridges.
- Piers.
- Boats.
- Stone crossings.
- Short tunnels.
- Themed teleportation transitions.

Avoid unnecessarily long walking distances or complicated navigation.

Players should always understand where to go next.

---

## 4. Current Development Scope

For Milestone 3, implement **Holes 3, 4 and 5 only**.

This means:

- Complete Jungle Island with Holes 3 and 4.
- Establish Temple Island and build Hole 5.
- Reserve appropriate geographical space for Hole 6.
- Connect the new content to the existing course progression.

Do not begin gameplay construction for Holes 6–9.

Do not redesign the existing approved Starting Island unless required for transitions.

---

## 5. Hole 3 — Jungle Crossing
### Jungle Island | Par 3

Hole 3 introduces our second island.

The player arrives in a noticeably denser, more vertical tropical rainforest.

Create an original winding putting lane crossing a small jungle ravine.

**Signature feature:** A short elevated wooden bridge incorporated directly into the playable course.

Environmental direction:

- Layered rainforest vegetation.
- Detailed jungle floor.
- Oversized tropical plants.
- Expressive palms.
- Hanging vines.
- Substantial trees and dramatic environmental silhouettes.
- Visible terrain depth beneath the elevated crossing.
- Attractive distant jungle scenery.

Gameplay requirements:

Introduce a modest directional challenge and at least one meaningful bank-shot opportunity.

The bridge must be physically integrated into the putting geometry, not simply decorative.

Make it possible to recover from imperfect shots without excessive penalties.

This should feel like the beginning of a jungle expedition.

---

## 6. Hole 4 — Turtle Cove
### Jungle Island | Par 3

Hole 4 occupies the coastal side of the same island as Hole 3.

The environment opens from dense rainforest into a secluded tropical cove with turquoise water and substantial sandstone formations.

**Signature feature:** Two viable putting routes.

**Safe route:** A longer, curved approach with forgiving geometry.

**Risk route:** A shorter, more technically demanding passage through a natural rock opening or tunnel.

Both approaches must be physically playable.

The shortcut should reward accuracy rather than depend on random ball behavior.

Environmental requirements:

- Hero-quality rock formations.
- A distinctive natural opening.
- Detailed coastal terrain.
- Attractive lagoon water.
- Tropical vegetation around the cove.
- Clear sightlines for evaluating the two routes.

This hole concludes Jungle Island.

The player should then transition naturally toward Temple Island.

---

## 7. Hole 5 — Temple Steps
### Temple Island | Par 4

Hole 5 introduces our third environmental cluster.

This should be the most ambitious and visually memorable hole of the current production milestone.

The player arrives at ancient tropical ruins partially reclaimed by the jungle.

**Signature feature:** A longer, elevated putting challenge constructed from multiple interconnected stone terraces and ramps.

Environmental direction:

- Original weathered sandstone architecture.
- Carved structures and decorative columns.
- Moss-covered stone surfaces.
- Tropical plants growing between ruins.
- Larger architectural silhouettes.
- A dramatic elevated overlook.
- Rich environmental depth.

Gameplay requirements:

Create a technically interesting but readable progression through multiple putting sections.

Elevation should affect ball behavior naturally.

Provide reasonable recovery opportunities.

Avoid overly narrow precision-only obstacles that become frustrating in VR.

Where practical, allow the player to look back toward Jungle Island and the Starting Island from the upper temple.

Reserve suitable space and environmental continuity for the eventual Hole 6.

---

## 8. Performance Expectations

We have relaxed the original mandatory 120 Hz requirement.

The revised target is:

**Stable 90 Hz using the Rich graphical preset.**

120 Hz remains an optional performance-mode objective, not a restriction on visual ambition.

The latest validated version has demonstrated full-rate 90 Hz operation following a fresh SteamVR session.

Preserve that achievement where practical.

Continue using:
- LODs for distant assets.
- Sensible foliage culling.
- Efficient shadow casting.
- Reusable materials.
- Static batching where appropriate.
- Adjustable graphics presets.

Do not automatically sacrifice near-field graphical fidelity to recover 120 Hz.

Remember that previous investigations identified SteamVR and wireless-link conditions as potential causes of frame-pacing issues independent of scene geometry.

Any performance changes must be supported by measurements.

Preserve putting responsiveness and controller tracking.

---

## 9. Island-Based Original Soundtrack

Andrew will personally provide original background music for the environmental clusters.

**We are not creating a separate track for every individual hole.**

An environmental cluster may contain one, two or three holes, sharing a continuous musical theme.

The eventual seven-world game will contain 63 holes, with approximately four or five distinct musical environments per world, depending on course design.

### Tropical Adventure music plan

| Environmental Cluster | Music |
|---|---|
| Starting Island — Holes 1–2 | Existing Island Exploration track |
| Jungle Island — Holes 3–4 | New jungle theme supplied by Andrew |
| Temple Island — Holes 5–6 | New temple theme supplied by Andrew |
| Volcanic Island — Holes 7–8 | Future volcanic theme |
| Summit Sanctuary — Hole 9 | Future finale theme |

This structure must remain flexible if the eventual hole grouping changes.

### Music delivery

By the time the current development milestone is finished, Andrew expects the relevant new music files to be available locally at:

`C:\Downloads`

Cloud Claude cannot access this directory directly.

Prepare the Unity integration and document the expected filenames and asset locations.

Local Claude will import the actual music once the files are ready.

### Music system requirements

1. Assign music by environmental cluster rather than individual hole.
2. Maintain continuous playback between holes belonging to the same cluster.
3. Support seamless looping.
4. Smoothly crossfade when transitioning into a new island or environmental region.
5. Avoid unnecessary restarts or overlapping music sources.
6. Preserve the current default music volume of 0.55.
7. Provide independent music and sound-effects volume controls.
8. Use efficient streaming/compression settings for longer audio tracks.
9. Design the system to be reusable across all seven planned worlds.

Do not generate replacement music or use unrelated licensed tracks.

Andrew is providing the soundtrack.

---

## 10. Gameplay Preservation

Do not alter existing functioning systems without a demonstrated technical reason.

Preserve:

- Ball physics.
- Putter interaction.
- Steam Frame controller mappings.
- Locomotion.
- Stroke counting.
- Scorecard behavior.
- Out-of-bounds rules.
- Hole progression.
- Existing quality settings.

All new putting geometry should work within the established gameplay architecture.

Ensure environmental colliders do not interfere with playable greens.

Expand automated tests to cover the new holes and their unique geometry.

Test both the safe and shortcut routes on Turtle Cove.

Verify that players cannot become stranded during inter-island progression.

---

## 11. Development Responsibilities

We are intentionally splitting development between Cloud Claude and Local Claude to make efficient use of available resources.

### Cloud Claude

Your responsibilities:

- Design and implement new hole definitions.
- Develop course geometry.
- Implement island generation and environmental dressing logic.
- Connect new holes to course progression.
- Extend the reusable environment and music systems.
- Write additional Blender generation scripts if new original assets are required.
- Add automated regression tests.
- Maintain documentation.
- Prepare a clear local-validation handoff.

Your cloud environment does not have Unity or Blender.

Do not waste substantial development time attempting to establish a full graphical workstation.

You may write new asset-generation scripts, but clearly document that they require local execution.

Do not report code as compiled or graphics as verified unless genuine testing has occurred.

### Local Claude

Local Claude will handle:

- Blender script execution.
- Unity asset import.
- Unity compilation.
- Scene generation.
- Desktop screenshot capture.
- Automated tests.
- PCVR and desktop builds.
- Performance benchmarking.
- Final Steam Frame testing with Andrew.

Organize your work into meaningful checkpoints so Local Claude can validate batches of changes without excessive back-and-forth.

### Universal Modder / FAL

Do not initiate paid generation without Andrew's approval.

Prioritize our existing successful Blender asset pipeline.

Any future AI-generated assets must be documented with their source and applicable licence.

---

## 12. Required Deliverables

At the end of this milestone, provide:

1. Three new playable holes: Jungle Crossing, Turtle Cove and Temple Steps.
2. A complete Jungle Island environment.
3. An initial Temple Island environment with reserved space for Hole 6.
4. Appropriate island-to-island transitions.
5. Updated course navigation and progression.
6. Reusable environmental systems and new asset scripts where required.
7. Flexible cluster-based music support.
8. Expanded automated tests.
9. Updated project and development documentation.
10. A GitHub development checkpoint and pull request.
11. A comprehensive Local Claude validation handoff.
12. A concise ChatGPT HQ report identifying completed work, limitations and any decisions required from Andrew.

The local visual checkpoint should eventually include:

- Aerial overview of the expanded archipelago.
- Player-height tee views for Holes 3–5.
- Jungle Crossing bridge.
- Turtle Cove's alternative routes.
- Temple Steps architecture and elevation.
- Inter-island transitions.
- Before/after performance measurements.

---

## 13. Stop Condition

After implementing Holes 3, 4 and 5, commit and push the development branch.

Prepare the handoff and STOP.

Do not begin Holes 6–9 without approval.

The next milestone will complete the remaining course, including our potentially ambitious standalone Hole 9 finale.

### Final Creative Objective

**Nine holes. Flexible island clusters. One extraordinary tropical adventure.**

Every island should feel like discovering a new location in a beautiful video game, while the putting experience remains familiar, intuitive and genuinely enjoyable.

---

## Local Claude addendum (answers to Cloud Claude's questions; suggestions, not HQ requirements)

**Exact hole layouts** (lane shapes, bank angles, ramp heights, bridge length) are deliberately left to Cloud Claude's design within sections 5–7. Keep the existing constraints:
- par as given;
- lanes wide enough for VR (Hole 1's lane width as the minimum);
- `PlayableSurface` greens, plus `OutOfBoundsSurface` on everything else;
- hole-local frames via `TropicalCourse` and `HoleDefinition`.

**Suggested checkpoint order:**
1. **M3 checkpoint 1:** cluster music system (with the existing track as the Starting Island cluster), the archipelago and island-cluster framework, the Jungle Island terrain and dressing, Hole 3 Jungle Crossing with its playable bridge, and the Hole 2 → 3 transition. Tests for each.
2. **M3 checkpoint 2:** Hole 4 Turtle Cove (both routes, with tests for each) and the cove environment.
3. **M3 checkpoint 3:** Temple Island, Hole 5 Temple Steps, the reserved Hole 6 space, the Hole 4 → 5 transition, and the overlook sightlines.
4. **M3 checkpoint 4:** stranding/progression tests, capture viewpoints for all section 12 shots, docs, the validation handoff and the HQ report.

New Blender assets (bridge, temple architecture, jungle trees, vines) can go in whichever checkpoint first needs them. Note them in the commit so Local Claude runs those scripts first.

**In-project music names** (Local Claude converts Andrew's WAVs, whatever their source names, to these):

| Cluster | In-project file | Status |
|---|---|---|
| Starting Island (1–2) | `Assets/_Game/Audio/Music/IslandExploration.ogg` | exists |
| Jungle Island (3–4) | `Assets/_Game/Audio/Music/JungleTheme.ogg` | awaiting Andrew |
| Temple Island (5–6) | `Assets/_Game/Audio/Music/TempleTheme.ogg` | awaiting Andrew |
| Volcanic Island (7–8) | `Assets/_Game/Audio/Music/VolcanicTheme.ogg` | future |
| Summit Sanctuary (9) | `Assets/_Game/Audio/Music/SummitTheme.ogg` | future |

The system must handle a missing clip gracefully: keep playing the current cluster's music and log a warning, so checkpoints work before Andrew's tracks arrive.
