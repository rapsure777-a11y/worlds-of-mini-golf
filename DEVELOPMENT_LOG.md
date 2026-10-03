# Development log

## 2026-10-03: Milestone 1 build (First Physical Putt)

**Done**
- Unity 6000.3.9f1 project from the URP template; added OpenXR 1.18 / XR Management 4.7 / Input System 1.20. Removed collab, multiplayer center and visual scripting.
- `ProjectSetup` configures Windows x64, D3D11 only, Linear, PC URP asset (MSAA 4x), OpenXR loader with Single Pass Instanced and 7 controller profiles.
- Gameplay core: `GolfBall` (own rolling model), `Putter` (tracked, swept strike test), `Cup` (real hole in mesh), `HoleController` (strokes, OOB +1 and return, reset, stroke limit 10), `CourseController` (hole sequence, scorecard), `VRRig` (tracking, teleport arc, snap turn, grab-move, putter length/angle/twist, handedness, go-to-ball, desktop debug mode), wrist display, scorecard panel, synthesised SFX and haptics.
- `CourseGeometry`: welded green mesh from layout and height function, real cup hole and cup, rails around the outline.
- `SceneBuilder` generates `TropicalAdventure.unity`: Hole 1 "Beach Warm-up" (7 m lane, 8 cm rise, par 2) on a greybox island with primitive palms and rocks.
- Windows player builds (99 MB, `Builds/Windows/`).
- `Tools/WalkaboutProbe` measurement mod built and deployed to the Walkabout Mods folder; `analyze.py` turns recordings into tuning numbers.

**Tests (PlayMode, editor, no headset): 10/10 pass**
| Test | Result |
|---|---|
| Roll-out from 2 m/s | 3.046 m vs 3.056 m analytic |
| Putt at cup | holed, 1 stroke |
| 5 m/s over cup | skips; hops about 4 cm off the far lip and loses about half its speed |
| Perpendicular wall rebound | ratio 0.72 (= tuning) |
| OOB | returns to last rest spot, +1 stroke |
| 2% slope holds ball / 12% slope rolls it | pass |
| Putter swing 1.5 m/s | ball about 2.0 m/s straight down the face normal |
| 10° open face | launch 8.5° off line |

**Bugs found and fixed through tests**
- The contact normal from (body centre minus contact point) tilted up to 35° at speed, because PhysX contact points are from the start of the step. This was braking the ball on flat ground. Now uses the start-of-step centre.
- Rest detection compared total speed including the ground-hold push, so the ball never rested. Now uses surface speed.
- PhysX reports face normals on the cup rim edge, which our wall logic treated as a wall bounce. Fixed by the start-of-step normal.
- A rigid inelastic cup lip launched fast balls about 40 cm high. Upward speed added per contact is now capped (`maxContactLift` 0.5 m/s).
- Cups snapped to cell centres (5 cm off the requested spot). An even cup tile now snaps to grid vertices exactly.

**Not yet verified:** anything in the headset (tracking, putter orientation on Frame controllers, SPI rendering of all materials, comfort, frame rate).
