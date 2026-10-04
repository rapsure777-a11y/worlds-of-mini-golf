"""Design-time ball model for Worlds of Mini Golf (see golfsim.c). A paper check for hole concepts, NOT the game:
it ignores 3D (airborne balls), lip-outs and putter mechanics. Validate every concept in Unity before trusting it."""
import ctypes, math, os, random

_lib = ctypes.CDLL(os.path.join(os.path.dirname(os.path.abspath(__file__)), "libgolfsim.so"))


class Params(ctypes.Structure):
    _fields_ = [(n, ctypes.c_double) for n in ("a0", "k", "rest_speed", "rest_time", "bounce", "keep", "radius", "cup_r", "cup_fall", "maxspeed")]


class Result(ctypes.Structure):
    _fields_ = [("status", ctypes.c_int), ("x", ctypes.c_double), ("y", ctypes.c_double), ("t", ctypes.c_double), ("hits", ctypes.c_int),
                ("first_hit_x", ctypes.c_double), ("first_hit_y", ctypes.c_double), ("speed_at_cup", ctypes.c_double)]


P = Params(a0=0.55, k=0.08, rest_speed=0.03, rest_time=0.25, bounce=0.72, keep=0.94, radius=0.0225, cup_r=0.054,
           cup_fall=math.sqrt(2 * 0.0225 * 1.2 / 9.81), maxspeed=9.0)
REST, HOLED, OOB, TIMEOUT = 0, 1, 2, 3
_lib.run.argtypes = [ctypes.POINTER(Params), ctypes.c_int, ctypes.POINTER(ctypes.c_double), ctypes.POINTER(ctypes.c_double), ctypes.c_int, ctypes.c_int,
                     ctypes.c_double, ctypes.c_double, ctypes.c_double, ctypes.c_int, ctypes.POINTER(ctypes.c_double), ctypes.POINTER(ctypes.c_double),
                     ctypes.POINTER(ctypes.c_int), ctypes.c_double, ctypes.c_double, ctypes.c_int, ctypes.c_double, ctypes.c_double, ctypes.c_double,
                     ctypes.c_double, ctypes.c_double, ctypes.POINTER(Result)]


def arr(vals, t=ctypes.c_double):
    return (t * max(1, len(vals)))(*vals)


class Course:
    """walls: list of polylines/polygons (lists of (x, z)); closed=True polygons also get the closing edge.
    height(x, z) -> metres; hazards: polygons that put the ball out of bounds; cup (x, z) or None. Hole-local metres."""

    def __init__(self, walls, height=lambda x, z: 0.0, cup=None, hazards=(), bounds=None, step=0.05):
        self.cup = cup
        segs = []
        for poly, closed in walls:
            n = len(poly)
            for i in range(n if closed else n - 1):
                a, b = poly[i], poly[(i + 1) % n]
                segs += [a[0], a[1], b[0], b[1]]
        self.nseg = len(segs) // 4
        self.segs = arr(segs)
        xs = [p[0] for poly, _ in walls for p in poly]; zs = [p[1] for poly, _ in walls for p in poly]
        x0, x1, z0, z1 = bounds or (min(xs) - 0.5, max(xs) + 0.5, min(zs) - 0.5, max(zs) + 0.5)
        self.gx0, self.gy0, self.gstep = x0, z0, step
        self.gnx, self.gny = int((x1 - x0) / step) + 2, int((z1 - z0) / step) + 2
        self.height = height
        self.grid = arr([height(x0 + i * step, z0 + j * step) for j in range(self.gny) for i in range(self.gnx)])
        hx, hy, hn = [], [], []
        for poly in hazards:
            hn.append(len(poly)); hx += [p[0] for p in poly]; hy += [p[1] for p in poly]
        self.nhaz = len(hn); self.hx, self.hy, self.hn = arr(hx), arr(hy), arr(hn, ctypes.c_int)

    def shot(self, x, z, angle_deg, speed, max_t=40.0, use_cup=True, use_walls=True):
        """angle_deg: direction in the x-z plane, 0 = +z (down the lane), positive toward +x."""
        a = math.radians(angle_deg)
        vx, vz = speed * math.sin(a), speed * math.cos(a)
        return self.shot_v(x, z, vx, vz, max_t, use_cup, use_walls)

    def shot_v(self, x, z, vx, vz, max_t=40.0, use_cup=True, use_walls=True):
        r = Result()
        cx, cz = self.cup if self.cup else (0, 0)
        _lib.run(ctypes.byref(P), self.nseg if use_walls else 0, self.segs, self.grid, self.gnx, self.gny, self.gx0, self.gy0, self.gstep, self.nhaz, self.hx, self.hy,
                 self.hn, cx, cz, 1 if (self.cup and use_cup) else 0, x, z, vx, vz, max_t, ctypes.byref(r))
        return r


def rect(x0, z0, x1, z1):
    return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]


def aim_at(x, z, tx, tz):
    return math.degrees(math.atan2(tx - x, tz - z))


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def speed_to_stop_at(course, x, z, tx, tz, lag=0.0, lo=0.2, hi=8.5):
    """Launch speed (noise-free) that brings the ball to rest `lag` metres past the target along the aim line, by bisection on the model.
    Walls/hazards are included, so use only for straight, unobstructed segments."""
    ang = aim_at(x, z, tx, tz)
    want = dist((x, z), (tx, tz)) + lag
    for _ in range(26):
        mid = (lo + hi) / 2
        r = course.shot(x, z, ang, mid)
        d = dist((x, z), (r.x, r.z if hasattr(r, "z") else r.y))
        if d < want: lo = mid
        else: hi = mid
    return (lo + hi) / 2


PLAYERS = {  # assumed strike scatter: aim error sigma (degrees) and speed error sigma (fraction). CALIBRATE from session logs.
    "casual": (6.0, 0.20),
    "regular": (3.5, 0.13),
    "expert": (1.8, 0.07),
}

# Extra error when a player has to *read* an unfamiliar ricochet (bank) line: a per-attempt bias in aim (degrees) and speed (fraction).
BANK_READ = {"casual": (4.0, 0.12), "regular": (2.5, 0.08), "expert": (1.2, 0.04)}

# Comfortable maximum ball speed (m/s) a player can produce with a controlled swing. ASSUMPTIONS, to be checked against session logs
# (one measured strike: head 1.96 -> ball 2.58 m/s; the smoke-test swing gives 3.24 m/s). Harder requests are clipped to the cap.
SPEED_CAP = {"casual": 3.8, "regular": 4.8, "expert": 6.5}


def union_walls(rects, eps=1e-6):
    """Boundary segments of a union of axis-aligned rectangles (x0, z0, x1, z1): the rails the game's CourseGeometry builds around a layout.
    Returns [(segment_points, False), ...] for Course(walls=...)."""
    def inside(x, z):
        return any(r[0] <= x <= r[2] and r[1] <= z <= r[3] for r in rects)
    xs = sorted({v for r in rects for v in (r[0], r[2])}); zs = sorted({v for r in rects for v in (r[1], r[3])})
    segs = []
    d = 1e-4
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            here = inside(cx, cz)
            for (dx, dz, a, b) in ((0, -1, (xs[i], zs[j]), (xs[i + 1], zs[j])), (0, 1, (xs[i], zs[j + 1]), (xs[i + 1], zs[j + 1])),
                                  (-1, 0, (xs[i], zs[j]), (xs[i], zs[j + 1])), (1, 0, (xs[i + 1], zs[j]), (xs[i + 1], zs[j + 1]))):
                nx_, nz_ = cx + dx * ((xs[i + 1] - xs[i]) / 2 + d), cz + dz * ((zs[j + 1] - zs[j]) / 2 + d)
                if here != inside(nx_, nz_) and here:
                    segs.append(([a, b], False))
    return segs
