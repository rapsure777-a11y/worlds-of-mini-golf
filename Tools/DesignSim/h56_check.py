"""Paper checks for Holes 5 and 6 (indicative; the 2D model has no cliffs, so only smooth parts and the kickers are checked)."""
import sys, math; sys.path.insert(0,'.')
from golfsim import *
# ---- Hole 5 kickers on a flat deck (heights ignored): outer x -2.5..2.5, z 2.8..7.1 region approximated by a rectangle with the kicker segment
def seg(a,b): return ([a,b],False)
walls=union_walls([(-2.5,2.8,2.5,10.3)])
k=[seg((1.5,3.2),(2.5,4.2))]            # Ramp2Kicker
r2rail=[seg((1.5,4.7),(1.5,7.0))]
c=Course(walls+k+r2rail, lambda x,z:0.0, bounds=(-3,3,2,11))
print("Ramp 2 kicker: strike +x from (0,3.7)")
for v in (1.8,2.2,2.4,3.0):
    r=c.shot(0,3.7,90,v,use_cup=False); print(f"  {v}: rest ({r.x:.2f},{r.y:.2f}) hits {r.hits}")
# upper kickers
cup=(0.4,9.1)
kl=[seg((-2.5,8.7),(-1.6,9.6))]; kr=[seg((2.5,8.7),(1.6,9.6))]
c2=Course(union_walls([(-2.5,7.1,2.5,10.3)])+kl+kr, lambda x,z:0.0, cup=cup, bounds=(-3,3,7,11))
print("Upper kicker (left): strike +z from (-2.15,7.8)")
for v in (1.4,1.8,2.2,2.8):
    r=c2.shot(-2.15,7.8,0,v); print(f"  {v}: status {r.status} rest ({r.x:.2f},{r.y:.2f}) hits {r.hits}")
print("Upper kicker (right): strike +z from (2.15,7.8)")
for v in (1.4,1.8,2.2,2.8):
    r=c2.shot(2.15,7.8,0,v); print(f"  {v}: status {r.status} rest ({r.x:.2f},{r.y:.2f}) hits {r.hits}")
# ---- Hole 6 mill road: lane x -2..-1, rises 7.5% from z=2.0 to 10.2 (heights smooth)
yg=0.613
def road(x,z): return min(yg,max(0.0,(z-2.0)*0.075))
c3=Course(union_walls([(-2.0,0,-1.0,10.2)]), road, bounds=(-2.5,-0.5,-0.5,10.7))
print("Mill road: repeated 3.0 m/s putts up the lane from z=1.7")
z=1.7
for i in range(1,7):
    r=c3.shot(-1.5,z,0,3.0,use_cup=False); print(f"  stroke {i}: from z={z:.2f} rests at z={r.y:.2f} (height {road(-1.5,r.y):.2f}) hits {r.hits}"); z=r.y
    if road(-1.5,z)>=yg-0.05: break
print("Hole 6 tee putt to the dock (flat lane x -0.35..0.35, back curb ignored): rest z for strikes from z=0.6")
c4=Course(union_walls([(-0.35,0,0.35,4.1)]), lambda x,z:0.0, bounds=(-1,1,-0.5,4.6))
for v in (1.9,2.0,2.1,2.2,2.3,2.5):
    r=c4.shot(0,0.6,0,v,use_cup=False); print(f"  {v}: rest z={r.y:.2f}")
