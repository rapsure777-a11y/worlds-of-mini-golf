# RESUME: where we stopped (2026-10-05)

Read this first, then `CLAUDE.md`. Project: `C:\Users\fence\Projects\WorldsOfMiniGolf`, branch `milestone-3-island-hopping`. Everything is committed (check `git log -3`); nothing has been pushed to GitHub (push only if Andrew asks).

## State in one paragraph
Worlds of Mini Golf has **4 holes built** out of a planned **9**: Beach Warm-up (1) and Palm Corner (2) on the Starting Island, Jungle Crossing (3, bridge over a ravine) and **Tiki Twister (4, par 3: drop, 90 degree turn, ramp jump over a 0.6 m gap, roulette bowl)** on the Jungle Island. **107/107 PlayMode tests passed at the last full run** and both players build (`Builds/Windows/WorldsOfMiniGolf.exe` PCVR, `Builds/Desktop/WorldsOfMiniGolf_Desktop.exe`). Andrew has headset-approved holes 1-4, the tiki masks and poles, the scorecard and the sound. Report: `Docs/REPORT_HOLE4_TIKI_TWISTER.md`.

## Course plan (Andrew's decision)
Nine holes in five zones, one music track per zone (all five tracks are imported): 1-2 Starting Island (IslandExploration), 3-4 Jungle (JungleTheme), 5-6 Temple (TempleTheme), 7-8 Volcanic (VolcanicTheme), **9 Summit: one long finale hole** (SummitTheme). Cluster data lives in `TropicalCourse.Clusters()` and already matches; Temple, Volcanic and Summit have no islands or holes yet.

## First thing to do next session
1. Ask Andrew whether to push (cloud only sees pushed work), then continue with Temple Island holes 5-6 (new island, `TropicalCourse.Clusters()` needs centre/radius, new `Hole05/06`, a `TempleIsland` class like `JungleIsland`, tests like the Hole 3/4 ones). Use cloud's design sprint (`Docs/HOLE_DESIGN_SPRINT_001.md`) and work obstacles (waterwheel) in as features inside holes; the waterwheel is not in the course yet.
2. Launch with `Builds\Windows\WorldsOfMiniGolf.exe`, SteamVR streaming first, ONE streaming session only (two sessions made the headset audio cut out).

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
