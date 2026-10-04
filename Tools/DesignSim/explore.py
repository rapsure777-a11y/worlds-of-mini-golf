import math, random, sys
sys.path.insert(0, ".")
from golfsim import *

def mc(course, start, ang, spd, player, n=1500, seed=1, want=HOLED):
    rnd = random.Random(seed); sa, sv = PLAYERS[player]; ok = 0
    for _ in range(n):
        r = course.shot(start[0], start[1], ang + rnd.gauss(0, sa), spd * (1 + rnd.gauss(0, sv)))
        ok += r.status == want
    return ok / n

def best_shot(course, start, angs, spds):
    """Noise-free best (angle, speed) by the largest surrounding success region (3x3 neighbourhood score)."""
    ok = {}
    for a in angs:
        for s in spds:
            ok[(a, s)] = course.shot(start[0], start[1], a, s).status == HOLED
    best, bs = None, -1
    for i, a in enumerate(angs):
        for j, s in enumerate(spds):
            sc = sum(ok.get((angs[ii], spds[jj]), False) for ii in range(max(0, i - 3), min(len(angs), i + 4)) for jj in range(max(0, j - 3), min(len(spds), j + 4)))
            if sc > bs: bs, best = sc, (a, s)
    return best, bs, ok

def funnel(cx, cz, r, depth):
    def h(x, z):
        d = math.hypot(x - cx, z - cz) / r
        return depth * (0.5 + 0.5 * math.cos(math.pi * d)) * -1 if d < 1 else 0.0
    return h

def hole1(width=1.8, bowl_r=0.0, depth=0.0, cup_x=0.0, cross=0.0, kicker=None):
    hw = width / 2
    walls = [(rect(-hw, 0, hw, 7.6), True)]
    if kicker: walls.append((kicker, False))
    def h(x, z):
        t = min(1, max(0, (z - 2.4) / 1.0)); v = 0.08 * t * t * (3 - 2 * t)
        if cross: v += -cross * x * min(1, max(0, (z - 3.5) / 1.0))
        if bowl_r: v += funnel(cup_x, 6.7, bowl_r, depth)(x, z)
        return v
    return Course(walls, h, cup=(cup_x, 6.7))

if __name__ == "__main__":
    start = (0.0, 0.6)
    for name, c in [("as built-ish 1.2m lane, cup centre", Course([(rect(-0.6, 0, 0.6, 7), True)], lambda x, z: 0.08 * (lambda t: t * t * (3 - 2 * t))(min(1, max(0, (z - 2.4) / 1.0))), cup=(0, 6.2)))]:
        (a, s), sc, ok = best_shot(c, (0, 0.6), [i * 0.1 for i in range(-30, 31)], [2.4 + 0.05 * i for i in range(0, 30)])
        print(name, "best", a, round(s, 2), "score", sc)
        for pl in PLAYERS: print("  HIO", pl, round(mc(c, (0, 0.6), a, s, pl), 3))
