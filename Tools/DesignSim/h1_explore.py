import sys; sys.path.insert(0,'.')
from play import *
from explore import hole1, funnel

def run(name, c, cup, par=2, n=300):
    pol = lambda pos,k: (cup[0],cup[1],0.35) if k==1 else (cup[0],cup[1],0.12)
    show(name, stats(c,(0,0.6),pol,par,n=n,players=("casual","regular","expert")))

run("A: 1.8 m lane, no funnel", hole1(1.8), (0,6.7))
for r,d in ((0.8,0.05),(0.9,0.08),(1.0,0.10)):
    run(f"B: 1.8 m lane, funnel r={r} depth={d*100:.0f} cm", hole1(1.8,r,d), (0,6.7))
run("C: 1.8 m lane, funnel r=0.9 d=8cm, cup offset x=+0.35 and 3% cross-slope", hole1(1.8,0.9,0.08,cup_x=0.35,cross=0.03), (0.35,6.7))
