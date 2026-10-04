"""Hole 4 'Hollow Drop' as built (HoleDefinition.cs Hole04), plus candidate tweaks."""
import sys; sys.path.insert(0,'.')
from play import *
def sm(t): return t*t*(3-2*t)
def ramp(z,z0,z1,h0,h1): return h0+(h1-h0)*sm(min(1,max(0,(z-z0)/(z1-z0))))
RECTS=[(-0.6,0,0.6,4.0),(-0.6,4.0,4.6,5.2),(3.4,5.2,4.6,9.2),(-1.2,9.2,4.6,10.4),(-1.2,10.4,0.0,14.0)]
def h4(x,z): return ramp(z,5.6,8.2,0,-0.24)+ramp(z,11.4,12.4,0,0.10)
def make(h=h4,rects=RECTS,cup=(-0.6,13.0)):
    return Course(union_walls(rects),h,cup=cup,bounds=(-2,5.5,-0.5,14.5))
def pol(pos,k):
    x,z=pos
    if z<4.0 and x<0.7: return (0.0,4.6,0.3)          # up tee lane to the turn
    if z<5.2 and x<3.3: return (4.0,4.6,0.0)          # along the turn
    if z<9.0 and x>3.3: return (4.0,9.8,0.2)          # down the drop into the basin
    if z<10.4 and x>-0.1: return (-0.6,9.8,0.0)       # back west across the basin
    return (-0.6,13.0,0.12)                           # up the climb to the cup
if __name__=="__main__":
    c=make()
    show("Hole 4 Hollow Drop as built (par 4)", stats(c,(0,0.6),pol,4,n=300))
    # drop speed: ball rolled gently down the descent lane from rest at top of ramp
    for v in (0.3,0.8,1.5,2.5):
        r=c.shot(4.0,5.4,0,v,use_cup=False); print("descent start v",v,"->",r.status,round(r.x,2),round(r.y,2))
    # climb: speeds to reach the cup from basin end of final lane
    for v in (1.2,1.6,2.0,2.4,2.8,3.4):
        r=c.shot(-0.6,10.8,0,v); print("climb v",v,"->",r.status,round(r.x,2),round(r.y,2))
