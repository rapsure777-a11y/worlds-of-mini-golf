import sys; sys.path.insert(0,'.')
from play import *
from explore import mc

def arc(cx, cz, r, a0, a1, n=14):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cz + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]

def hole4v2(mouth=0.45, cup=(0.0, 7.0), funnel=(0.8, 0.05), tunnel=(3.0, 5.4), cone=None):
    t0, t1 = tunnel; m = mouth / 2
    C = (0.9, 3.6); rin, rout = 2.0, 3.4
    outer = arc(C[0], C[1], rout, -90, 0)            # (0.9, 0.2) -> (4.3, 3.6)
    inner = arc(C[0], C[1], rin, 0, -90)             # (2.9, 3.6) -> (0.9, 1.6)
    W = []
    W.append([(-0.9, t0), (-0.9, 0), (0.9, 0), (0.9, 0.2)] + outer + [(4.3, 8.4), (-1.6, 8.4), (-1.6, t1), (-m, t1), (-m, t0), (-0.9, t0)])
    W.append([(m, t1), (2.9, t1), (2.9, 3.6)] + inner + [(0.9, 3.0), (m, 3.0), (m, t1)])
    walls = [(w, False) for w in W]
    def h(x, z):
        if cone:
            R, D = cone; d = math.hypot(x - cup[0], z - cup[1])
            return 0.0 if d >= R else (-D if d <= 0.25 else -D * (1 - (d - 0.25) / (R - 0.25)))
        d = math.hypot(x - cup[0], z - cup[1]) / funnel[0]
        return -funnel[1] * (0.5 + 0.5 * math.cos(math.pi * d)) if d < 1 else 0.0
    return Course(walls, h, cup=cup, bounds=(-2.2, 4.9, -0.5, 9.0))

def find_sweep_shot(c, tee=(0, 0.6)):
    sols = {}
    for a in [60 + 0.5 * i for i in range(0, 75)]:
        for s in [2.2 + 0.2 * i for i in range(0, 18)]:
            r = c.shot(tee[0], tee[1], a, s)
            if r.status == REST and r.x > 2.7 and r.y > 3.2:
                sols[(a, round(s, 1))] = 1
    best = max(sols, key=lambda k: sum(((k[0] + da, round(k[1] + ds, 1)) in sols) for da in (-2, -1, -0.5, 0, 0.5, 1, 2) for ds in (-0.4, -0.2, 0, 0.2, 0.4)))
    return best, len(sols)

def run(mouth, cone=None):
    c = hole4v2(mouth=mouth, cone=cone); tee = (0, 0.6)
    (ba, bs), cnt = find_sweep_shot(c)
    print(f"== H4 v2: coastal sweep (curved lane) vs tunnel mouth {mouth} m, cone={cone};  sweep shot aim {ba} deg, {bs} m/s ({cnt} working combos)")
    def safe(pos, k):
        x, z = pos
        if k == 1 and z < 1.5 and x < 0.9: return ("trick", ba, bs)
        if z > 5.2: return (0.0, 7.0, 0.3 if k <= 3 else 0.12)
        if x > 2.7 and z > 3.0: return (3.6, 6.9, 0.3)
        if x < 0.9: return ("trick", ba, bs)
        return (3.6, 3.6, 0.3)
    def short(pos, k):
        x, z = pos
        if z > 5.2: return (0.0, 7.0, 0.3 if k <= 3 else 0.12)
        if z < 2.8 and x < 0.8:
            xm = x * (1 - (3.0 - z) / (7.0 - z))
            if abs(xm) < 0.1: return (0.0, 7.0, 0.3)
            return (0.0, 1.0, 0.0)
        return (3.6, 6.9, 0.3) if x > 2.7 else (0.0, 7.0, 0.3)
    show(" SAFE sweep", stats(c, tee, safe, 3, n=250))
    show(" TUNNEL", stats(c, tee, short, 3, n=250))

if __name__ == '__main__':
    run(0.45)
    run(0.5)
