# Local tasks

Work that needs Andrew's PC, the Unity Editor UI, the headset, or Walkabout. Local Claude/Codex can run the non-headset steps; Andrew does anything in VR.

Repo: `C:\Users\fence\Projects\WorldsOfMiniGolf`. Unity: `C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe`.

---

## L1: First physical putt in the Steam Frame (Milestone 1 acceptance), OPEN
**Who:** Andrew (headset). About 15 minutes.

1. Start SteamVR with the Frame connected and streaming.
2. Run `Builds\Windows\WorldsOfMiniGolf.exe`. (Rebuild first if needed: `Unity.exe -batchmode -quit -projectPath . -executeMethod Gamebreak.MiniGolf.Editor.Automation.SetupAndBuild -logFile Logs\build.log`.)
   - Or open the project in Unity, open `Assets/_Game/Worlds/Tropical/Scenes/TropicalAdventure.unity` and press Play with SteamVR running.
3. Controls:
   | Input | Action |
   |---|---|
   | Right hand (default) | Putter. Swing it through the ball. There is no shoot button. |
   | Either stick forward, release | Teleport |
   | Stick left/right | Snap turn 30° |
   | Left grip, hold and pull | Grab-move (drag yourself) |
   | Right grip + stick up/down | Putter length |
   | Right grip + stick left/right | Putter angle (add trigger: rotate head) |
   | A / X on putter hand | Stand beside the ball, facing the cup |
   | B / Y on putter hand | Return ball to last resting spot |
   | Off-hand A / X | Show or hide scorecard |
   | Off-hand B / Y, hold 1 s | Switch putter hand |
   | Menu, hold 1.5 s | Restart hole |
4. Report back (copy this list):
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
**Who:** Andrew, about 2 minutes. `gh` is not installed.
1. `winget install --id GitHub.cli` (or let Claude install it).
2. `gh auth login` (browser sign-in).
3. Tell Claude to create the repo: suggested name `worlds-of-mini-golf`, **private**.

## L4: Blender, OPEN
Blender was downloading on 2026-10-03. When installed, tell Claude its path (Microsoft Store builds are found with `Get-AppxPackage *Blender*`). Needed for Milestone 4 (World Forge trial assets).
