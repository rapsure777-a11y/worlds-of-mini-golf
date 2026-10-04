import sys; sys.path.insert(0,'.')
from h2_explore import *
from explore import mc

def best_bank(c, tee):
    sols = {}
    for a in [i * 0.5 for i in range(10, 100)]:
        for s in [2.4 + 0.2 * i for i in range(0, 20)]:
            r = c.shot(tee[0], tee[1], a, s)
            if r.status == HOLED or (r.status == REST and dist((r.x, r.y), c.cup) < 1.3 and r.hits >= 1 and r.y > 5.0):
                sols[(a, round(s, 1))] = 1
    if not sols: return None
    return max(sols, key=lambda k: sum(((k[0] + da, round(k[1] + ds, 1)) in sols) for da in (-1, -0.5, 0, 0.5, 1) for ds in (-0.4, -0.2, 0, 0.2, 0.4))), len(sols)

def evaluate(label, W, gap_w, fr, fd, cup, n=250, L=2.4):
    import h2_explore
    c = hole2(gap_w=gap_w, funnel_r=fr, funnel_d=fd, cup=cup, L=L)
    # re-bound arena width by rebuilding with custom outline
    res = {}
    tee = (0, 0.6)
    safe = stats(c, tee, safe_policy(cup), 3, n=n)
    risk = stats(c, tee, risk_policy(cup), 3, n=n)
    bb = best_bank(c, tee)
    if bb:
        (ba, bs), cnt = bb
        def bank_policy(pos, k):
            if k == 1: return ("trick", ba, bs)
            return (cup[0], cup[1], 0.3 if k <= 2 else 0.12)
        bank = stats(c, tee, bank_policy, 3, n=n)
    else: bank = None
    print(f"--- {label}")
    for pl in PLAYERS:
        line = f"  {pl:8s} safe {safe[pl]['mean']:.2f}  bank {bank[pl]['mean'] if bank else float('nan'):.2f}  shortcut {risk[pl]['mean']:.2f}   par-or-better  safe {safe[pl]['par_or_better']*100:.0f}% bank {(bank[pl]['par_or_better']*100 if bank else 0):.0f}% shortcut {risk[pl]['par_or_better']*100:.0f}%   HIO shortcut {risk[pl]['hio']*100:.0f}%"
        print(line)
    print("  gap pass", {p: round(gap_pass(c, -0.7, player=p), 2) for p in PLAYERS})

