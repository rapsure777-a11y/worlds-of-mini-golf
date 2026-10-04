# RESUME: where we stopped (2026-10-04, written before a PC restart)

Read this first, then `CLAUDE.md`. Project: `C:\Users\fence\Projects\WorldsOfMiniGolf`, branch `milestone-3-island-hopping`. Everything is committed (check `git log -3`); nothing has been pushed to GitHub (push only if Andrew asks).

## State in one paragraph
Worlds of Mini Golf has **4 holes built** out of a planned **9**: Beach Warm-up (1) and Palm Corner (2) on the Starting Island, Jungle Crossing (3, bridge over a ravine) and **Hollow Drop (4, par 4: a 24 cm drop ramp, a low basin, a 10% climb to a raised cup)** on the Jungle Island. **70+ PlayMode tests pass (78/78 at the last run)** and both players build (`Builds/Windows/WorldsOfMiniGolf.exe` PCVR, `Builds/Desktop/WorldsOfMiniGolf_Desktop.exe`). Andrew has tested holes 1-3, the scorecard sliders/restart buttons, the trees, the barrels/crates and the sound in the headset. **Hole 4, the tiki poles/masks and the score celebrations were built last and have NOT been tried in VR yet.**

## Course plan (Andrew's decision)
Nine holes in five zones, one music track per zone (all five tracks are imported): 1-2 Starting Island (IslandExploration), 3-4 Jungle (JungleTheme), 5-6 Temple (TempleTheme), 7-8 Volcanic (VolcanicTheme), **9 Summit: one long finale hole** (SummitTheme). Cluster data lives in `TropicalCourse.Clusters()` and already matches; Temple, Volcanic and Summit have no islands or holes yet.

## First thing to do next session
1. Ask Andrew how Hole 4, the tiki props and the celebrations felt in the headset (launch with `Builds\Windows\WorldsOfMiniGolf.exe`, SteamVR streaming first, ONE streaming session only: two sessions made the headset audio cut out).
2. Andrew tested Hole 4 (2026-10-04): too long, too many turns for par 4, too generic/flat, no obstacles. Redesign waits for the cloud design sprint review (`Docs/HOLE_DESIGN_SPRINT_001.md`).
3. Then continue: Temple Island holes 5-6 (new island, `TropicalCourse.Clusters()` needs centre/radius, new `Hole05/06`, a `TempleIsland` class like `JungleIsland`, tests like the Hole 3/4 ones).

## Built this session (details in `DEVELOPMENT_LOG.md`)
- Hero props via Blender: barrel and crates (`Tools/Blender/hero_props.py`), jungle trees (`hero_jungle.py`, fewer outward-facing leaf cards, attached to branches), tiki poles and masks (`hero_tiki.py`). Regenerate with `blender-launcher.exe -b --factory-startup --python Tools/Blender/<script>.py` (the Store build only runs through `%LOCALAPPDATA%\Microsoft\WindowsApps\BlenderFoundation.Blender_ppwjx1n5r4v9t\blender-launcher.exe`).
- Scorecard (X): Music/Effects sliders, Restart Hole, Restart Course (needs a second press). The putter head works as a pointer (slider: rest 0.3 s, then it moves relatively; button: rest 0.7 s). Controller fingertip + trigger also work.
- `ScoreCelebration` (confetti, floating label, fanfare, haptics) on hole completion.
- Sign boards are one solid panel so the text no longer crosses plank seams.

## Tooling and gotchas
- Shell is Windows PowerShell 5.1 (no `&&`). Unity CLI scripts: `Tools\art-shots.ps1 -Quality rich` (rebuild scene + screenshots), `Tools\run-tests.ps1` (SteamVR/game closed), build both players with `-executeMethod Gamebreak.MiniGolf.Editor.Automation.SetupAndBuildAll`.
- Close the game before rebuilding (the exe is locked). Models face Blender -Y but arrive on Unity +Z: `TropicalWorld.Tiki` turns them 180 degrees; the clubhouse handles it with its own yaw.
- Known flaky test: `Swing_StrikesBallAlongFace` fails now and then when run alone (timing); passes in the full suite.
- Saved volumes live in the registry (`HKCU\Software\Gamebreak Labs\Worlds of Mini Golf`, keys `gb.musicScale*`, `gb.sfxVolume*`). If the game is ever silent, check those first: they had been saved at 0 by accident. A quick check that the game really outputs sound: the audio-session peak meter probe used this session (query the process's Windows audio session peak).
- Headset audio: Windows default playback must be "Speakers (Steam Streaming Speakers)"; Andrew had to play through PC speakers when two streaming sessions fought.
- Only the right controller connected in Andrew's logs (Steam Frame). Check `Sessions\session_*.txt` under `%USERPROFILE%\AppData\LocalLow\Gamebreak Labs\Worlds of Mini Golf`.

## How Andrew likes to work
Short status updates, implementation over research, he tests in the headset and reports (I cannot see VR). Ask him before pushing or publishing anything. He decides the creative direction (hole count, zone plan, tiki theme).
