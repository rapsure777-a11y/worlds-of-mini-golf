import sys; sys.path.insert(0,'.')
from play import *
def sm(t): t = min(1, max(0, t)); return t * t * (3 - 2 * t)

def cone(cx, cz, R, D, flat=0.25):
    def h(x, z):
        d = math.hypot(x - cx, z - cz)
        if d >= R: return 0.0
        return -D if d <= flat else -D * (1 - (d - flat) / (R - flat))
    return h

def hole3v2(guides=True, mouth=1.2, funnelR=1.8, funnelD=0.16, crest=0.18):
    z_m0, z_m1 = 3.8 - mouth / 2, 3.8 + mouth / 2
    rects = [(-0.6, 0, 0.6, 2.2), (-1.4, 2.2, 3.0, 5.6), (3.0, z_m0, 6.6, z_m1), (6.6, 2.6, 9.8, 5.4)]
    walls = union_walls(rects)
    if guides:
        walls.append(([(1.4, 5.6), (3.0, z_m1)], False))   # north guide (angled stone wall)
        walls.append(([(1.4, 2.2), (3.0, z_m0)], False))   # south guide
    cup = (8.7, 4.0)
    fn = cone(cup[0], cup[1], funnelR, funnelD)
    def h(x, z):
        t = max(0, min(1, 1 - abs(x - 4.8) / 2.4))
        return crest * sm(t) + fn(x, z)
    return Course(walls, h, cup=cup, bounds=(-2.0, 10.5, -0.5, 6.5)), cup

def policy(cup):
    def pol(pos, k):
        x, z = pos
        if x < 0.7 and z < 2.2: return (0.0, 4.2, 0.2)             # up the tee lane into the clearing
        if x < 3.0: return (7.4, 3.8, 0.3)                          # at the bridge mouth, long enough to crest and land on the pad
        return (cup[0], cup[1], 0.25 if k <= 3 else 0.12)
    return pol

def cross_rate(c, player, n=800, seed=4):
    """Share of second shots from random spots in the clearing that end past the bridge (x>6.6) or holed."""
    rnd = random.Random(seed); sa, sv = PLAYERS[player]; ok = 0
    for _ in range(n):
        pos = (rnd.uniform(-1.1, 1.8), rnd.uniform(2.6, 5.2))
        ang, spd = plan_stroke(c, pos, (7.4, 3.8), 0.3)
        r = c.shot(pos[0], pos[1], ang + rnd.gauss(0, sa), min(SPEED_CAP[player], spd * (1 + rnd.gauss(0, sv))))
        ok += (r.status == HOLED) or r.x > 6.6
    return ok / n

if __name__ == "__main__":
    for guides in (False, True):
        c, cup = hole3v2(guides=guides)
        print(f"== H3 v2: clearing + {'angled guide walls' if guides else 'plain square mouth'}; bridge 1.2 m, crest 0.18 m; cone funnel R1.8 D16cm at the cup")
        print("  crossing success from random spots in the clearing:", {p: round(cross_rate(c, p), 2) for p in PLAYERS})
        show("  play", stats(c, (0, 0.6), policy(cup), 3, n=250))
