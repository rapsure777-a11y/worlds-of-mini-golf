"""Paper checks for Hole 9, Summit Sanctuary (indicative: 2D model, no flight, no cliffs)."""
import sys, math, random; sys.path.insert(0,'.')
from golfsim import *
T1,P0,HM,SL,AH=0.20,0.38,0.50,0.62,0.72
def h(x,z):
    if z>=13.9-.05:
        if x>=5.0-.05: return AH
        if x>=0.4-.05 and z>=17.95: return SL+(AH-SL)*min(1,max(0,(x-1.8)/3.2))
        return HM+(SL-HM)*min(1,max(0,(z-13.9)/3.7))
    if z>=12.5-.05: return HM
    if x>=3.35 and z>=6.55: return T1+(HM-T1)*min(1,max(0,(z-6.6)/5.9))
    if z>=8.1-.05: return P0+(HM-P0)*min(1,max(0,(z-9.1)/3.4))
    if z>=6.55: return T1+0.26*min(1,max(0,(z-6.6)/1.0))
    if z>=4.95: return T1
    return T1*min(1,max(0,(z-1.2)/3.8))
rects=[(-1,0,1,5),(-1,5,4.4,6.6),(-0.8,6.6,0.2,7.6),(-1,8.1,2.4,12.5),(3.4,6.6,4.4,12.5),(-1,12.5,4.4,13.9),(-1,13.9,0.4,19.0),(0.4,18,5.0,19),(5.0,17.5,7.0,19.5)]
walls=union_walls(rects)
def drop(w):
    (a,b)=w[0]; same=lambda i,v:abs(a[i]-v)<1e-6 and abs(b[i]-v)<1e-6
    if same(0,2.4) and 8.1<=min(a[1],b[1]) and max(a[1],b[1])<=12.5: return True
    if same(0,3.4) and 6.6<=min(a[1],b[1]) and max(a[1],b[1])<=12.5: return True
    if same(1,18.0) and 1.8<=min(a[0],b[0]) and max(a[0],b[0])<=5.0: return True
    if same(1,19.0) and 1.8<=min(a[0],b[0]) and max(a[0],b[0])<=5.0: return True
    return False
walls=[w for w in walls if not drop(w)]
banks=[([(3.0,5.0),(4.4,6.4)],False),([(4.4,12.9),(3.4,13.9)],False),([(-1,15.8),(-0.65,15.8)],False),([(0.4,15.8),(0.05,15.8)],False),([(-1,17.6),(0.4,19.0)],False)]
haz=[rect(2.4,6.6,3.4,12.5),rect(1.8,17.4,5.0,17.99),rect(1.8,19.01,5.0,19.6)]
c=Course(walls+banks,h,cup=(6.2,18.5),hazards=haz,bounds=(-1.5,7.5,-0.5,20),step=0.04)
rnd=random.Random(5)
def table(title,x,z,ang,speeds,cls,n=150,sx=0.0,sa=2.0):
    print(title)
    for v in speeds:
        res={}
        for _ in range(n):
            r=c.shot(x+rnd.uniform(-sx,sx),z,ang+rnd.gauss(0,sa),v*max(.2,1+rnd.gauss(0,.06)))
            k=cls(r); res[k]=res.get(k,0)+1
        print(f"  {v:3.1f} m/s: "+"  ".join(f"{k} {n_/n*100:3.0f}%" for k,n_ in sorted(res.items())))
def where(r):
    if r.status==HOLED: return "HOLED"
    if r.status!=REST: return "oob"
    x,z=r.x,r.y
    if x>=5.0: return "altar"
    if z>=17.9 and x>0.3: return "landing/bridge"
    if z>=13.9: return "shrine lane"
    if z>=12.5: return "merge"
    if x>=3.4 and z>=6.6: return "climb"
    if z>=8.1: return "overlook"
    if z>=5.0: return "terrace1"
    return "approach"
table("tee putt north up the approach (x 0, from z 0.8)",0,0.8,0,(2.4,3.0,3.6,4.2),where,sx=0.3)
table("terrace 1 east shot from (0.5,5.8) aimed east, kicker",0.5,5.8,90,(2.2,2.8,3.4),where,sx=0.0,sa=3.0)
table("up the climb from (3.9,6.8) north",3.9,6.8,0,(3.4,4.0,4.6,5.2),where,sx=0.3)
table("merge terrace west->? putt north through the shrine gate from (-0.3,13.0)",-0.3,13.0,0,(1.8,2.4,3.0),where,sx=0.25)
table("shrine lane ball into kicker, from (-0.3,15.0) north",-0.3,15.0,0,(2.0,2.6,3.2),where,sx=0.25)
table("final putt from landing (1.0,18.5) along the bridge to the cup",1.0,18.5,90,(2.2,2.6,3.0,3.4),where,sa=1.5)
