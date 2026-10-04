"""Hole 4 'Hollow Drop': as built vs. small tweaks (kicker elbow, gentler climb, descent-lane bump stop). Indicative only."""
import sys; sys.path.insert(0,'.')
from asbuilt_h4 import *
def sm(t): return t*t*(3-2*t)
def mk(kicker=False, climb=(11.4,12.4), drop=(5.6,8.2), dropamt=-0.24, climbamt=0.10, extra=()):
    f=lambda x,z: ramp(z,drop[0],drop[1],0,dropamt)+ramp(z,climb[0],climb[1],0,climbamt)
    walls=union_walls(RECTS)+list(extra)
    if kicker: walls=walls+[([(-0.6,4.0),(0.6,5.2)],False)]
    return Course(walls,f,cup=(-0.6,13.0),bounds=(-2,5.5,-0.5,14.5))
def best_speed(c,pos,ang,target,lo=0.8,hi=5.0):
    b=None
    for i in range(60):
        s=lo+(hi-lo)*i/59; r=c.shot(pos[0],pos[1],ang,s,use_cup=False)
        d=dist((r.x,r.y),target)
        if b is None or d<b[0]: b=(d,s)
    return b[1]
def pol_k(c):
    s1=best_speed(c,(0,0.6),0.0,(3.9,4.6))
    def p(pos,k):
        x,z=pos
        if z<1.0 and x<0.7 and k==1: return ("trick",0.0,s1)
        if z<4.0 and x<0.7: return (0.0,4.6,0.3)
        if z<5.2 and x<3.3: return (4.0,4.6,0.0)
        if z<9.0 and x>3.3: return (4.0,9.8,0.2)
        if z<10.4 and x>-0.1: return (-0.6,9.8,0.0)
        return (-0.6,13.0,0.12)
    return p
if __name__=="__main__":
    variants={"A as built":mk(),"B kicker elbow":mk(kicker=True),"C kicker + gentler climb(10.8-12.4)":mk(kicker=True,climb=(10.8,12.4)),
              "D kicker + gentler climb + softer drop(5.6-8.8)":mk(kicker=True,climb=(10.8,12.4),drop=(5.6,8.8),dropamt=-0.20)}
    for name,c in variants.items():
        show(name+(" (par 4)"), stats(c,(0,0.6),pol_k(c),4,n=120))
