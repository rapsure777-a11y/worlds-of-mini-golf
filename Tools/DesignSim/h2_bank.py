import sys; sys.path.insert(0,'.')
from h2_explore import *
from explore import best_shot, mc

c = hole2(gap_w=0.5, funnel_r=0.7, funnel_d=0.04)
tee = (0, 0.6)
# Search aim/speed for a bank route: first shot rebounds off the east wall and ends near or in the cup.
angs = [i * 0.25 for i in range(20, 200)]      # +5 .. +50 degrees (to the right)
spds = [2.4 + 0.1 * i for i in range(0, 36)]
sols = {}
for a in angs:
    for s in spds:
        r = c.shot(tee[0], tee[1], a, s)
        if r.status == HOLED: sols[(a, round(s, 1))] = 'H'
        elif r.status == REST and dist((r.x, r.y), c.cup) < 1.3 and r.hits >= 1 and r.y > 5.0: sols[(a, round(s, 1))] = 'N'  # near the cup after a bank
H = [k for k, v in sols.items() if v == 'H']
N = [k for k, v in sols.items() if v == 'N']
print("bank solutions: holed", len(H), " ending near the cup (<1.3 m)", len(N))
if H:
    best = max(H, key=lambda k: sum(((k[0] + da, round(k[1] + ds, 1)) in sols) for da in (-0.5, -0.25, 0, 0.25, 0.5) for ds in (-0.2, -0.1, 0, 0.1, 0.2)))
    print("best bank hole-in-one: aim", best[0], "speed", best[1], " hole-in-one rate casual/regular/expert",
          [round(mc(c, tee, best[0], best[1], p, n=1500), 3) for p in PLAYERS])
# Near-cup bank (aim for the best 'near' cluster) then putt.
pool = N or H
best = max(pool, key=lambda k: sum(((k[0] + da, round(k[1] + ds, 1)) in sols) for da in (-0.75, -0.5, -0.25, 0, 0.25, 0.5, 0.75) for ds in (-0.3, -0.2, -0.1, 0, 0.1, 0.2, 0.3)))
print("best bank-then-putt: aim", best[0], "speed", best[1])
def bank_policy(pos, k):
    if k == 1: return ("trick", best[0], best[1])
    return (c.cup[0], c.cup[1], 0.3 if k <= 2 else 0.12)
show("BANK route (first shot off the east wall, then putt)", stats(c, tee, bank_policy, 3, n=300))
