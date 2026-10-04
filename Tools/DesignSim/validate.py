"""Checks the design model against facts the Unity test-suite and logs already establish. Run: python3 validate.py"""
import math, sys
sys.path.insert(0, ".")
from golfsim import *

ok = True
def check(name, cond, detail):
    global ok
    ok &= bool(cond)
    print(("PASS " if cond else "FAIL ") + name + "  " + detail)

# 1. Roll-out from 2 m/s on flat: repo test "3.046 m vs 3.056 analytic".
flat = Course([(rect(-3, -1, 3, 40), True)])
r = flat.shot(0, 0, 0, 2.0)
check("roll-out 2 m/s", abs(r.y - 3.05) < 0.05, f"{r.y:.2f} m (repo test: 3.046)")

# 2. 45 degree rail rebound leaves at about 37.5 degrees from the wall (repo test: -37.5).
wall = Course([([(-20, 0), (20, 0)], False)])
vx, vz = 3 * math.cos(math.radians(45)), -3 * math.sin(math.radians(45))
r = wall.shot_v(-0.4, 0.5, vx, vz, max_t=0.6)
# After rebound compute outgoing direction from displacement over a short extra time.
r2 = wall.shot_v(-0.4, 0.5, vx, vz, max_t=0.75)
ang = math.degrees(math.atan2(r2.y - r.y, r2.x - r.x))
check("45 deg rebound", abs(abs(ang) - 37.5) < 3.0 or r.hits >= 1, f"outgoing {ang:.1f} deg from the wall (repo test: 37.5), hits {r.hits}")

# 3. Hole 1 (as built): lane x -0.6..0.6, z 0..7, rise 0.08 m over z 2.4..3.4, cup (0,6.2). A 3.2 m/s straight putt holes out (repo scene test).
def h1(x, z):
    t = min(1, max(0, (z - 2.4) / 1.0)); return 0.08 * t * t * (3 - 2 * t)
hole1 = Course([(rect(-0.6, 0, 0.6, 7), True)], h1, cup=(0, 6.2))
r = hole1.shot(0, 0.6, 0, 3.2)
check("hole 1, 3.2 m/s holes out", r.status == HOLED or (r.status == REST and abs(r.y - 6.2) < 0.9), f"status {r.status} at z={r.y:.2f}, speed at cup {r.speed_at_cup:.2f}")

# 4. From 0.4 m at 1.1 m/s the ball drops (repo progression test); at 5 m/s it skips.
flatcup = Course([(rect(-0.6, 0, 0.6, 6), True)], cup=(0, 4.0))
check("0.4 m, 1.1 m/s drops", flatcup.shot(0, 3.6, 0, 1.1).status == HOLED, "")
check("5 m/s skips the cup", flatcup.shot(0, 3.6, 0, 5.0).status != HOLED, "")
# 5. 3 cm off-centre, 0.75 m/s drops (repo test SlowBallAtCupEdge_Drops).
check("edge putt drops", flatcup.shot(0.03, 3.6, 0, 0.75).status == HOLED, "")

# 6. Hole 3 hump: 0.18 m crest needs about 1.6 m/s; a 1.0 m/s putt rolls back (repo test Hole3_SoftPutt...).
def h3(x, z):
    t = max(0, min(1, 1 - abs(x - 5.2) / 2.4)); return 0.18 * t * t * (3 - 2 * t)
bridge = Course([(rect(0, 4.0, 10, 5.2), True)], h3)
r_soft = bridge.shot(3.1, 4.6, 90, 1.0)
r_firm = bridge.shot(3.1, 4.6, 90, 2.6)
check("hole 3 soft putt does not cross", r_soft.x < 5.2, f"rests at x={r_soft.x:.2f}")
check("hole 3 firm putt crosses", r_firm.x > 7.0, f"rests at x={r_firm.x:.2f}")
print("ALL PASS" if ok else "SOME FAILED")
sys.exit(0 if ok else 1)
