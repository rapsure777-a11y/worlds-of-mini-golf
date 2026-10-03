# Local tasks

Work that needs Andrew's PC, the Unity Editor UI, the headset, or Walkabout. Local Claude/Codex can run the non-headset steps; Andrew does anything in VR.

Repo: `C:\Users\fence\Projects\WorldsOfMiniGolf`. Unity: `C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe`.

---

## L1: First physical putt in the Steam Frame (Milestone 1 acceptance), OPEN
**Who:** Andrew (headset). About 15–20 minutes. The two holes are greybox: plain colours and primitive palms. Judge the feel, not the art.

**Before you put the headset on (2 min)**
1. Optional sanity check without VR: double-click `Builds\Desktop\WorldsOfMiniGolf_Desktop.exe`. Hold the left mouse button and move the mouse through the ball to putt; the help panel lists the keys. Close it when done.
2. Start SteamVR with the Frame connected and streaming. Wait until SteamVR shows the headset as ready.
3. Run `Builds\Windows\WorldsOfMiniGolf.exe`. The desktop window shows a debug panel (FPS, strike speeds) that is useful if someone is watching. If the window says "Desktop debug" instead of "VR: ...", OpenXR did not start: check SteamVR is the active OpenXR runtime (SteamVR Settings > Developer > "Set SteamVR as OpenXR runtime") and relaunch.

**In the headset**
You start behind Hole 1's tee, looking down the lane. The putter is in your right hand.

| Input | Action |
|---|---|
| Right hand (default) | Putter. Swing it through the ball. There is no shoot button. |
| Either stick forward, release | Teleport (arc) |
| Stick left/right | Snap turn 30° |
| Left grip, hold and pull | Grab-move (drag yourself, Walkabout style) |
| Right grip + stick up/down | Putter length |
| Right grip + stick left/right | Shaft angle (add trigger: rotate head) |
| A / X on putter hand | Stand beside the ball, facing across the line to the cup |
| B / Y on putter hand | Return ball to its last resting spot (no penalty) |
| Off-hand A / X | Show or hide the scorecard |
| Off-hand B / Y, hold 1 s | Switch putter hand |
| Menu, hold 1.5 s | Restart hole |

Suggested order:
1. Look around: both eyes render the same scene, nothing pink, the floor is at your real floor height.
2. Look at the putter. If it points somewhere strange (along your forearm, sideways), hold the **right grip** and push the stick **left/right** until the shaft hangs down naturally, then rotate the head (grip + trigger + stick left/right) until the face is square. Note roughly how far you had to adjust; I'll make it the default.
3. Press **A** to stand beside the ball. Take a few practice swings away from the ball, then putt. Try a soft tap, a medium putt and a firm one.
4. Play Hole 1 out. After 3 seconds you move to Hole 2 (draft dogleg). Play it out too.
5. Try teleport, snap turn, grab-move, ball return (B), the scorecard (off-hand X) and a hand switch.
6. Quit with Alt+F4 on the desktop window, or from the SteamVR dashboard.

**The session is logged automatically** to `%USERPROFILE%\AppData\LocalLow\Gamebreak Labs\Worlds of Mini Golf\Sessions\session_*.txt`. It records the headset and controller layout, every strike (head speed, ball speed, face angle, aim error), hole results, your final putter settings and frame timing. Just tell me to look at it; you don't need to copy numbers.

**Report back** (short answers are fine):
   - [ ] Game renders in both eyes (no pink materials, no single-eye rendering)
   - [ ] Head and controllers track; putter follows the hand with no visible lag
   - [ ] Which way does the putter extend from the controller? Is the face square to the target when you hold it naturally? (If not, say roughly how far off: grip-angle and head-rotate adjustments exist)
   - [ ] A slow tap moves the ball a little; a firm swing sends it far. Does the speed feel proportional?
   - [ ] Ball rolls straight, slows naturally, climbs the small rise
   - [ ] Rails bounce the ball believably
   - [ ] Ball drops into the cup; wrist display shows strokes and result
   - [ ] Teleport, snap turn, grab-move all work and feel comfortable
   - [ ] Haptic buzz and click sound on strike
   - [ ] Frame rate smooth (SteamVR frame timing if possible)
   - Which controller profile SteamVR used (SteamVR > Controller bindings shows it)
   - Anything that felt wrong compared with Walkabout

## L2: Walkabout physics measurement, OPEN
**Who:** Andrew plays; Local Claude can do the file steps. About 10 minutes in VR.

`WalkaboutProbe.dll` is already copied to `C:\Program Files (x86)\Steam\steamapps\common\Walkabout Mini Golf\Mods\`. It only reads values and writes CSVs to `...\Walkabout Mini Golf\UserData\probe\`.

1. Launch Walkabout Mini Golf (solo, offline).
2. On any hole, create `UserData\probe\cmd.txt` with:
   ```
   settings
   bodies
   ```
   (Local Claude can write that file while the game runs.) Check `probe.log` says both were written.
3. Play putts. It records automatically while the ball or putter moves:
   - 3 long straight putts on a flat section, letting the ball stop on its own (rolling decel)
   - 3 firm putts straight into a rail, then 3 at about 45° (rebounds)
   - 5 putts of varying strength with a clean swing (strike transfer)
   - 2 putts that roll over the cup too fast (lip behaviour), plus note visually how high it hops
   - Hit one out of bounds; note what happens to the ball and the stroke count
4. Quit, then run:
   ```
   uv run --python 3.12 Tools/WalkaboutProbe/analyze.py "<game>\UserData\probe\bodies_*.csv" "<game>\UserData\probe\xforms_*.csv"
   ```
5. Copy the printed numbers and the key lines of `settings.tsv`, `bodies.tsv` (ball row only) and `materials.tsv` into `WALKABOUT_REFERENCE.md`. **Do not commit the CSV/TSV files.**
6. If the putter transform isn't named "putter", look at `bodies.tsv`/a renderer dump for its name and send `trackxf <name>`.

Cleanup when finished: delete `Mods\WalkaboutProbe.dll`. Note that `Mods\sinai-dev-UnityExplorer` is left over from the earlier experiment (UnityExplorer crashed the game) and is probably safe to delete. To return the game to vanilla, verify files in Steam and remove `version.dll`, `MelonLoader\`, `Mods\`, `UserLibs\`, `UserData\`.

## L3: GitHub remote, OPEN
**Who:** Andrew, about 2 minutes. `gh` 2.102 is installed but not signed in.
1. In the Claude Code prompt type `! gh auth login` (GitHub.com, HTTPS, browser sign-in).
2. Claude then creates the **private** repo `worlds-of-mini-golf` and pushes `main`.

## L4: Blender, OPEN
Blender was downloading on 2026-10-03. When installed, tell Claude its path (Microsoft Store builds are found with `Get-AppxPackage *Blender*`). Needed for Milestone 4 (World Forge trial assets).
