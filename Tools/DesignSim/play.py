"""Monte-Carlo 'player' on top of golfsim: plays a hole with a simple aim-at-waypoints policy plus assumed strike scatter."""
import math, random, sys
sys.path.insert(0, ".")
from golfsim import *

def ideal_speed(course, pos, target, lag, ang=None):
    if ang is None: ang = aim_at(pos[0], pos[1], target[0], target[1])
    want = dist(pos, target) + lag
    lo, hi = 0.15, 8.8
    for _ in range(22):
        mid = (lo + hi) / 2
        r = course.shot(pos[0], pos[1], ang, mid, use_cup=False, use_walls=False)
        if dist(pos, (r.x, r.y)) < want: lo = mid
        else: hi = mid
    return (lo + hi) / 2

def plan_stroke(course, pos, target, lag):
    """Noise-free aim and speed that put the ball `lag` beyond the target, reading any cross-slope (the player 'reads the break')."""
    want = aim_at(pos[0], pos[1], target[0], target[1])
    ang = want
    for _ in range(4):
        spd = ideal_speed(course, pos, target, lag, ang)
        r = course.shot(pos[0], pos[1], ang, spd, use_cup=False, use_walls=False)
        got = aim_at(pos[0], pos[1], r.x, r.y)
        err = (want - got + 180) % 360 - 180
        ang += err
    return ang, ideal_speed(course, pos, target, lag, ang)

def play(course, tee, policy, player, rnd, max_strokes=9):
    sa, sv = PLAYERS[player]
    pos = tee; strokes = 0; oobs = 0
    while strokes < max_strokes:
        strokes += 1
        plan = policy(pos, strokes)
        if plan[0] == "trick":            # ("trick", angle, speed): a ricochet line the player must read (extra bias on top of execution noise)
            ba, bv = BANK_READ[player]
            ang, spd = plan[1] + rnd.gauss(0, ba), plan[2] * max(0.2, 1 + rnd.gauss(0, bv))
        else:                             # (tx, tz, lag)
            tx, tz, lag = plan
            ang, spd = plan_stroke(course, pos, (tx, tz), lag)
        r = course.shot(pos[0], pos[1], ang + rnd.gauss(0, sa), min(SPEED_CAP[player], spd * max(0.2, 1 + rnd.gauss(0, sv))))
        if r.status == HOLED: return strokes + oobs, strokes == 1, 0
        if r.status == OOB: oobs += 1; continue          # +1 stroke, ball returns to where it was struck from
        pos = (r.x, r.y)
    return max_strokes + oobs + 1, False, oobs

def stats(course, tee, policy, par, n=400, seed=7, players=("casual", "regular", "expert")):
    out = {}
    for pl in players:
        rnd = random.Random(seed); tot = 0; hio = 0; atpar = 0; over2 = 0; dist_ = {}
        for _ in range(n):
            s, h, _o = play(course, tee, policy, pl, rnd)
            tot += s; hio += h; atpar += s <= par; over2 += s >= par + 2
            dist_[s] = dist_.get(s, 0) + 1
        out[pl] = dict(mean=tot / n, hio=hio / n, par_or_better=atpar / n, double_or_worse=over2 / n)
    return out

def show(title, res):
    print(title)
    for pl, d in res.items():
        print(f"  {pl:8s} mean {d['mean']:.2f}  hole-in-one {d['hio']*100:4.0f}%  par-or-better {d['par_or_better']*100:3.0f}%  double-bogey+ {d['double_or_worse']*100:3.0f}%")
