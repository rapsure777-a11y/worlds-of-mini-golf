"""Paper checks for Holes 7 and 8 (indicative: the 2D model has no flight or cliffs).
Hole 8: a ball rolling in through the gate lane (+x along z 5.4..6.2) onto the bowl's rim shelf: does it orbit and settle, hole out sometimes, never leave?
Hole 7: the vent exit roll-out on Tier 3, and the tee-putt bank off the 45 degree corner."""
import sys, math, random; sys.path.insert(0,'.')
from golfsim import *
R, SHELF, CONE, SS, APRON = 2.0, 0.5, 0.10, 0.065, 0.30
BX, BZ = 0.8, 5.033
def bowl_h(x, z):
    r = math.hypot(x - BX, z - BZ)
    if r <= APRON: return 0.0
    rin = R - SHELF
    if r <= rin: return CONE * (r - APRON)
    return CONE * (rin - APRON) + SS * (r - rin)
n = 96
ring = [(BX + R * math.cos(2 * math.pi * i / n), BZ + R * math.sin(2 * math.pi * i / n)) for i in range(n)]
bowl = Course([(ring, True)], bowl_h, cup=(BX, BZ), bounds=(BX - R - .3, BX + R + .3, BZ - R - .3, BZ + R + .3), step=0.04)
def arcx(z): return BX - math.sqrt(max(1e-4, (R + 0.02) ** 2 - (z - BZ) ** 2))
rnd = random.Random(11)
print("Hole 8 - rolling in through the gate (lane z 5.4..6.2, along +x): outcome by entry speed")
for v in (1.2, 1.6, 2.0, 2.5, 3.0, 3.5, 4.0):
    N = 400; holed = apron = other = out = 0; rad = []
    for _ in range(N):
        z = rnd.uniform(5.45, 6.15); x = arcx(z) + 0.06
        ang = 90 + rnd.gauss(0, 2.5)                      # a little aim scatter along the lane
        r = bowl.shot(x, z, ang, v * max(0.2, 1 + rnd.gauss(0, 0.06)))
        d = math.hypot(r.x - BX, r.y - BZ)
        if r.status == HOLED: holed += 1
        elif r.status == REST:
            rad.append(d)
            if d <= APRON + 0.03: apron += 1
            else: other += 1
        else: out += 1
    mean = sum(rad) / len(rad) if rad else float('nan')
    print(f"  {v:3.1f} m/s: holed {holed/N*100:3.0f}%  rest on apron {apron/N*100:3.0f}%  rest elsewhere {other/N*100:3.0f}% (mean r {mean:.2f})  other {out/N*100:.0f}%")
print("Hole 7 - vent exit on Tier 3: released at (2.4,14.5) going +z; roll-out (flat), cup at (0.4,18.6)")
t3 = Course(union_walls([(-0.4, 14.0, 3.0, 19.6)]), lambda x, z: 0.0, cup=(0.4, 18.6), bounds=(-1, 3.6, 13.5, 20.2))
for v in (1.6, 2.0, 2.4):
    r = t3.shot(2.4, 14.5, 0, v)
    print(f"  exit {v} m/s: rest ({r.x:.2f},{r.y:.2f}) status {r.status}; distance to cup {math.hypot(r.x-0.4, r.y-18.6):.2f} m")

# ---- Hole 7 Tier 1/2 on paper: tee putts into the bank, the causeway climb, the mouth
def h7(x, z):
    if z >= 7.2 - 0.05:
        d = math.hypot(x - 2.4, z - 9.3)
        return 0.24 - 0.03 * (1 - min(1, d / 0.30)) if d < 0.30 else 0.24
    if x >= 1.75 and z >= 3.95: return 0.24 * min(1, max(0, (z - 4.0) / 3.2))
    return 0.0
rects = [(-0.8, 0, 0.8, 2.0), (-0.8, 2.0, 3.0, 4.0), (1.8, 4.0, 3.0, 7.2), (0.6, 7.2, 3.0, 10.0)]
walls = union_walls(rects)
def keep(seg):
    (a, b) = seg[0]
    vx = abs(a[0] - 1.8) < 1e-6 and abs(b[0] - 1.8) < 1e-6 and 4.0 <= min(a[1], b[1]) and max(a[1], b[1]) <= 7.2
    hz = abs(a[1] - 7.2) < 1e-6 and abs(b[1] - 7.2) < 1e-6 and 0.6 <= min(a[0], b[0]) and max(a[0], b[0]) <= 1.8
    return not (vx or hz)
walls = [w for w in walls if keep(w)]
bank = [([(-0.8, 2.4), (0.8, 4.0)], False)]
lake = [rect(-1.6, 4.05, 1.8, 7.3)]
c7 = Course(walls + bank, h7, hazards=lake, bounds=(-1.8, 3.4, -0.5, 10.5), step=0.04)
print("Hole 7 - tee putt (0,0.6) north into the corner bank, by speed: where does the ball rest?")
for v in (1.6, 2.2, 2.8, 3.4, 4.2):
    r = c7.shot(0, 0.6, 0, v, use_cup=False)
    print(f"  {v:3.1f} m/s: rest ({r.x:.2f},{r.y:.2f}) status {r.status} hits {r.hits}")
print("Hole 7 - from the causeway foot (2.4,4.3) putting north (aim scatter 2 deg): rest on ramp / pad / mouth / lava")
for v in (2.6, 3.2, 3.8, 4.2, 4.6, 5.2):
    res = {"ramp": 0, "pad": 0, "mouth": 0, "lava": 0}; N = 200
    for _ in range(N):
        r = c7.shot(2.4 + rnd.uniform(-0.3, 0.3), 4.3, rnd.gauss(0, 2.0), v * max(0.2, 1 + rnd.gauss(0, 0.06)), use_cup=False)
        if r.status != REST: res["lava"] += 1
        elif r.y >= 7.2:
            res["mouth" if math.hypot(r.x - 2.4, r.y - 9.3) < 0.25 else "pad"] += 1
        else: res["ramp"] += 1
    print(f"  {v:3.1f} m/s: " + "  ".join(f"{k} {c/N*100:3.0f}%" for k, c in res.items()))

# ---- Hole 8 route on paper: tee putt west, the corner bank, the 3.4 m climb, the north bank, the gate lane, then into the bowl (ring left open at the gate arc)
GL = 0.1096
def h8(x, z):
    if x >= -2.45 and z >= 5.35 and x < 0.6: return GL
    if x >= -0.65: return 0.0
    return GL * min(1, max(0, (z - 1.8) / 3.4))
rects8 = [(-0.6, 0, 0.6, 2.6), (-3.4, 0.5, -0.6, 1.5), (-3.4, 1.5, -2.4, 6.2), (-2.4, 5.4, -1.3, 6.2)]
w8 = [w for w in union_walls(rects8) if not (abs(w[0][0][0] + 1.3) < 1e-6 and abs(w[0][1][0] + 1.3) < 1e-6)] + [([(-3.4, 1.5), (-2.4, 0.5)], False), ([(-3.4, 5.2), (-2.4, 6.2)], False)]
gate_a0, gate_a1 = 144.7 - 2.5, 169.5 + 2.5
def in_gate(deg): return gate_a0 <= deg % 360 <= gate_a1
ring8 = []
cur = []
for i in range(n + 1):
    a = 360.0 * i / n
    if in_gate(a):
        if cur: ring8.append((cur, False)); cur = []
    else: cur.append((BX + R * math.cos(math.radians(a)), BZ + R * math.sin(math.radians(a))))
if cur: ring8.append((cur, False))
def h8full(x, z):
    r = math.hypot(x - BX, z - BZ)
    if r < R + 0.05: return bowl_h(x, z) + 0.1525 * 0 - 0.0629 + 0.0  # bowl, cup rim at y=-0.063 relative to the lane base
    return h8(x, z)
c8 = Course(w8 + ring8, h8full, cup=(BX, BZ), bounds=(-3.6, 3.2, -0.3, 7.4), step=0.04)
print("Hole 8 - tee putt west (0,0.9) -> corner bank -> climb: by speed, where does the first stroke end?")
for v in (2.4, 3.2, 4.0, 4.8):
    r = c8.shot(0, 0.9, -90, v, use_cup=False)
    print(f"  {v:3.1f} m/s: rest ({r.x:.2f},{r.y:.2f}) status {r.status} hits {r.hits}")
print("Hole 8 - from the column foot (-2.9,1.8) putting north: ends where? (climb 0.11 m, north bank, gate lane, bowl)")
for v in (3.0, 3.6, 4.2, 5.0, 6.0):
    rs = []
    for _ in range(100):
        r = c8.shot(-2.9 + rnd.uniform(-0.3, 0.3), 1.8, rnd.gauss(0, 2.0), v * max(0.2, 1 + rnd.gauss(0, 0.06)))
        rs.append(r)
    holed = sum(1 for r in rs if r.status == HOLED)
    inbowl = sum(1 for r in rs if r.status == REST and math.hypot(r.x - BX, r.y - BZ) < R)
    apron = sum(1 for r in rs if r.status == REST and math.hypot(r.x - BX, r.y - BZ) < 0.33)
    lane = sum(1 for r in rs if r.status == REST and math.hypot(r.x - BX, r.y - BZ) >= R)
    print(f"  {v:3.1f} m/s: holed {holed}%  in bowl {inbowl}% (on apron {apron}%)  still on the route {lane}%")
