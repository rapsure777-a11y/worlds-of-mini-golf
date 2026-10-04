import sys; sys.path.insert(0,'.')
from explore import *

def hole1k(phi, z0, length=0.7, r=0.8, d=0.05, side=-1):
    """1.8 m lane with tide-pool funnel at (0,6.7) and a kicker: a straight rock face leaving the left wall at z0, angled phi degrees off the wall."""
    hw = 0.9
    x0 = side * hw
    a = math.radians(phi)
    p0 = (x0, z0)
    p1 = (x0 - side * length * math.sin(a), z0 + length * math.cos(a)) if False else (x0 + (-side) * length * math.sin(a), z0 + length * math.cos(a))
    walls = [(rect(-hw, 0, hw, 7.6), True), ([p0, p1], False)]
    def h(x, z):
        t = min(1, max(0, (z - 2.4) / 1.0)); return 0.08 * t * t * (3 - 2 * t) + funnel(0, 6.7, r, d)(x, z)
    return Course(walls, h, cup=(0, 6.7))

def scan(c, start=(0, 0.6)):
    best, bscore = None, -1
    angs = [i * 0.25 for i in range(-90, 91)]
    for a in angs:
        r0 = c.shot(start[0], start[1], a, 2.0)  # probe: does first wall contact happen at kicker? use hit coordinates
    res = []
    for a in angs:
        for s in [2.6 + 0.1 * i for i in range(0, 14)]:
            r = c.shot(start[0], start[1], a, s)
            if r.status == HOLED and r.hits >= 1:
                res.append((a, s, r.hits, r.first_hit_y, r.first_hit_x))
    return res

direct = hole1k(0, 3.5, length=0.001)
print("direct HIO (regular) at best nominal:")
(a, s), sc, _ = best_shot(direct, (0, 0.6), [i * 0.1 for i in range(-20, 21)], [2.6 + 0.05 * i for i in range(0, 25)])
print("  best", round(a, 1), round(s, 2), "-> casual/regular/expert", [round(mc(direct, (0, 0.6), a, s, p, n=1200), 2) for p in PLAYERS])
for phi in (10, 20, 30, 40):
    for z0 in (3.0, 3.6, 4.2):
        c = hole1k(phi, z0)
        res = scan(c)
        bank = [r for r in res if abs(r[4] - (-0.9)) < 0.8 and r[3] > z0 - 0.2 and r[3] < z0 + 0.8]  # first contact on the kicker
        if not bank: print(f"phi {phi} z0 {z0}: no bank HIO"); continue
        # best nominal among bank solutions: most neighbours
        S = {(r[0], round(r[1], 2)) for r in bank}
        best = max(S, key=lambda k: sum(((k[0] + da, round(k[1] + ds, 2)) in S) for da in (-0.5, -0.25, 0, 0.25, 0.5) for ds in (-0.1, 0, 0.1)))
        pr = [round(mc(c, (0, 0.6), best[0], best[1], p, n=1200), 2) for p in PLAYERS]
        print(f"phi {phi} z0 {z0}: best aim {best[0]} speed {best[1]} -> HIO casual/regular/expert {pr} (solutions {len(S)})")
