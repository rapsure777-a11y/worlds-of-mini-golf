"""Proving-ground roulette bowl: cone bowl + flat rim shelf + outer wall; tee on the shelf, ball struck roughly tangentially. Indicative only."""
import sys, math, random; sys.path.insert(0,'.')
from golfsim import *
def make(R=1.8, shelf=0.45, slope=0.10, apron=0.30, n=72, shelf_slope=0.06):
    """Cone of `slope` from the apron out to R-shelf, then a gentler `shelf_slope` rim shelf out to the wall at R (so a ball can rest there to be re-struck)."""
    Ro=R; Rin=R-shelf
    wall=[(Ro*math.cos(2*math.pi*i/n), Ro*math.sin(2*math.pi*i/n)) for i in range(n)]
    def h(x,z):
        r=math.hypot(x,z)
        if r<=apron: return 0.0
        if r<=Rin: return slope*(r-apron)
        return slope*(Rin-apron)+shelf_slope*(r-Rin)
    return Course([(wall,True)], h, cup=(0,0), bounds=(-Ro-0.3,Ro+0.3,-Ro-0.3,Ro+0.3), step=0.04), Ro
def run(R,shelf,slope,apron,speeds,N=300,seed=5,ss=0.06):
    c,Ro=make(R,shelf,slope,apron,shelf_slope=ss)
    rnd=random.Random(seed)
    print(f"R={R} shelf={shelf} cone {slope*100:.0f}% shelf {ss*100:.0f}% apron={apron}")
    for v in speeds:
        row=[]
        for pl in ("casual","regular"):
            sa,sv=PLAYERS[pl]; holed=rest_apron=rest_other=0; tmax=0
            for _ in range(N):
                x,z=0.0,-(R-shelf*0.5)        # tee on the shelf at the bottom
                a=90+rnd.gauss(0,sa)+4         # hit along +x, a touch inward
                r=c.shot(x,z,a,v*max(0.2,1+rnd.gauss(0,sv)))
                d=math.hypot(r.x,r.y)
                if r.status==HOLED: holed+=1
                elif r.status==REST: 
                    if d<=apron+0.03: rest_apron+=1
                    else: rest_other+=1
            row.append(f"{pl}: holed {holed/N*100:3.0f}% apron {rest_apron/N*100:3.0f}% elsewhere {rest_other/N*100:3.0f}%")
        print(f"  v {v:3.1f}  "+" | ".join(row))
if __name__=="__main__":
    sp=(1.5,2.0,2.5,3.0,3.5,4.5)
    sp=(1.0,1.5,2.0,2.5,3.0,4.0)
    run(2.0,0.5,0.10,0.30,sp,ss=0.065)
    run(2.0,0.5,0.10,0.30,sp,ss=0.068)
    run(2.0,0.5,0.09,0.30,sp,ss=0.065)
