# Hole Design Sprint 001: Tropical Adventure, Gameplay-First Course Redesign (revision 2)

Status: **DESIGN ONLY. Submitted for Andrew and ChatGPT HQ review.** Nothing was implemented. No Unity scene, playable geometry, ball physics, terrain, shader, material or asset was changed, and Graphics Pass 3 has not started. The repo additions are this document and `Tools/DesignSim/` (an offline paper model of the ball physics).

> **Update:** the hole *concepts* in §4 were judged too conservative by HQ and are superseded by `HOLE_CONCEPTS_PASS2.md`. The assessment of the built holes (§3), the physics investigation (§5), the library and order (§6) and the simulator notes (§7) remain the engineering reference.

Revision 2 replaces the first revision, which was written against a three-hole build. It is based on `f64c8b8` (Hole 4 "Hollow Drop" built) and includes three rounds of HQ direction: replace the "shape is the identity" approach with mechanics; keep Holes 1–4 approachable but give them *simple versions* of the interesting mechanics; make ramps, jumps, elevation, tunnels and banks part of the everyday vocabulary; and investigate what the physics needs for airborne balls, stacked surfaces and enclosed pipes.

> "A hole's shape is not its gameplay identity."
> "One strong mechanic executed beautifully is preferable to five poorly combined gimmicks."
> "Build the interesting mechanics once. Reinvent their appearance seven times."

---

## 0. Read this first

1. **What exists.** Four holes are built: 1 *Beach Warm-up* (par 2), 2 *Palm Corner* (par 3), 3 *Jungle Crossing* (par 3), 4 *Hollow Drop* (par 4). This document uses the working names HQ used (Palm Landing, The Fork, Rope Bridge, Hollow Drop) for the *redesigned* versions; the built names are given in §3.
2. **The big technical finding (§5).** The ball is a real PhysX rigidbody, so the physics is already fully 3D. The "2.5D" limit is in the *generators* (one height function, rails on every boundary), not the ball. Airborne balls, stacked surfaces and tunnels are mostly a **geometry-authoring and rules** problem, not a ball-physics rewrite. Enclosed pipes are the one place the ball code itself fights us, and the recommended answer is a teleport-style "transport pipe", not simulated tube physics.
3. **Everything numeric is indicative.** Player scatter in the simulator is assumed (casual 6° / 20% speed, regular 3.5° / 13%, expert 1.8° / 7%). Airborne balls and cup lip-outs are not modelled. The policy players are deliberately simple and conservative, so absolute stroke counts run high; *relative* comparisons are the useful output. Treat every success rate as a hypothesis for Unity and Steam Frame testing.
4. **Nothing here changes `GolfBall` physics.** Where a concept needs an engine change it is flagged with a tier (§6) and is a decision for Andrew.

---

## 1. Design vocabulary and principles

**The vocabulary** (the parts every hole is built from, each reskinned per island):

| Part | What the player does with it | Simple version | Richer version |
|------|------------------------------|----------------|----------------|
| Ramp / elevation | Reads pace; uphill needs power, downhill needs touch | Gentle rise or drop | Terraces, drop-and-climb |
| Bank | Ricochets off an angled or curved surface | Optional pocket or kicker | Bank that is the only route |
| Tunnel / arch | Plays blind through a short roofed section | 0.8 m arch on a shortcut | Long tunnel with a skylight and a funnel exit |
| Launch (jump) | Deliberately lofts the ball across a gap | Tiny hop over a low log | Ravine or lava gap with a landing pad |
| Stacked level | Puts the ball above or below another route | Upper deck over a lane | Two-level hole with a ramp between |
| Transport pipe | Ball enters one mouth, leaves another | Short pipe under a wall | Pipe that skips a hazard or a level |
| Moving obstacle | Timing | Slow gate | Gate that also deflects banks |
| Hazard | Risk with a visible price | Water edge | Lava channel beside a safe route |

**Principles**

- **Invite an unexpected shot.** Most holes should contain one optional line that a curious player tries on purpose: a bank, a launch, a roll-through. It should be visible from the tee and never required.
- **Simple versions early.** Every mechanic appears first in a forgiving form so the player learns it by playing, then returns later in a richer form.
- **A shortcut must cost more than one stroke to miss.** Otherwise it dominates the safe route (sim: tunnel and gap shortcuts won at every skill level when a miss cost one stroke).
- **A bank only matters when the direct line is blocked or riskier.**
- **Funnels are the main forgiveness lever.** A cone profile of about 11% slope captures; smooth cosine bowls under-capture; rounded crests create rest zones, so use peaked crests.
- **The safe route always exists and is readable.** Risk is opt-in.
- **Breathing room is designed in.** Not every hole is a puzzle.
- **Not an obstacle carnival.** One headline mechanic per hole; later holes combine two learned mechanics, not four new ones.

---

## 2. Mechanics ladder and course progression

`●` headline mechanic, `○` supporting or optional, `·` absent. "Build now" means the current engine can do it; tiers T1a–T3 are defined in §6.

| # | Hole (island) | Par | Ramp/elev. | Bank | Tunnel | Launch | Stacked | Pipe | Moving | Hazard | Needs |
|---|---------------|-----|-----------|------|--------|--------|---------|------|--------|--------|-------|
| 1 | Palm Landing (Start) | 2 | ● | ○ | · | · | · | · | · | · | Build now (+T1a for the bank) |
| 2 | The Fork (Start) | 3 | ○ | ○ | ● | · | · | · | · | · | Build now (+T1a) |
| 3 | Rope Bridge (Jungle) | 3 | ○ | ● | · | ○ | · | · | · | ○ fall = recovery | T1a; T1b for the jump |
| 4 | Hollow Drop (Jungle) | 4 | ● | ○ | · | · | · | · | · | · | T1a for kickers |
| 5 | Terrace Steps (Temple) | 3 | ● | ○ | ○ | ● | · | · | · | · | T1b |
| 6 | Spirit Gate (Temple) | 3 | ○ | ○ | ● | · | · | · | ● | · | T2a |
| 7 | Lava Run (Volcanic) | 4 | ○ | ● | · | ○ | · | ● | · | ● | T1c, T2c |
| 8 | The Furnace (Volcanic) | 4 | ○ | ○ | ○ | ● | ● | · | ○ | ○ | T1b, T2b |
| 9 | Summit Sanctuary (Summit) | 5 (prov.) | ● | ○ | ○ | ○ | ○ | ○ | · | · | Reuse only |

Difficulty is a wave: **1 easy, 2 easy, 3 medium (recoverable), 4 medium (pace), 5 relaxed breather with a showpiece, 6 medium (timing), 7 spike, 8 demanding (combination), 9 long and celebratory.** Course par is 2+3+3+4+3+3+4+4+5 = 31.

Skill emphasis alternates: power (1) → route choice (2) → precision (3) → pace (4) → elevation (5) → timing (6) → risk judgment (7) → mastery (8) → endurance and flourish (9).

---

## 3. Assessment of the four built holes

Simulator baselines use the real geometry (`Tools/DesignSim/asbuilt.py`, `asbuilt_h4.py`). Players: casual / regular / expert.

### Hole 1, Beach Warm-up (par 2)

*As built:* straight 1.2 × 7.0 m lane, one smooth 8 cm rise between z 2.4 and 3.4, cup at z 6.2.
*Sim:* mean 2.77 / 2.35 / 1.94; par-or-better 41 / 64 / 94%; hole-in-one 4 / 5 / 12%.

| Principle | Verdict |
|-----------|---------|
| Banks and ricochets | None. The lane is too narrow for a bank to matter. |
| Risk versus reward | None; there is only one line. |
| Elevation and momentum | One small rise. Good for teaching power. |
| Natural obstacles | None in play. |
| Alternate routes | None. |
| Forgiving recovery | Excellent: no OOB, rails return everything. |
| Difficulty | Right for a tutorial (par reachable by most). |
| Identity | Weak: it is a test lane. |

**Verdict: keep the approachable lane, add identity cheaply.** Do not complicate it. See §4.

### Hole 2, Palm Corner (par 3)

*As built:* L-shaped dogleg, 1.2 m lanes, a 5 cm mound on the inside of the corner, 4% fall toward the cup.
*Sim:* mean 3.75 / 3.21 / 2.65; par-or-better 45 / 69 / 96%.

It plays fine and almost nobody is surprised by it: one line, one corner, and the only decision is where to aim at the corner. The mound is too low to change a decision. **Verdict: replace the L with a split route** (HQ approved).

### Hole 3, Jungle Crossing (par 3)

*As built:* elbow, a 3.6 m bridge with an 18 cm smooth hump, landing pad, final lane to a cup that is not in line, 12 cm rails.
*Sim:* mean 6.6 / 5.9 / 5.3; par-or-better 0 / 0 / 0%; double-bogey or worse 93 / 91 / 82%. A firm putt crosses and a soft one does not, and the speed window is narrow and non-monotonic because a smooth crest creates a rest zone near the top. These means are inflated by the simple policy players, but a par-3 result of 0% even for the expert policy means par 3 is not reachable as built.

| Principle | Verdict |
|-----------|---------|
| Banks | Present in principle (bank off the east rail into the final lane), but the lane is too short to teach it. |
| Risk versus reward | Weak: falling off is just a punishment. |
| Elevation and momentum | The hump is the idea; it is a smooth bump with a narrow window. |
| Natural obstacle | The ravine is a real feature. Good. |
| Alternate routes | None. |
| Recovery | Poor: a ball that leaves returns to the last rest spot with no shorter, safer option. |
| Identity | Strong *place*, weak *play*. |

**Verdict: keep the ravine and bridge, redesign the approach** (HQ approved).

### Hole 4, Hollow Drop (par 4)

*As built:* S-shaped 1.2 m lane: tee lane (4 m), turn east (width 5.2), **descent lane** with a 24 cm drop (smoothstep, peak slope about 14%) from z 5.6 to 8.2, a low basin that turns back west, then a 1.4 m final lane with a **10% climb** (peak about 15% because of the smoothstep) to a cup raised 10 cm. A putt too soft for the climb rolls back into the basin with no penalty.

*Sim:*

| | Strokes with perfect execution | Mean (casual / regular / expert) |
|---|---|---|
| As built | 5 | 8.4 / 7.2 / 6.3 |
| With a kicker at the first elbow | 4–5 | 7.3 / 6.5 / 5.3 |
| Kicker + gentler climb | 4–5 | 7.1 / 6.4 / 5.3 |
| Kicker + gentler climb + softer drop (8.8 m ramp, 20 cm) | 4–5 | 7.1 / 6.4 / 5.3 (no further gain) |

These absolute means are inflated by the simple policy players, but the structural finding is real: **the hole has five straight legs, so par 4 is unreachable even with perfect play**, and every leg is a 1.2 m lane where scatter produces rail rebounds. The *momentum* idea is genuinely good (the drop speeds the ball up, the climb punishes a timid putt), but nothing in the layout lets a player *use* that momentum cleverly.

| Principle | Verdict |
|-----------|---------|
| Banks | None (all legs axis-aligned; the end walls just stop the ball). |
| Risk versus reward | None: one path. |
| Elevation and momentum | **Strong**: the drop and the climb are the best use of elevation in the course. |
| Natural obstacle | The basin is a good natural feature. |
| Alternate routes | None. |
| Recovery | Good: nothing is OOB and a short climb rolls back. |
| Identity | Good ("the valley hole"), but each corner is just an elbow. |

**Verdict: keep Hollow Drop. Do not replace it with the tunnel idea.** The drop-and-climb is the identity and it is already built. The smallest fix that adds decisions is to turn the two elbows into **kickers** (angled bank walls) so a good shot can chain legs, and to soften the climb so the 15% peak is nearer 9%. This is a hybrid in spirit only: the tunnel concept is retired, but its idea of "a line that rewards nerve" returns as the elbow kickers and the downhill run-in.

**Headset feedback (Andrew, recorded in `DEVELOPMENT_LOG.md`):** Hollow Drop is "too long with too many turns to reach the cup in 4 strokes", and the holes feel "flat, generic, and lack obstacles". This independently confirms the simulator's five-legs finding. It sharpens the recommendation: **remove a turn, do not just soften the ones that exist.** The cheapest way is to fold the tee lane and the first turn into one banked elbow (kicker) so the S becomes tee, descent, basin-and-climb, which is three to four natural strokes. If the kickers (T1a) are not ready, shorten the tee lane to about 2 m and widen the lanes to about 1.5 m, and set par 5 until it is tested.

### Course-level read of Holes 1–4

Today the first four holes are four lanes with turns. Elevation is the only real mechanic, and it appears in all four. The design problem is not difficulty; it is that **no hole offers an optional unusual shot**. The smallest changes (§4) add exactly one invitation per hole.

---

## 4. Hole designs

Diagram legend: `#` rail or wall, `T` tee, `O` cup, `~` ravine, water or lava, `=` bridge deck, `/ \` angled bank, `^ v` slope, `[ ]` tunnel roof, `>>` ball line. Horizontal scale is about 0.25 m per character. Side views are exaggerated vertically.

### Hole 1, Palm Landing (par 2)

**Concept:** an approachable dune lane that teaches pace, with one visible optional trick.

```
 top-down                           side view (exaggerated)
   ########                                     crest   O
   #  O   #                          ____------\___   __/
   #      ###  <- sand pocket       T            \_/ (small trough before the cup)
   #       /     with angled wall
   #  ^^^  #     (bank)
   #      /#
   #  T   #
   ########
```

- **Primary feature:** the 8 cm dune rise (keep), plus a **sand pocket** off the lane's right side at about two-thirds distance.
- **Safe route:** straight putt up the lane. Par 2 for most players.
- **Experiment:** bank off the pocket's angled back wall for a line to the cup that ignores the rise. It saves nothing, but it feels clever, and it teaches that angled walls exist.
- **Optional hop (T1b, later):** a 6° "driftwood" lip that a firm shot can hop. Do not build unless Hole 3's jump proves out.
- **Expected difficulty:** 1/5.
- **Missed shots:** too soft rolls back down the rise (no penalty); too hard hits the back rail and returns.
- **Memorable because:** it is the first place the player discovers the course has secrets.
- **Needs:** one angled bank wall (T1a). Without T1a, ship the plain lane; nothing is lost.

### Hole 2, The Fork (par 3)

**Concept:** the first real route decision, with a short tunnel on the shortcut.

```
 top-down                                    shortcut side view
   #########################                  roof ___________
   #   O                   #               T ___|  [ tunnel ]  |___ O
   #  ####        ########  #
   #  # [ ]       #  long   #
   #  # [ ] island#  forgiving
   #  #  \ #######  route   #
   #  #   shortcut  ########
   ####      T      #
```

- **Primary feature:** a rock island splits the lane. The **wide route** goes around (long, forgiving, gentle slope). The **shortcut** is narrower (about 0.6 m) and passes through a **0.8 m rock arch**.
- **Safe route:** the wide outer lane, a plain two-putt to par 3.
- **Experiment and reward:** the arch shortcut gives a realistic par-2 chance. A player can also bank off the island's far face onto the shortcut; to keep that from dominating, the near face is set at an angle that sends misses toward the wide route.
- **Expected difficulty:** 2/5 safe, 3/5 shortcut.
- **Missed shots:** a miss at the arch mouth rebounds to the fork (cost about one stroke), never OOB.
- **Tunnel design rule:** the arch is short, the floor is flat, and one side has a cut-out window so the player can see the ball. A ball must never be able to stop where the player cannot reach it.
- **Memorable because:** the first time the player chooses, and the first time the ball disappears briefly and re-emerges.
- **Needs:** a roof collider over an existing lane section (build now). Balance note from the sim: a bank on the island wall dominated both routes, so lengthen the outer legs to about 3.0–3.4 m or give the island's near face a shallow angle.

### Hole 3, Rope Bridge (par 3)

**Concept:** make the bridge matter. A banked, funnelled approach; a peaked crest; a recovery strip; and an optional jump for the curious.

```
 top-down
   ###############################
   #  clearing          ~~~~~~   #
   #  T  ///             ~~~~~=====O
   #     ///  guide      ~~~~ bridge  pad
   #         wall        ~~~~~~~~    #
   #####   recovery strip (low) #######
       ###########################

 side view (peaked A-frame crest, not a smooth hump)
                  /\
        _________/  \_________
   T __/  funnel  mouth       \__ pad __ O
                 ravine
   ---------- recovery strip (lower) ----------
```

- **Primary feature:** the bridge with a **peaked** crest (a smooth hump creates a rest zone) and a **funnel mouth** (cone profile of about 11%) so a roughly aimed ball is guided onto the deck. Angled guide walls in the approach clearing turn a slightly wide shot into a straight entry.
- **Safe route:** a moderate, centred putt along the guide wall.
- **Experiment:** a short **launch ramp** at the ravine's narrow upstream end lets a firm shot fly the gap and land on the pad. A miss falls into the ravine. This is the first airborne shot and it is entirely optional.
- **Expected difficulty:** 3/5.
- **Missed shots:** a ball that leaves the bridge lands on the **recovery strip** (a lower, safe ledge beside the ravine). It costs the usual penalty stroke but the replay position is one pushed putt away from the bridge, not a long walk.
- **Sim:** a plain wide mouth still crosses only about 34–37% of the time (mean 4.4 / 4.0 / 3.6), which is why the funnel and the guide are essential; the final values are for Local to tune.
- **Memorable because:** the bridge is the hole, and the player can try to leap the ravine.
- **Needs:** T1a for the guide walls, T1b for the jump. The bridge, funnel, peaked crest and recovery strip need nothing new.

### Hole 4, Hollow Drop (par 4, retained)

**Concept:** the valley hole, kept as built. Momentum is the identity. The change is small: let the S-bends become kickers so a good shot can ride the drop.

```
 top-down                                   side view
   ##########                                T_______ _
   #   #  #   # O                                    \
   #   #  #  ###  <- gentler 9% climb                  \_______  (drop 24 cm)
   #  T   #  #                                                 \_____ basin __/--O
   # ^ ###/  # (kicker at the first elbow)
   ##########
```

- **Primary feature:** the 24 cm drop and the climb (keep).
- **Change 1, kickers (T1a):** a 45° bank wall at the first elbow, and one at the basin corner. A hard shot up the tee lane now bounces east into the descent without a second stroke; a ball rolled down the drop rides the basin kicker west toward the final lane.
- **Change 2, softer climb:** lengthen the climb from 1.0 m to about 1.6 m so its peak slope is about 9% (not 15%); the ball still rolls back from a timid putt but a medium putt crests.
- **Safe route:** four controlled strokes through the lanes.
- **Experiment:** "ride the drop": the right pace down the descent hits the basin kicker and carries most of the way to the climb, saving a stroke.
- **Expected difficulty:** 3/5 with kickers and a wider lane, 4/5 as built. Be honest about the sim: even with the elbow kicker the expert policy reaches par 4 only about 13% of the time, and the softer drop changed nothing. The kicker removes about one stroke; it does not make par 4 easy. **Recommend par 5 for the hole as built, or par 4 only after a Unity test with both kickers and a wider (about 1.5 m) lane.** Hollow Drop's job is pace, not a tight par.
- **Missed shots:** timid climb rolls back to the basin (no penalty); too fast skips past the cup into the back wall.
- **Memorable because:** the ball *feels* the valley. This is the hole where the course teaches "gravity is part of the shot".
- **Needs:** T1a for the kickers; the climb edit needs nothing.

### Hole 5, Terrace Steps (Temple Island, par 3), preliminary

**Concept:** elevation as a showpiece, and the first real jump.

```
 side view
                              ___________ O
                    ______   /  upper terrace
         T ________/ lower\ /<- ramp (15-20 degrees)
                  terrace  gap  (launch lip over a pool)
```

- **Primary feature:** two terraces. A **long switchback ramp** reaches the upper terrace safely. A **launch ramp** on the lower terrace can fly a shallow gap onto the upper terrace directly.
- **Safe route:** the switchback (two to three strokes).
- **Experiment:** the jump. At 20° and 3.5 m/s the ball flies about 0.8 m with an apex near 7 cm, so the gap is 0.5 m wide with a funnelled landing.
- **Difficulty:** 2/5.
- **Missed shots:** a short jump drops into the pool (the lower ledge): a one-stroke recovery and the switchback is still available.
- **Memorable because:** the first deliberate jump, and the view from the upper terrace.
- **Needs:** T1b.

### Hole 6, Spirit Gate (Temple Island, par 3), preliminary

**Concept:** the first moving obstacle, kept slow and readable.

```
 top-down
   #############
   # T  [  |  ]  O   <- slow rotating gate (about 8 s per turn)
   #############        tunnel behind it
```

- **Primary feature:** a slow rotating gate in front of a short tunnel. The gate is a visible pattern, not a reflex test.
- **Safe route:** wait and putt through the gap.
- **Experiment:** deliberately bank a hard shot off the stationary rail to skip the timing.
- **Difficulty:** 3/5.
- **Missed shots:** deflects back toward the tee.
- **Memorable because:** timing a ball is satisfying, and the gate has weight because it transfers velocity.
- **Needs:** T2a (velocity transfer on the moving collider).

### Hole 7, Lava Run (Volcanic Island, par 4), preliminary

**Concept:** risk and reward around a lava channel, with the first transport pipe.

```
 top-down
   ###############
   #  ****  O    #   * = lava (+1 stroke, return)
   #  ****       #
   #  T ===(pipe)=>   <- the pipe skips the lava
   ###############
```

- **Primary feature:** a **transport pipe** that crosses a lava channel. The mouth sits beside a lava edge.
- **Safe route:** the long outer ledge, three controlled strokes.
- **Experiment:** a bank off an angled obsidian wall into the pipe mouth.
- **Difficulty:** 4/5.
- **Missed shots:** lava costs a stroke and returns the ball.
- **Memorable because:** the pipe is a spectacle: the ball vanishes, glows through a glass section, and pops out beyond the lava.
- **Needs:** T1c (hazard volume) and T2c (teleport pipe).

### Hole 8, The Furnace (Volcanic Island, par 4), preliminary

**Concept:** a combination of already-learned mechanics, with one new arrangement: a stacked walkway.

- **Primary feature:** an upper walkway above a lower channel. The upper level is a bridge-style run; the lower channel contains a launch ramp over a lava gap and ends in the final bowl.
- **Safe route:** the upper walkway, long and forgiving, with a gentle ramp down to the cup.
- **Experiment:** launch from the lower channel across the lava, or drop from the upper deck to the lower channel on purpose to skip the ramp.
- **Difficulty:** 4/5.
- **Missed shots:** recovery ledges at the lava; a ball on the lower level is a legitimate play position.
- **Memorable because:** the first time the course has two levels in the same footprint.
- **Needs:** T2b (stacked surfaces), T1b.

### Hole 9, Summit Sanctuary (Summit, par 5, provisional)

**Concept:** a long celebratory finale with an elevated approach and the Sunset Bowl as the last act. The bowl is the finish, not the whole hole.

```
 side view
                                                  _______
                                          ______/ climb \___ ___ bowl /
          T ____ terrace 1 ___ terrace 2 /                  \__ O __/
```

- **Approach (three legs):** terrace 1 (an open, gentle shot); a ramp up to terrace 2 with an optional tunnel shortcut; a final switchback to the rim of the bowl.
- **Optional shortcuts:** the tunnel from terrace 1 to terrace 2, and a launch ramp over a rim gap.
- **Finale:** the Sunset Bowl, a wide cone (about 11% slope) that sends any ball arriving on the rim toward the cup, with a visible sunset behind it.
- **Safe route:** terraces and switchbacks, five relaxed strokes.
- **Difficulty:** 2/5 per shot, long overall.
- **Memorable because:** the whole course resolves in a bowl that always ends well.
- **Needs:** reuse of earlier parts; no new library parts.

---

## 5. What the physics needs: airborne balls, stacked surfaces and pipes

This section is the result of reading `GolfBall.cs`, `PlayableSurface.cs`, `HoleController.cs`, `Cup.cs`, `CourseGeometry.cs`, `HoleFactory.cs` and `VRRig.cs`. The conclusions are about *design feasibility*; nothing was run in Unity.

### 5.1 How the ball actually works

- A PhysX `Rigidbody` + `SphereCollider`, gravity applied by script (`useGravity = false`), continuous dynamic collision, 50 Hz fixed step (0.02 s), radius 2.25 cm, `maxBallSpeed` 9 m/s.
- Each fixed step the script classifies contacts by normal: `n.y ≥ 0.6` is **ground** (about 53° or flatter), everything else is **wall**. Walls get a scripted rebound from the *pre-contact* velocity; ground contacts apply rolling resistance, 5/7 g slope acceleration and a rest-hold rule.
- When there is **no ground contact** the script just integrates gravity. A comment in the code states the intent: *"the ball can roll over edges and leave ramps."* So an airborne ball is already modelled correctly and deterministically: no air drag, parabolic flight.
- On landing the perpendicular velocity into the surface is zeroed (dead landing, no bounce), then rolling resumes.

### 5.2 Airborne balls (jumps): design feasibility is good; four things are missing

| Gap | Why it matters | Smallest fix |
|-----|----------------|--------------|
| **Rails on every boundary.** `BuildWalls` puts a rail on every edge of the green union, including the end of a launch ramp and the near edge of a landing pad. | The ball would hit a rail at the lip instead of leaving. | A per-edge "open" flag (or a rectangle list of open edges) in `GreenLayout`, so the rail is skipped at a lip and at a landing edge. |
| **Rest rules.** `IsAtRest` requires `IsGrounded`; `PlayableSurface.IsUnder` checks only a ray straight down. | A ball resting in the gap or on a non-playable ledge is out of bounds; that is *desired* for a hazard but must be deliberate. | Tag landing pads and recovery strips `PlayableSurface`; leave gaps and ravine floors untagged (penalty) or tagged (free recovery) as the hole requires. |
| **Landing area.** The ball's range depends on take-off speed squared. | Casual scatter of ±20% in speed is about ±40% in jump length. | Design jumps with landing zones at least 1.2 m deep or with a funnel, and keep gaps short for casual players. |
| **Test coverage.** No test launches the ball. | Without it, any engine change could break flight silently. | A PlayMode test that rolls a ball off a ramp at fixed speeds and checks the landing within a tolerance. |

**Flight numbers (frictionless, level landing, range `v² sin 2θ / g`, apex `v² sin² θ / 2g`):**

| Ramp angle | Take-off speed 2.5 m/s | 3.5 m/s | 5.0 m/s |
|------------|------------------------|---------|---------|
| 15° | 0.32 m, apex 2 cm | 0.62 m, apex 4 cm | 1.27 m, apex 9 cm |
| 20° | 0.41 m, apex 4 cm | 0.80 m, apex 7 cm | 1.64 m, apex 15 cm |
| 25° | 0.49 m, apex 6 cm | 0.96 m, apex 11 cm | 1.95 m, apex 23 cm |

Consequences for design: jumps are small and low (apexes of 2–10 cm), so **the landing edge must be rail-free or lower than the lip**, otherwise the 9–12 cm rails stop the ball. A gap of about 0.5 m is friendly; 1 m is for experts. Speed lost climbing the ramp (5/7 g sin θ) must be subtracted from the putt speed, so a jump needs a *run-up* of 1–2 m.

### 5.3 Stacked putting surfaces: PhysX supports it; the generators do not

- **What already works:** PhysX has no problem with two colliders above each other. `PlayableSurface.IsUnder` raycasts straight down from the ball, so it correctly finds the deck the ball is on. `LastRestPosition`, the stroke penalty and the stuck-ball safeguard are all 3D. The kill plane is 3 m below the hole origin, so lower levels are safe if they stay within 3 m. **The VR rig already lifts the player to ball height** (`TeleportToBall` sets feet to `ball.y − radius`), so a ball on an upper level can be played without a new locomotion system.
- **What does not:** `GreenLayout` has exactly one height function and one set of rectangles, and `BuildSurface` / `BuildWalls` build one surface and one rail loop from it. There is no notion of a second level, a connector, or an overhang.
- **Needed (T2b):**
  1. A `GreenLayout` that holds a list of **levels**, each with its own rectangles, height function and rails, plus **connectors** (ramps) marked as the only legal transitions.
  2. A minimum **clearance** of 0.25 m between a deck and anything under it, so the ball and the putter head have room.
  3. A rule for **resting under an overhang**: either forbid it (underpasses are run-through, floored with a slope that does not hold, and short) or add a `NoRestZone` that moves the ball to the exit with no penalty.
  4. Terrain: the island plateau generator must cover the *vertical* footprint; retaining walls are decorative.
  5. `NoSceneryOnGreens` must treat both levels as greens.
- **Player-view caveat:** a ball under an upper deck is hard to see. Keep stacked sections short and give them cut-away windows. Headset testing decides how much overhang is comfortable.

### 5.4 Tunnels and arches: buildable now

A tunnel is a **roof collider over an existing lane**, with the walls already provided by the lane's rails. Requirements: 0.12 m minimum headroom (the rails are 9–12 cm; the ball is 4.5 cm across), a floor that does not trap the ball, a window so the player can see it, and `GolfBall` unchanged. The only rule worth adding is that a ball that stops inside a tunnel is handled (a short tunnel plus a slight exit slope, or a `NoRestZone`).

### 5.5 Bankable surfaces: no physics change needed

The ball's wall rebound uses the real contact normal, so an angled or curved wall collider will deflect it correctly *today*. What is missing is **authoring**: `BuildWalls` only makes axis-aligned rails. The fix (T1a) is a `BankWall` component that extrudes a polyline into a wall mesh and collider, placed as a child of the hole. Curves must be faceted at 5° or less so the ball does not catch on facet edges. Remember the rebound property: outgoing angle is *shallower* than the mirror angle (tan β = 1.306 · tan α).

### 5.6 Enclosed ball-transport pipes

Two ways to build one:

**Option A, a teleport-style transport pipe (recommended).**
Entry trigger at the mouth; the ball becomes kinematic (`GolfBall.FixedUpdate` already returns early when `isKinematic`); a visible ball (or the real one, rendered) follows a spline through a translucent or glass tube for `length / speed` seconds; the ball is released at the exit with a chosen velocity. Design requirements:
- The mouth needs a lip and a rising floor so a slow ball rolls back out (a ball must not rest on the mouth).
- Exit speed is *clamped* (for example 1.2–3.5 m/s) so the result is consistent and readable.
- `HoleController` state stays correct during transit: `InPlay` remains true, the rest and stuck-ball timers do not run, `Struck` is not re-raised, and OOB and reset are ignored until release.
- It supports vertical drops, loops and crossing other levels for free, because nothing simulates the inside.
- Cost: low to moderate; no change to ball physics.

**Option B, a physically simulated tube (not recommended first).**
- The ball code classifies contacts by normal: inside a round tube the contact normal sweeps through the 0.6 threshold, so a rolling ball would get "wall" rebounds (restitution 0.72, tangent keep 0.94) every step and lose energy erratically.
- Fixes: a rectangular or flat-bottomed tube; a "conduit surface" tag that treats all contacts as ground; handling of rest inside the pipe; a minimum inner width for a 4.5 cm ball with scripted contact offsets; and an extensive test matrix.
- Cost: high, and it risks regressing the feel of every other hole.

**Recommendation:** build Option A first. If HQ later wants a visible physical pipe, use a short, flat-floored, open-topped channel (which is just a rail lane) and reserve Option B for a tested special case.

### 5.7 Moving obstacles, hazards and timing

- **Moving barriers:** a kinematic `Rigidbody` moved in `FixedUpdate`. The ball's wall rebound currently uses only the ball's pre-contact velocity, so a moving wall needs the **relative** velocity (`v − v_wall`) and the rebound is `v_wall + reflect(v_rel)`. Speed should be capped (about 1.2 m/s) and barriers kept in open space away from rails to avoid crushing a ball against a wall (depenetration is capped at 0.5 m/s).
- **Hazard volumes:** a trigger that calls the existing OOB path (`HoleController.BallOutOfBounds`), so the penalty and the return-to-rest rules already exist. This is a small, safe addition.

### 5.8 Cross-cutting items

- **Collider thickness:** at 9 m/s and 50 Hz the ball moves 18 cm per step. Continuous dynamic detection handles this against static mesh, but roofs and thin guides should be at least 1 cm thick.
- **Functional versus decorative separation:** every functional collider goes in a `Functional` child under `HoleController`; every decorative mesh is a sibling with no collider. The existing `NoSceneryOnGreens` test already enforces the second half.
- **Simulator:** `DesignSim` does not yet model flight. A small 3D extension (gravity, lip, landing) would make jump placement testable on paper.
- **Tests to add with each tier:** airborne landing tolerance (T1b), bank deflection angle (T1a), no-rest-in-tunnel (T0), stacked-route reachability (T2b), pipe transit timing and state (T2c), moving-barrier velocity transfer (T2a).

---

## 6. Reusable obstacle library and implementation order

| Tier | Part | Functional geometry | Decorative layer | Engine change | Effort | Enables |
|------|------|--------------------|------------------|---------------|--------|---------|
| **T0** | Ramps, rises, funnels, recovery strips, bridge, split junction, tunnel roof | Height function, rectangles, a roof collider | Rock, planks, sand | **None** | Low | Holes 1, 2, 3 (non-jump), 4 (climb), 9 (reuse) |
| **T1a** | Angled and curved bank walls | Extruded polyline wall + collider | Rock, vine, driftwood | New authoring component; ball physics unchanged | Medium | Holes 1, 2, 3, 4, 7 |
| **T1b** | Open edges and launch ramps | Per-edge rail opt-out; landing pad | Log, rock lip | `GreenLayout` open edges; landing rules; a flight test | Medium | Holes 3 (jump), 5, 8, 9 |
| **T1c** | Hazard volumes | Trigger polygon calling OOB | Lava, water | Small trigger component | Low | Holes 3 (fall), 7, 8 |
| **T2a** | Moving barriers | Kinematic collider with velocity | Idol, gate | Relative-velocity rebound in `GolfBall` | Medium-high | Hole 6 |
| **T2b** | Stacked surfaces | Multi-level `GreenLayout`, connectors, clearance | Deck, pillars | New generator logic; terrain footprint; rest rules | High | Hole 8, Hole 9 (optional) |
| **T2c** | Teleport transport pipe | Entry/exit triggers, spline, state handling | Glass tube, glow | Ball hold and release; `HoleController` integration | Medium | Hole 7, Hole 9 |
| **T3** | Physical pipe | Conduit collider and contact tag | Same | `GolfBall` contact classification | High | Not recommended first |

**Recommended order**

1. **T0 pass on Holes 1–4**, as authored changes only: Hole 2 (split with the arch), Hole 3 (peaked crest, funnel, recovery strip), Hole 4 (softer climb), Hole 1 (nothing structural).
2. **T1a banks**, then re-apply to Holes 1, 2, 3, 4.
3. **T1c hazards**, then **T1b open edges and the jump test**; this unlocks the Hole 3 jump and Hole 5.
4. Build **Temple Island (Hole 5)** on T1b, then **T2a** moving barrier for Hole 6.
5. **T2c** teleport pipe, then **T2b** stacked levels, for Holes 7–9.

---

## 7. Simulator

`Tools/DesignSim/` keeps the C core (`golfsim.c`), the Python wrapper (`golfsim.py`), the validation script (`validate.py`, all lines PASS), the policy players (`play.py`) and the exploration scripts. The compiled `libgolfsim.so` is **not** in version control any more: it builds from source automatically on first import (`gcc -O2 -shared -fPIC -o libgolfsim.so golfsim.c -lm`), and `README.md` documents this. `asbuilt.py` and `asbuilt_h4.py` reproduce the baselines in §3.

Caveats: assumed scatter; no airborne flight; no lip-outs; the policy players aim at lane centres and are conservative, so absolute strokes run high. Use it to rank options, not to predict scores.

---

## 8. Decisions requested from Andrew and HQ

1. **Hole 4:** keep Hollow Drop, add kickers and a softer climb, and set par 5 until a Unity test shows par 4 is fair (recommended)? Or leave as built?
2. **Hole 2:** approve the split route with a short arch on the shortcut.
3. **Hole 3:** approve the peaked crest, funnel, recovery strip and guide walls, and the optional launch jump as a later layer.
4. **Bank walls (T1a):** approve building this component first.
5. **Launch jumps (T1b):** approve a rail opt-out in `GreenLayout` and a landing test. This touches the generator, not the ball.
6. **Pipes:** approve the teleport transport pipe (T2c) as the first pipe, and defer simulated tube physics.
7. **Stacked levels (T2b):** approve as a Hole 8 / Hole 9 goal, with underpass rules (short, windowed, no-rest) and the player-visibility caveat.
8. **Moving obstacles:** approve a velocity-transfer change to the rebound (T2a) at the time Hole 6 is built, not before.
9. **Hole 9:** approve the provisional par 5 with an elevated approach and the Sunset Bowl as the last act.
10. **Names:** confirm the working names (Palm Landing, The Fork, Rope Bridge, Hollow Drop, Terrace Steps, Spirit Gate, Lava Run, The Furnace, Summit Sanctuary) versus the built names (Beach Warm-up, Palm Corner, Jungle Crossing).
