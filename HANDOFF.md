# Worlds of Mini Golf: handoff (written 2026-10-03)

You are picking up a project from a previous session that you cannot see. This file is the full context. Read it before doing anything.

## The project
An **original** Unity PCVR miniature-golf game. Working title: **Worlds of Mini Golf**. It is heavily *inspired by* Walkabout Mini Golf's putting controls, physics, locomotion and feel, but it is independent development.

- **Hard rule:** no Walkabout code, assets, extracted files or decompiled output go into this project. Feel is matched by tuning our own implementation. Do not copy.
- All art must be original or permissively licensed (CC0 such as Quaternius, Kenney, Poly Haven; or AI-generated through Universal Modder). Record each asset's license in `ASSETS.md`.
- **First milestone:** ONE playable VR hole with physical putting that feels as close to Walkabout as independent work can get.
- **First theme:** a polished, saturated tropical-island platformer look, a Crash Bandicoot 4-inspired *style* (stylized chunky palms, exaggerated rocks, layered jungle, playful atmosphere). It is a style reference only. Use no assets or IP from that game. Not photorealistic.
- **Theme roadmap (creative direction, do not build yet):** Tropical (Crash-inspired), Cherry Blossom (Ghost of Tsushima), Gothic (Bloodborne/Castlevania), Journey to the Center of the Earth (Skyrim Blackreach), Gardens of Babylon (AC Mirage), Atlantis (Subnautica), Shangri-La (Himalayan/Journey-like), Labyrinth (Elden Ring-like dark castle). The user once said "seven" and the list has eight; ask which is right. Design so a theme is a swappable asset collection rather than a code change, but do not over-engineer this now.
- No extended research phase. Apply what is below and implement.

## User and hardware
- Windows 11 Pro PC, AMD Radeon RX 7900 XT, **Steam Frame headset over PCVR streaming**. The Frame is only a display; the game runs on the PC as x64. Do NOT use ARM64 builds.
- User name on the machine: `fence`. The user is hands-on and tests in VR. You cannot see the headset, so ask them to check things in VR and report.
- Safety rules from earlier work that still apply: keep outputs small (write logs and inventories to files, read parts); solo/offline only; never publish extracted game files; confirm before outward-facing actions (publishing, pushing, creating public repos).

## Tooling already installed on this machine
| Tool | Where / notes |
|---|---|
| Unity Hub | `C:\Program Files\Unity Hub`, user is signed in (free Personal license) |
| Unity CLI | `%LOCALAPPDATA%\Unity\bin\unity.exe`. `unity install <ver> -c <changeset> -a x86_64 --json ...`. Its output only appears reliably when you redirect to files (Start-Process -RedirectStandardOutput). |
| Unity Editor **6000.3.9f1** | `C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe` (changeset 7a9955a4f2fa). A second editor, 6000.6.4f1, also exists. Prefer 6000.3.9f1 (URP 17.3.0 is built in to it). |
| Blender 5.2.2 | Microsoft Store build (the web and winget downloads were blocked with 403, and a Cloudflare bot check blocked blender.org). Find it with `Get-AppxPackage *Blender*`. |
| .NET 8 SDK | winget |
| git | winget. `gh` (GitHub CLI) is **not** installed. |
| uv + Python 3.12 | uv in `%USERPROFILE%\.local\bin`; Python lives in `%USERPROFILE%\uvpy` (set `UV_PYTHON_INSTALL_DIR` to it; the default location errored). Refresh PATH from the registry in new shells. |
| ffmpeg | winget (Gyan) |
| Universal Modder `um` 0.2.0 | `uv tool install git+https://github.com/rehan-remade/universal-modder`. Subcommands: scan, fal (asset generation: sprites, textures, PBR, 3D, rigs, SFX, music), sprite, render3d (Blender), video (contact sheets, trims), win (screenshots, recording, input, processes), backup, publish (lint a mod folder), kb (knowledge base). Needs `FAL_KEY` for fal. Use it for asset generation and capture; do not force it into ordinary Unity development. |

PowerShell notes: Windows PowerShell 5.1 (no `&&`). Run Unity headless with `Start-Process ... -ArgumentList '-batchmode','-quit','-projectPath',... ,'-executeMethod','Class.Method','-logFile',...`. Do NOT pass `-nographics` when you need real shader compilation (it forces a Null GfxDevice).

## Known-good package versions (looked up from packages.unity.com on 2026-10-03)
- com.unity.render-pipelines.universal **17.3.0** (built in to the 6000.3 editor)
- com.unity.xr.openxr **1.18.0**
- com.unity.xr.management **4.7.0**
- com.unity.xr.interaction.toolkit **3.6.1**
- com.unity.inputsystem **1.20.0**
- com.unity.xr.hands 1.9.0 (optional)

## What the earlier Walkabout modding taught us (use it, don't redo it)
The prior experiment is archived at `C:\Users\fence\Projects\walkabout-mod-experiment` (see its README). Do not delete it, and do not copy any of its game-derived data into this repo.

Reference facts about the *reference game* that matter for tuning feel and VR compatibility:
- Walkabout: Unity 6000.3.9f1, IL2CPP, **URP**, **Direct3D11**, OpenXR, **SinglePassInstanced** stereo, eye texture `Tex2DArray` 3240x3240 per eye (volumeDepth 2), **Linear** color space. Match this for our PCVR target: D3D11, OpenXR, Single Pass Instanced, Linear, URP.
- **Shader lesson (important):** a hand-written URP shader shipped in an asset bundle rendered **pink / `isSupported=False`** inside a SinglePassInstanced game, even though the bundle compiled with no errors. The cause was never fully isolated: removing `-nographics` changed nothing (the bundle was byte-identical). For our own game this is a non-issue if we build with XR enabled in Player Settings. If custom shaders go pink in VR, check that the OpenXR plugin is enabled with Single Pass Instanced in the project before the build, include `#pragma multi_compile_instancing`, and use the `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` macros. URP/Lit and Shader Graph shaders work in single-pass instanced projects.
- Static batching in the reference game merges scenery into "Combined Mesh" objects. This is an optimization we may want too, but note it prevents per-object visibility changes.
- Il2CppInterop on Unity 6 breaks some Unity generics (`ReadOnlySpan.GetPinnableReference`). Irrelevant to our native Unity project, so ignore it.

## Immediate next steps (nothing below has been started)
1. Confirm with the user where they want to work (they questioned doing this in the desktop Code tab). Then `git init` here.
2. **GitHub repo:** `gh` is not installed and not authenticated. Ask the user: repo name (suggest `worlds-of-mini-golf`), **private or public (default private)**, and whether to install `gh` and run `gh auth login` themselves. Do not push anything without their yes.
3. Create the Unity 6000.3.9f1 project in this folder (URP template or minimal manifest with the packages above). Set Player Settings: Windows x64, Direct3D11 only, Linear, OpenXR plugin with Single Pass Instanced, Input System active.
4. Implement the vertical slice, preferably with an editor script that builds the scene reproducibly in batch mode:
   - XR rig (XR Origin, tracked head and hands) with smooth move, snap turn and teleport/walk locomotion.
   - Physical putter held in the controller: the head follows the hand via physics (velocity or joint tracking, not parenting), with a sensible mass and collision so the ball responds to real swing speed and angle.
   - Ball: Rigidbody tuned for putting (low drag, rolling resistance, sleep and rest detection, physics material), ball dispenser or return, reset on out-of-bounds, a stroke counter.
   - Cup: trigger and sink detection, hole-complete feedback.
   - One hole of greybox geometry on a tropical island with water hazards (reset triggers), then dress it with stylized assets.
   - World-space scorecard UI.
   - Play-mode tests: ball rolls into the cup, out-of-bounds resets, stroke counting.
5. Source stylized tropical assets (CC0 packs, or generate with `um fal`, then clean up in Blender). Track licenses.
6. Check each rung in VR with the user. Frame time targets: hold the headset's native refresh with headroom.

## Working style the user wants
Implementation over research. Short status updates. Report failures with what was tried. Keep big output out of chat. When blocked on something only the user can do (VR checks, sign-ins, UAC prompts, quitting the game), say so plainly and wait.
