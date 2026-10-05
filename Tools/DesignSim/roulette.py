"""Concept check: 'roulette bowl' (Hole 8). A circular bowl with a banked rim wall; the ball is hit along the wall and spirals in to a centre cup."""
import sys, math, random; sys.path.insert(0,'.')
from golfsim import *
def bowl(R=1.5, slope=0.09, cup_r=0.0, n=64, flat=0.25):
    wall=[(R*math.cos(2*math.pi*i/n), R*math.sin(2*math.pi*i/n)) for i in range(n)]
    h=lambda x,z: slope*max(0.0, math.hypot(x,z)-flat)
    return Course([(wall,True)], h, cup=(0,0), bounds=(-R-0.3,R+0.3,-R-0.3,R+0.3), step=0.04)
def trial(c, R, v, ang_off, sa, sv, rnd, start_r=None):
    x,z=0.0,-(start_r or R-0.12)       # start near the wall at the bottom of the bowl, hit along +x (tangent)
    a=90+ang_off+rnd.gauss(0,sa)       # 90 deg = +x
    r=c.shot(x,z,a,v*max(0.2,1+rnd.gauss(0,sv)))
    return r.status, math.hypot(r.x,r.y)
if __name__=="__main__":
    rnd=random.Random(3)
    for R,slope in ((1.5,0.09),(1.5,0.12),(2.0,0.10)):
        c=bowl(R,slope)
        print(f"bowl R={R} m, cone slope {slope*100:.0f}%")
        for v in (1.0,1.6,2.2,2.8,3.6,4.5):
            for pl in ("casual","regular"):
                sa,sv=PLAYERS[pl]; N=300; holed=0; near=0
                for _ in range(N):
                    st,d=trial(c,R,v,0,sa,sv,rnd); holed+=st==HOLED; near+=(st==REST and d<0.35)
                print(f"  v {v:3.1f} {pl:7s} holed {holed/N*100:3.0f}%  rest within 35 cm {near/N*100:3.0f}%")
