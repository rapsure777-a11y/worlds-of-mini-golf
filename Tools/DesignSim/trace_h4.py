import sys; sys.path.insert(0,'.')
from asbuilt_h4 import *
import random
c=make(); pos=(0,0.6)
for k in range(1,12):
    plan=pol(pos,k); ang,spd=plan_stroke(c,pos,plan[:2],plan[2])
    r=c.shot(pos[0],pos[1],ang,min(spd,6.5))
    print(k,"from",tuple(round(v,2) for v in pos),"aim",round(ang,1),"spd",round(spd,2),"->",r.status,round(r.x,2),round(r.y,2),"hits",r.hits)
    if r.status==1: break
    pos=(r.x,r.y)
