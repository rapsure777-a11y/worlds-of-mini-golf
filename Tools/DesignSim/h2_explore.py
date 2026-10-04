import sys; sys.path.insert(0,'.')
from play import *

def poly_circle(cx, cz, r, n=10):
    return [(cx + r * math.cos(2 * math.pi * i / n), cz + r * math.sin(2 * math.pi * i / n)) for i in range(n)]

def hole2(gap_w=0.6, gap_x=-0.7, cup=(-1.15, 6.7), log_tilt=0.15, funnel_r=0.0, funnel_d=0.0, L=2.4):
    """Open 4.8 x 8.2 green. A fallen palm (thick log, slightly tilted) crosses it at z~4.2 from the left wall to a root-ball at x~0.9.
    Root gap of width gap_w centred at gap_x is the shortcut; the 1.05 m opening right of the root-ball is the safe route."""
    zc = 4.2
    def zl(x): return zc + log_tilt * (x - gap_x)
    th = 0.17  # half thickness
    gl, gr = gap_x - gap_w / 2, gap_x + gap_w / 2
    logA = [(-L, zl(-L) - th), (gl, zl(gl) - th), (gl, zl(gl) + th), (-L, zl(-L) + th)]
    rx = 0.95
    logB = [(gr, zl(gr) - th), (rx, zl(rx) - th)] + poly_circle(rx + 0.15, zl(rx), 0.5, 10)[3:8] + [(rx, zl(rx) + th), (gr, zl(gr) + th)]
    outline = rect(-L, 0, L, 8.2)
    def h(x, z):
        if funnel_r:
            d = math.hypot(x - cup[0], z - cup[1]) / funnel_r
            return -funnel_d * (0.5 + 0.5 * math.cos(math.pi * d)) if d < 1 else 0.0
        return 0.0
    return Course([(outline, True), (logA, True), (logB, True)], h, cup=cup)

def safe_policy(cup, via=(1.9, 5.3)):
    def pol(pos, k):
        if pos[1] < 4.0 and pos[0] < 1.2: return (via[0], via[1], 0.25)    # through the wide gap on the right
        return (cup[0], cup[1], 0.3 if k <= 2 else 0.12)
    return pol

def risk_policy(cup):
    def pol(pos, k):
        if pos[1] < 3.9: return (cup[0], cup[1], 0.3)      # straight for the root gap and the cup behind it
        return (cup[0], cup[1], 0.3 if k <= 2 else 0.12)
    return pol

def gap_pass(course, gap_x, zc=4.2, n=2000, player="regular", tee=(0, 0.6)):
    """Fraction of shortcut attempts (aim at the gap centre, speed to reach the cup) that make it past the log without touching it."""
    rnd = random.Random(2); sa, sv = PLAYERS[player]; ok = 0; hole = course.cup
    ang0, spd = plan_stroke(course, tee, hole, 0.3)
    for _ in range(n):
        r = course.shot(tee[0], tee[1], ang0 + rnd.gauss(0, sa), spd * (1 + rnd.gauss(0, sv)))
        ok += (r.status == HOLED) or (r.y > zc + 0.6)
    return ok / n

if __name__ == "__main__":
    for funnel in ((0, 0), (0.7, 0.04)):
        c = hole2(funnel_r=funnel[0], funnel_d=funnel[1])
        print(f"== H2 'Split the Palms' (log with root gap), cup funnel r={funnel[0]} depth={funnel[1]*100:.0f} cm")
        for gw in (0.5, 0.6, 0.7):
            c = hole2(gap_w=gw, funnel_r=funnel[0], funnel_d=funnel[1])
            print(f" root gap {gw} m: shortcut clean-pass:", {p: round(gap_pass(c, -0.7, player=p), 2) for p in PLAYERS})
        c = hole2(gap_w=0.6, funnel_r=funnel[0], funnel_d=funnel[1])
        show(" SAFE route (right gap, then across)", stats(c, (0, 0.6), safe_policy(c.cup), 3, n=300))
        show(" SHORTCUT policy (always try the root gap)", stats(c, (0, 0.6), risk_policy(c.cup), 3, n=300))
