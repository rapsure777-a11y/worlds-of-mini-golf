import sys; sys.path.insert(0,'.')
from play import *
from explore import mc

def smooth(t): t = min(1, max(0, t)); return t * t * (3 - 2 * t)

# ---------- H5: ramp speed windows (a 0.36 m rise over L metres onto a 2.6 m terrace with a back wall)
print("H5 terrace ramps: launch-speed window at the base of the ramp (ball starts 1.0 m before it, 1.2 m wide lane)")
for name, rise, L in (("steep centre ramp  0.36 m over 1.0 m", 0.36, 1.0), ("gentle side ramp   0.36 m over 2.4 m", 0.36, 2.4), ("low step           0.18 m over 1.2 m", 0.18, 1.2)):
    z0 = 1.0
    def h(x, z, rise=rise, L=L, z0=z0): return rise * smooth((z - z0) / L)
    top = z0 + L + 2.6
    c = Course([(rect(-0.6, 0, 0.6, top), True)], h)
    # speeds that put the ball ON the upper terrace (rests with z beyond the ramp) vs rolling back
    ok = []
    for i in range(0, 90):
        v = 1.0 + 0.05 * i
        r = c.shot(0, 0.2, 0, v)
        if r.y > z0 + L + 0.1: ok.append(v)
    lo = min(ok) if ok else None
    # overshoot: ball reaches the back wall and rebounds back down the ramp: find the largest speed that still rests on the terrace
    stay = [v for v in ok if c.shot(0, 0.2, 0, v).y > z0 + L + 0.1]
    hi = max(stay)
    print(f"  {name}: crests from {lo:.2f} m/s, still rests on the terrace up to {hi:.2f} m/s -> window {lo:.2f}-{hi:.2f} (centre {(lo+hi)/2:.2f}, +-{(hi-lo)/2/((lo+hi)/2)*100:.0f}%)")
    for pl in PLAYERS:
        sa, sv = PLAYERS[pl]; rnd = random.Random(5); n = 2000; good = 0; c0 = (lo + hi) / 2
        for _ in range(n):
            v = min(SPEED_CAP[pl], c0 * (1 + rnd.gauss(0, sv))); good += (lo <= v <= hi)
        print(f"     {pl}: lands on the terrace {good/n*100:.0f}%")

# ---------- H9: amphitheatre bowl capture
print("\nH9 amphitheatre: cup at the centre of a bowl; putts arrive aimed at the centre from 3 m with aim error, speed 1.0-2.8 m/s")
def bowl(R, D, cx=0.0, cz=0.0):
    def h(x, z):
        d = math.hypot(x - cx, z - cz) / R
        return -D * (0.5 + 0.5 * math.cos(math.pi * d)) if d < 1 else 0.0
    return h
for R, D in ((1.4, 0.07), (2.0, 0.12), (2.0, 0.16), (2.4, 0.16), (2.4, 0.22)):
    c = Course([(poly_circ := [(R * 1.25 * math.cos(2 * math.pi * i / 24), R * 1.25 * math.sin(2 * math.pi * i / 24)) for i in range(24)], True)], bowl(R, D), cup=(0.0, 0.0))
    # max slope (cosine profile): D*pi/(2R)
    row = []
    for pl in PLAYERS:
        sa, sv = PLAYERS[pl]; rnd = random.Random(9); n = 400; holed = 0
        for _ in range(n):
            ang = rnd.gauss(0, sa)
            spd = rnd.uniform(1.0, 2.8)
            start = (0.0, -2.9 * 0.0 - R * 1.1)  # edge of the bowl
            r = c.shot(0.0 + 0.0, -R * 1.1, ang, spd, max_t=40)
            holed += r.status == HOLED
        row.append(f"{pl} {holed / n * 100:.0f}%")
    print(f"  R {R} m depth {D*100:.0f} cm (max slope {D*math.pi/(2*R)*100:.0f}%): holed within 40 s -> " + ", ".join(row))
