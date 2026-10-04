# Hole Design Sprint 001 — Tropical Adventure: Gameplay-First Course Redesign

Status: **DESIGN ONLY. Awaiting Andrew's approval.** No Unity scene, physics, terrain, shader, material or asset was modified. Graphics Pass 3 has not begun. The only additions in the repo are this document and `Tools/DesignSim/` (an offline Python/C paper-model of the ball physics used to test ideas).

> "A hole's shape is not its gameplay identity."
> "One strong mechanic executed beautifully is preferable to five poorly combined gimmicks."
> "Build the interesting mechanics once. Reinvent their appearance seven times."

---

## 0. Read this first

1. **Only Holes 1–3 exist in the repo.** Hole 4 is a spec in `MILESTONE3_BRIEF.md`, not built geometry. The "evaluation of existing Holes 1–4" below therefore evaluates 1–3 as built and Hole 4 as specified. Hole 4 can be reshaped for free.
2. **Hole 3 as built is too hard for par 3.** In the paper model, no modelled player type reaches par-or-better with any regularity on the as-built bridge, and the crossing is very speed-sensitive (a straight shot of 2.0 m/s stops short, 2.4 stops short, 2.8 is near the span, 3.2+ overshoots or goes off the far side). See §4, Hole 3.
3. **All sim numbers are indicative, not measured.** Player scatter is assumed (casual 6° / 20% speed, regular 3.5° / 13%, expert 1.8° / 7%). Airborne balls and lip-outs are not modelled. Treat *relative* comparisons (A vs B) as more reliable than absolute percentages. Every concept needs Unity validation by Local Claude, and the scatter should be calibrated from real session logs.
4. **Engine constraints bound the design** (§6). The golf engine is 2.5D (one height function, no overpasses), greens are unions of axis-aligned 0.1 m rectangles, and there is currently no support for angled/curved bumpers, moving obstacles that transfer velocity, or hazard volumes. Several concepts below therefore *depend on* library components that don't exist yet; each is flagged.

---

## 1. Design principles used

- **Route decision beats geometry.** A hole is memorable when the player stands at the tee and has a choice with a visible price.
- **A shortcut must cost more than one stroke to miss,** otherwise it dominates. (Sim: narrow-gap shortcuts won over safe routes at every skill level unless failure cost ≥ 2 strokes or the gap was very tight.)
- **A bank shot only matters if the direct line is blocked or riskier.** Otherwise players ignore it.
- **Funnels and cone bowls are the primary forgiveness lever.** A cone of ~11% slope captures; smooth cosine bowls under-capture; smooth hump crests create rest zones (use peaked/A-frame crests).
- **Safe route always exists and is always readable.** Risk is opt-in.
- **Breathing room is deliberate.** Holes 1, 5-lower and 9-approach are relaxed on purpose.
- **Mechanics are built once as library parts, re-skinned per island.**

---

## 2. Course-wide progression

| # | Name | Par | Dominant skill | Direct / ricochet | Open / constrained | Horizontal / vertical | Safe / risk | Relaxed / demanding | New mechanic introduced | Library parts |
|---|------|-----|----------------|-------------------|--------------------|-----------------------|-------------|---------------------|-------------------------|---------------|
| 1 | Palm Landing | 2 | Power | Direct (bank optional) | Open | Gentle slope | Safe | Relaxed (1/5) | Slope reading, optional bank | Bank wall, ramp |
| 2 | The Fork | 3 | Route choice | Both | Open→narrow | Horizontal | Both | Easy–medium (2/5) | Split junction | Split junction, funnel |
| 3 | Rope Bridge | 3 | Precision + power | Direct | Constrained | Mild vertical | Medium | Medium (3/5) | Bridge crossing, recovery strip | Bridge, funnel, recovery zone |
| 4 | Smuggler's Cut | 3 | Route choice + precision | Direct | Constrained vs open | Horizontal | Both | Medium (3/5) | Tunnel shortcut | Tunnel/arch, funnel |
| 5 | Terrace Steps | 3 | Power / elevation | Direct | Open | **Vertical** | Safe | Relaxed→medium (2/5) | Adjustable ramp / terrace | Ramp, bowl |
| 6 | Tidal Gate | 3 | Timing | Direct | Constrained | Horizontal | Medium | Medium (3/5) | **First moving obstacle** | Rotating barrier |
| 7 | Lava Run | 4 | Risk judgment | Ricochet | Mixed | Horizontal | **Risk** | Demanding (4/5) | Hazard volumes | Hazard volume, bank wall, recovery |
| 8 | The Gauntlet | 4 | Mastery | Both | Constrained | Mixed | Both | Demanding (4/5) | Combines bridge + barrier + bank | Reuse only |
| 9 | Sunset Bowl | 3 | Celebration | Direct | Open | Bowl | Safe | Relaxed (2/5) | Giant funnel finale | Sloped bowl |

Course par: 2+3+3+3+3+3+4+4+3 = **28**. Difficulty is a wave rather than a ramp: 1 → 2 → 3 → 3 → **breather 5** → 3 → **spike 7** → 4 → **celebratory 9**.

---

## 3. Evaluation of existing holes vs proposals

| Hole | As built / specified | Verdict | Smallest change with largest gameplay gain |
|------|----------------------|---------|--------------------------------------------|
| 1 | Straight lane 1.2 × 7 m, rise 0.08 m, cup at (0, 6.2), par 2. | Fine as a tutorial; no decisions, no identity. | Add one optional bank pocket (a side alcove with an angled wall) and a very slight cross-fall. No change to the main lane. |
| 2 | L-shaped, cup (-3, 4.6), par 3. | "Another L." One line, one corner; the only decision is the aim point at the corner. | Replace the corner with a split: a wide forgiving outer route and a narrow inner shortcut. |
| 3 | Bridge over ravine, 12 cm rails, deck submesh, par 3. | The bridge is a straight pipe; entry direction doesn't matter; falling off costs a stroke and a long reset. **Unreachable at par 3.** | Banked approach funnelling into the bridge mouth, a peaked crest, a wider crossing window, a recovery strip beside the ravine, par stays 3. |
| 4 | Spec only. | Free to design. | Tunnel shortcut vs. coastal sweep with the tunnel mouth narrow enough that missing is a real cost. |

---

## 4. Holes in detail

Scale: 1 character ≈ 0.25 m horizontally in top-down diagrams (approximate).
Legend: `#` rail/wall, `T` tee, `O` cup, `~` water/OOB, `^` rise, `*` hazard, `=` deck.

### Hole 1 — "Palm Landing" (par 2)

**Main concept:** a welcoming, readable lane with a gentle cross-slope. An optional alcove lets curious players try a bank.

```
 top-down (cup end at top)

   #######
   #  O  #
   #     ###
   #     #        <- bank alcove: angled wall, 1 piece
   #    /#
   #   /
   #     #
   #  T  #
   #######
```

```
 side view (gentle rise)
                      O
              ___---‾‾‾
   T ___---‾‾‾
```

- **Primary obstacle:** the slope itself (0.08 m rise over 7 m) plus a faint cross-fall.
- **Safe route:** straight, firm putt. Par 2 for regular players.
- **Risk/reward:** bank off the alcove wall for an easier angle. Gains nothing in distance; the reward is style and an alternate line when the straight line is nudged by the cross-fall.
- **Expected difficulty:** 1/5.
- **Missed shots:** under-hit rolls back a short distance (the engine's hold on gentle slopes stops it); over-hit hits the back rail and returns; no OOB.
- **Memorable because:** it teaches reading, and the alcove tells the player this course has secrets.
- **Needs:** one angled bank wall (library).

### Hole 2 — "The Fork" (par 3)

**Main concept:** the first route decision. A long, wide, forgiving route versus a narrow shortcut. Not another L.

```
 top-down

   #########################
   #   O                   #
   #  ###      ############ #
   #  # #      #            #
   #  # #      #  long      #
   #  # #  ##  #  forgiving #
   #  #  \ ## ##  route     #
   #  #   short   #         #
   #  #   cut     #         #
   ####     T     ###########
```

- **Primary obstacle:** the central island dividing the fork; the shortcut is narrow (~0.5–0.6 m) with a funnel lip at the far end.
- **Safe route:** the wide outer loop. A plain 2-putt route to par 3.
- **Risk/reward:** shortcut gives a realistic par-2 opportunity.
- **Expected difficulty:** 2/5 (safe) to 3/5 (shortcut).
- **Missed shots:** shortcut miss bounces back to the fork entrance (a free retry by walking; no OOB) so the cost is about one stroke plus position.
- **Balance finding:** in the sim a *bank* off the island wall dominated the other options, so the island's near wall should be given a **non-bankable face** (rough/bumpy texture is decorative only; geometry is just a straight rectangle wall at a lower angle) or the bank lane made longer. Recommended: lengthen the outer route to ~3.0–3.4 m legs so the shortcut does not trivially dominate.
- **Memorable because:** the first time the player *chooses*.
- **Needs:** split junction (rectangle-union green, doable now) plus a funnel mouth.

### Hole 3 — "Rope Bridge" (par 3)

**Main concept:** make the bridge matter. Directional entry, banked approach, modest vertical variation, and a lower recovery strip.

```
 top-down

   ####################################
   #  clearing          ~~~~ ravine ~~~#
   #  T   ///           ~~~~=========O #
   #      ///  bank      ~~~~=========  #
   #           guide     ~~~~           #
   #####                 ~~~~  pad      #
       #  recovery strip (low) ##########
       ####################
```

```
 side view (peaked A-frame crest, not a smooth hump)
                  /\
        ________ /  \_____
   T __/      mouth      \__ pad __ O
   recovery strip (lower) ------
```

- **Primary obstacle:** the bridge deck — narrow, rail-lined (12 cm rails), with a **peaked crest** rather than a smooth hump.
- **Safe route:** approach from the clearing with a moderate, centred shot; the bank guide pushes slightly off-line shots toward the mouth.
- **Risk/reward:** a hard direct shot across scores par but overshoots; a soft shot parks on the near side.
- **Expected difficulty:** 3/5.
- **Missed shots:** a ball that falls off lands on the **recovery strip** (lower, safe, +1 stroke as OOB) rather than the far reset spot; short shots roll back into the clearing.
- **Sim findings:**
  - As built, no player type reaches par reliably; the crossing speed window is narrow and non-monotonic (slope hold zones at the crest).
  - A plain wide mouth: crossing success ~0.34–0.37; mean strokes about 4.4 / 4.0 / 3.6 (casual / regular / expert); par-or-better about 20 / 30 / 40%.
  - Conclusion: needs a wider crossing window, a funnel at the mouth, and a peaked rather than rounded crest. Final tuning is for Local to test, starting from: mouth funnel R~1.8 m, peak height +4 cm, recovery strip 0.9 m wide.
- **Memorable because:** the bridge is the hole, and the guide banks reward reading.
- **Needs:** bridge (exists), bank guides, funnel, recovery zone.

### Hole 4 — "Smuggler's Cut" (par 3)

**Main concept:** a rock-tunnel shortcut versus a longer coastal route.

```
 top-down

   ###################################
   #                          O      #
   #   coastal sweep lane  ########  #
   #  ############         # ARCH #  #
   #  #                   ##########  #
   #  #     rock mass       tunnel    #
   #  #   [=======tunnel========]     #
   #  T                               #
   ####################################
```

- **Primary obstacle:** a rock mass with a tunnel running through it; the coastal lane sweeps around.
- **Safe route:** the coastal sweep. Par 3 for regular players.
- **Risk/reward:** a tunnel shot saves a stroke but the mouth is narrow (~0.42 m).
- **Expected difficulty:** 3/5.
- **Missed shots:** a ball striking the tunnel mouth rebounds toward the tee; no OOB.
- **Sim findings:** with a cone funnel at the exit (R 1.8 m, 16 cm) the tunnel produces hole-in-one rates around 33–36% (casual), 55–62% (regular), 82–84% (expert). That is **too high**: the tunnel dominates the safe route. The coastal sweep mean was about 3.7 / 3.5 / 3.3. Recommended: small cosine funnel only (target HIO about 15–25%), narrower mouth, and an easier/shorter coastal route, or accept par 3 with the tunnel as the intended 2.
- **Memorable because:** you can see the cup through the tunnel mouth.
- **Needs:** tunnel/arch (functional rails + decorative rock), funnel.

### Hole 5 — "Terrace Steps" (par 3) — preliminary

**Main concept:** elevation. Two terraces connected by an adjustable ramp. A relaxed power hole after Hole 4.

```
 side view
                         ___O
                  ______/
      T ____/ramp
```

- **Primary obstacle:** a ramp whose slope is tuned so that a firm shot reaches the upper terrace.
- **Safe route:** two putts via a side switchback.
- **Risk/reward:** a single hard shot up the ramp for par 2.
- **Difficulty:** 2/5. **Missed shots:** roll back down the ramp; no OOB.
- **Memorable because:** the first visibly vertical hole.
- **Needs:** adjustable ramp (sim window tested), sloped bowl at the top.

### Hole 6 — "Tidal Gate" (par 3) — preliminary

**Main concept:** the first controlled moving obstacle — a slow rotating barrier or gate.

```
   #########
   #   |   #   rotating bar, ~6 s period
   # T ---O #
   #########
```

- **Primary obstacle:** a rotating barrier at the mid-lane.
- **Safe route:** wait and time the gap.
- **Risk/reward:** skip the wait with a bank.
- **Difficulty:** 3/5. **Missed shots:** deflect off the barrier back toward the tee.
- **Memorable because:** timing is a new skill.
- **Needs:** rotating barrier **with wall-velocity in the rebound** — not supported by the engine today (a gameplay-code change, needs approval).

### Hole 7 — "Lava Run" (par 4) — preliminary

**Main concept:** risk/reward around volcanic hazards.

```
   #####################
   #   ***   O   ***   #
   #   ***       ***   #
   #        ###        #
   #   T               #
   #####################
```

- **Primary obstacle:** hazard volumes (+1 stroke, return to the last rest spot).
- **Safe route:** the longer outer edge around the hazard.
- **Risk/reward:** a ricochet through the hazard gap to the cup.
- **Difficulty:** 4/5. **Missed shots:** hazard penalty and replay.
- **Memorable because:** the danger is visible and the player picks the price.
- **Needs:** hazard volume, bank wall, recovery zone.

### Hole 8 — "The Gauntlet" (par 4) — preliminary

**Main concept:** a combination of learned mechanics in sequence (bridge, then barrier, then a bank) with no new rules.

- **Primary obstacle:** reuse of Hole 3 bridge, Hole 6 barrier and Hole 7 bank in one hole.
- **Safe route:** three short controlled shots.
- **Risk/reward:** a single bold line through all three.
- **Difficulty:** 4/5. **Memorable because:** the player recognises their own growth.
- **Needs:** nothing new.

### Hole 9 — "Sunset Bowl" (par 3) — preliminary

**Main concept:** a large celebratory finale: an open amphitheatre bowl funnelling to the cup.

```
 side view
   \               /
    \_____   _____/
          \_/ O
```

- **Primary obstacle:** the bowl itself; a long first shot lands anywhere and the funnel does the rest.
- **Safe route:** any shot that reaches the bowl.
- **Risk/reward:** a direct hole-in-one line across the rim.
- **Difficulty:** 2/5. **Missed shots:** the ball circles and settles; no OOB.
- **Sim finding:** cosine bowls under-capture; use a cone profile of roughly 11% slope.
- **Memorable because:** every player ends the round with a shot that feels good.
- **Needs:** sloped bowl/funnel.

---

## 5. Reusable obstacle library (design only, not implemented)

| Part | Functional geometry | Decorative layer | Engine support | Effort |
|------|--------------------|------------------|----------------|--------|
| Angled/curved bank wall | Segment wall at arbitrary angle (polyline for curves) | Rock/wood/vine skin | **Missing** (walls are axis-aligned) | Medium |
| Adjustable ramp | Height function window with slope parameter | Planks/stone | Exists via height function | Low |
| Bridge | Deck submesh + rails | Rope/planks | Exists | Done |
| Tunnel/arch | Two rails + roof collider-free | Rock mass, arch | Rails exist, roof decorative | Low |
| Rotating barrier | Moving segment with velocity | Paddle/gate | **Missing** (no wall velocity in rebound) | High |
| Hazard volume | Polygon trigger, +1 stroke, return | Lava/water | **Missing** | Medium |
| Recovery zone | Low safe strip | Sand/shore | Exists via green union | Low |
| Split junction | Rectangle-union branches | Rock island | Exists | Low |
| Funnel / bowl | Cone height profile (~11% slope) | Terrace / basin | Exists via height function | Low |

**Separation rule:** collision geometry lives in a `Functional` child that the tests understand; decorative meshes are siblings with no colliders (the existing `NoSceneryOnGreens` test already enforces this under `HoleController`).

**Suggested order:** bank wall → hazard volume → rotating barrier. Holes 1–5 need only the first (and Hole 1's alcove is optional).

---

## 6. Engine facts that bound the design

- Green = union of axis-aligned rectangles on a 0.1 m grid; a single height function (2.5D).
- Rails around the union boundary: 9 cm (12 cm on Hole 3).
- Ball: rolling decel 0.55 + 0.08·v; slope accel 5/7·g; rest-hold on gentle slopes (~7.85%); wall rebound restitution 0.72, tangent keep 0.94 (outgoing angle shallower than mirror: tan β = 1.306 · tan α); maxBallSpeed 9 m/s.
- Cup radius 0.054 m; capture at roughly ≤1.4 m/s centred.
- OOB: any non-green contact → +1 stroke, return to the last rest spot; stroke limit 10.

---

## 7. Method and caveats

`Tools/DesignSim/` contains a C core (`golfsim.c`, built to `libgolfsim.so`) driven from Python, reproducing the `GolfBall` model on a 2D top-down height grid with wall segments, hazard polygons and cup capture. `validate.py` reproduces repo-tested facts (roll-out distance, 45° rebound, Hole 1/3 facts, cup drop speeds) and passes. Monte-Carlo policies in `play.py` model casual/regular/expert players.

Caveats: assumed scatter, no airborne balls or lip-outs, no VR aiming error beyond the scatter model, simplified player policy. Use for relative comparisons only. **Validate every concept in Unity and calibrate from real play logs before building.**

---

## 8. Approval requested

1. Is the **Hole 3 redesign** (peaked crest, funnel mouth, recovery strip) approved to replace the as-built bridge?
2. Is the **Hole 2 split-route** replacement of the L approved?
3. Hole 4 direction: tunnel with a modest funnel and a coastal sweep?
4. Approve building the **bank wall** part first, and a **wall-velocity** engine change later for Hole 6?
5. Are Holes 5–9 concepts directionally right?

No further work will happen until Andrew responds.
