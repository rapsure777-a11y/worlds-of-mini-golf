# Walkabout reference

Observations of Walkabout Mini Golf (user's legitimate Steam install, app 1408230) used to tune our **independent** implementation. Record numbers and behaviour only. Never copy code, assets or extracted files.

## Known facts (from the 2026-10-03 modding experiment)
| Item | Value | Source |
|---|---|---|
| Engine | Unity 6000.3.9f1, IL2CPP | file scan |
| Render | URP, D3D11, Single Pass Instanced, Tex2DArray eye texture 3240x3240, Linear | runtime `info` via SceneryMod |
| XR | Unity OpenXR plugin | boot.config |
| Scenery | Static-batched ("Combined Mesh"); colliders separate from visuals | dump |
| Mod route | MelonLoader 0.7.3 works; UnityExplorer 4.13.6 crashes on Unity 6.3 | experiment |

Our project matches the render setup (URP, D3D11, SPI, Linear).

## To measure (WalkaboutProbe, see `LOCAL_TASKS.md` task L2)
| Quantity | Our current value | Walkabout | Notes |
|---|---|---|---|
| Physics step rate | 120 Hz | ? | `settings` |
| Gravity | -9.81 | ? | `settings` |
| Ball radius / mass | 0.0225 m / 0.046 kg | ? | `bodies` |
| Ball drag / angular drag | custom (0 + own model) | ? | `bodies` |
| Ball / wall physics materials | 0 friction, 0 bounce (own model) | ? | `materials.tsv` |
| Rolling decel a0 / k | 0.55 m/s² / 0.08 1/s | ? | `analyze.py` fit |
| Wall rebound speed ratio | ~0.68 perpendicular (0.72 x rail loss) | ? | `analyze.py` |
| Strike transfer (ball/putter speed) | 1.35 | ? | `analyze.py` with xforms csv |
| Cup radius | 0.054 m | ? | collider dump around the cup |
| Max holing speed | ~1.5 m/s centre hit (to verify) | ? | observe |
| OOB rule | return to last rest, +1 stroke | ? | observe |
| Stroke limit | 10 | ? | observe |

## Qualitative feel notes (fill in from play)
- Putter: does the head pass through walls/ground? Two-faced? How is length adjusted?
- Movement: teleport arc, grab-and-pull, snap turn angles, "go to ball" button?
- Feedback: haptic strength on strike, sounds, ball trail?
- UI: where is the stroke count shown? Scorecard access?
