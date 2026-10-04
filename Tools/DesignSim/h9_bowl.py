import sys; sys.path.insert(0,'.')
from play import *
def cone(R, D, flat=0.25):
    def h(x, z):
        d = math.hypot(x, z)
        if d >= R: return 0.0
        if d <= flat: return -D
        return -D * (1 - (d - flat) / (R - flat))
    return h
def trial(R, D, prof, rnds=250):
    c = Course([([(R * 1.3 * math.cos(2 * math.pi * i / 28), R * 1.3 * math.sin(2 * math.pi * i / 28)) for i in range(28)], True)], prof, cup=(0.0, 0.0), bounds=(-R*1.4, R*1.4, -R*1.4, R*1.4))
    out = []
    for pl in PLAYERS:
        sa, sv = PLAYERS[pl]; rnd = random.Random(9); holed = 0
        for _ in range(rnds):
            start = (0.0, -R * 1.2)
            ang = rnd.gauss(0, sa); spd = rnd.uniform(1.4, 2.6)
            holed += c.shot(start[0], start[1], ang, spd, max_t=45).status == HOLED
        out.append(f"{pl} {holed/rnds*100:.0f}%")
    return ", ".join(out)
print("cone bowl (constant slope to the cup, 0.25 m flat at the centre); putt aimed at the cup from the bowl edge at 1.4-2.6 m/s:")
for R, D in ((1.5, 0.12), (1.8, 0.16), (2.0, 0.2), (2.4, 0.24), (2.4, 0.30)):
    slope = D / (R - 0.25)
    print(f"  R{R} D{D*100:.0f}cm slope {slope*100:.1f}%: {trial(R, D, cone(R, D))}")
