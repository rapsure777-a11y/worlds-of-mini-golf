import sys; sys.path.insert(0,'.')
from play import *
from explore import mc

def hole4(mouth=0.5, tunnel_len=2.4, funnel=(0.8, 0.05), cup=(0.0, 7.0)):
    z_t0 = 3.0; z_t1 = z_t0 + tunnel_len
    rects = [(-0.9, 0, 0.9, z_t0), (-mouth / 2, z_t0, mouth / 2, z_t1), (-1.6, z_t1, 4.2, z_t1 + 3.0),
             (0.9, 0, 4.2, 1.3), (3.0, 1.3, 4.2, z_t1)]
    walls = union_walls(rects)
    def h(x, z):
        d = math.hypot(x - cup[0], z - cup[1]) / funnel[0]
        return -funnel[1] * (0.5 + 0.5 * math.cos(math.pi * d)) if d < 1 else 0.0
    return Course(walls, h, cup=cup, bounds=(-2.2, 4.8, -0.5, z_t1 + 3.5))

def safe_policy(c, z_t1):
    cup = c.cup
    def pol(pos, k):
        x, z = pos
        if z > z_t1 - 0.2: return (cup[0], cup[1], 0.3 if k <= 3 else 0.12)          # in the bowl
        if x > 2.7: return (3.6, 6.9, 0.3)                                              # up the east lane
        return (3.6, 0.7, 0.1)                                                         # along the shore lane to the corner
    return pol

def short_policy(c, z_t0):
    cup = c.cup
    def pol(pos, k):
        x, z = pos
        if z > z_t0 + 2.2: return (cup[0], cup[1], 0.3 if k <= 3 else 0.12)
        if z < z_t0 - 0.3 and x < 1.5:
            # on the pad: aim for the tunnel if the line to the cup threads the mouth, else line up first
            xm = x * (1 - (z_t0 - z) / (cup[1] - z))
            if abs(xm) < 0.12: return (cup[0], cup[1], 0.3)
            return (0.0, 1.0, 0.0)
        return (3.6, 6.9, 0.3) if x > 2.7 else (cup[0], cup[1], 0.3)
    return pol

if __name__ == "__main__":
    for mouth in (0.45, 0.5, 0.6):
        c = hole4(mouth=mouth); z_t1 = 5.4
        print(f"== mouth {mouth} m, tunnel 2.4 m, bowl funnel r0.8 d5cm")
        show(" SAFE (coastal lane, 3 legs)", stats(c, (0, 0.6), safe_policy(c, z_t1), 3, n=200))
        show(" TUNNEL (straight through the rock)", stats(c, (0, 0.6), short_policy(c, 3.0), 3, n=200))
        # clean entry probability from the tee
        rnd = random.Random(3)
        for pl in PLAYERS:
            sa, sv = PLAYERS[pl]; ang, spd = plan_stroke(c, (0, 0.6), c.cup, 0.3); ok = 0; east = 0; n = 1500
            for _ in range(n):
                r = c.shot(0, 0.6, ang + rnd.gauss(0, sa), min(SPEED_CAP[pl], spd * (1 + rnd.gauss(0, sv))))
                ok += r.y > 5.3 or r.status == HOLED
                east += r.x > 0.9 and r.y < 1.4
            print(f"   {pl}: clean tunnel pass {ok / n:.2f}, misses rebounding into the shore lane {east / n:.2f}")
