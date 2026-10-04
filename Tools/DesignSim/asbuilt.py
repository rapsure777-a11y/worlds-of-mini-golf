import sys; sys.path.insert(0,'.')
from play import *

def sm(t): return t*t*(3-2*t)
# ---- Hole 1 as built
def h1(x,z): return 0.08*sm(min(1,max(0,(z-2.4)/1.0)))
H1 = Course([(rect(-0.6,0,0.6,7),True)], h1, cup=(0,6.2))
p1 = lambda pos,k: (0,6.2,0.35) if k==1 else (0,6.2,0.12)
# ---- Hole 2 as built
def h2(x,z):
    d=math.hypot(x+0.15,z-4.35)/0.45
    mound=0.05*(0.5+0.5*math.cos(d*math.pi)) if d<1 else 0
    ramp=-0.04*sm(min(1,max(0,(x+1.2)/(-2.6+1.2))))
    return mound+ramp
H2 = Course([([(-0.6,0),(0.6,0),(0.6,5.2),(-3.6,5.2),(-3.6,4.0),(-0.6,4.0)],True)], h2, cup=(-3.0,4.6))
def p2(pos,k):
    if pos[1] < 3.9 and pos[0] > -0.7: return (0.0,4.6,0.3)
    return (-3.0,4.6,0.12)
# ---- Hole 3 as built
def h3(x,z):
    t=max(0,min(1,1-abs(x-5.2)/2.4)); hump=0.18*sm(t)
    lane=max(0,min(1,(z-6.4)/0.8)); lean=-0.03*sm(min(1,max(0,(x-8.8)/1.2)))*sm(lane)
    return hump+lean
H3 = Course([([(-0.6,0),(0.6,0),(0.6,3.4),(3.4,3.4),(3.4,4.0),(7.0,4.0),(7.0,3.4),(10,3.4),(10,9.2),(8.8,9.2),(8.8,6.4),(7.0,6.4),(7.0,5.2),(3.4,5.2),(3.4,5.8),(-0.6,5.8)],True)], h3, cup=(9.4,8.5))
def p3(pos,k):
    x,z=pos
    if x < 0.7 and z < 3.4: return (0.0,5.0,0.3)            # up the tee lane into the elbow
    if x < 3.3: return (4.6,4.6,0.0) if False else (8.2,4.6,0.0)  # through the bridge mouth toward the landing pad
    if x < 7.0: return (8.2,4.6,0.0)
    if z < 6.3: return (9.4,6.9,0.2)                         # up into the final lane
    return (9.4,8.5,0.12)
if __name__=="__main__":
    show("Hole 1 as built (par 2, 7 m straight lane, rise 8 cm)", stats(H1,(0,0.6),p1,2))
    show("Hole 2 as built (par 3, L-shaped dogleg)", stats(H2,(0,0.6),p2,3))
    show("Hole 3 as built (par 3, elbow + hump bridge + pad + lane)", stats(H3,(0,0.6),p3,3))
