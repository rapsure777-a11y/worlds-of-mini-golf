"""Reusable stylised rock kit (Graphics Pass 3B). Original work.

  blender -b --factory-startup --python Tools/Blender/hero_rockkit.py -- [piece ...]

Faceted, chunky forms: each rock is a union of noisy convex hulls with planar fractures, stacked into ledges (strata) and baked with vertex AO.
Textured in Unity by the triplanar rock material (suffix __Rock; reskinned per island: limestone / sandstone / basalt), so no UVs are needed.

  HeroCliff_A/B/C      large cliff masses 5-7 m tall, layered ledges and overhangs
  HeroRock_A..D        medium boulders 1.3-2.8 m
  HeroStone_A..D       small stones 0.3-0.9 m
  HeroSpire_A/B        tall sea-stack / needle rocks 4-7 m
  HeroBasalt_A/B/C     clusters of hexagonal basalt columns with tilted, broken tops
  HeroCrater_A/B       blocky volcanic wall chunks with vertical fractures (crater rim and caldera walls)
Front/back are irrelevant. Origin = centre of the base at ground level (base slightly flattened).
"""
import math
import os
import random
import sys

import bpy
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import archlib as al  # noqa: E402
import gblib as gb  # noqa: E402
from archlib import Piece, hull_geom  # noqa: E402

OUT = "Assets/_Game/Art/Generated/Models"
R = "Rock"


def sphere_points(rnd, n):
    pts = []
    for _ in range(n):
        v = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1)))
        pts.append(v.normalized() if v.length > 1e-6 else Vector((0, 0, 1)))
    return pts


def chunk_points(rnd, size, noise=0.16, n=70, cuts=5, flatten=0.62, horizontal=0.65):
    """Noisy ellipsoid of half-extents `size`, fractured by random planes, flat base at z = -flatten*size.z."""
    sx, sy, sz = size
    pts = []
    for v in sphere_points(rnd, n):
        k = 1.0 + rnd.uniform(-noise, noise)
        pts.append(Vector((v.x * sx * k, v.y * sy * k, v.z * sz * k)))
    for _ in range(cuts):
        a = rnd.uniform(0, math.tau)
        tilt = rnd.uniform(0.0, 1.0 - horizontal) * rnd.choice((-1, 1)) if rnd.random() < 0.5 else rnd.uniform(-0.25, 0.25)
        nrm = Vector((math.cos(a), math.sin(a), tilt)).normalized()
        support = max(p.dot(nrm) for p in pts)
        d = support * rnd.uniform(0.62, 0.86)
        pts = [p - nrm * (p.dot(nrm) - d) if p.dot(nrm) > d else p for p in pts]
    floor = -sz * flatten
    pts = [Vector((p.x, p.y, max(p.z, floor))) for p in pts]
    return pts


def add_hull(p, rnd, center, size, base_color, band=0.1, phase=0.0, **kw):
    pts = chunk_points(rnd, size, **kw)
    v, f = hull_geom([tuple(q) for q in pts])
    cols = []
    for face in f:
        c = Vector((0, 0, 0))
        for i in face:
            c += Vector(v[i])
        c /= len(face)
        z = c.z + center[2]
        k = 1.0 - band + band * (0.5 + 0.5 * math.sin(z * 7.5 + phase))
        j = rnd.uniform(-0.03, 0.03)
        cols.append((base_color[0] * (k + j), base_color[1] * (k + j), base_color[2] * (k + j), 0.0))
    d = p.d[R]
    base = len(d["v"])
    for q in v:
        d["v"].append((q[0] + center[0], q[1] + center[1], q[2] + center[2]))
    for face, col in zip(f, cols):
        d["f"].append(tuple(base + i for i in face))
        d["c"].append(col)


def stacked(p, rnd, base_size, layers, shrink=0.78, spread=0.26, base_color=(0.95, 0.92, 0.86), band=0.1, **kw):
    """Cliff masses: a wide base hull, then smaller hulls stacked with random lateral offsets (ledges, overhangs)."""
    z = 0.0
    sx, sy, sz = base_size
    cx = cy = 0.0
    for i in range(layers):
        add_hull(p, rnd, (cx, cy, z + sz * 0.62), (sx, sy, sz), base_color, band, phase=rnd.uniform(0, 6), **kw)
        z += sz * 1.05
        cx += rnd.uniform(-spread, spread) * sx
        cy += rnd.uniform(-spread, spread) * sy
        sx *= rnd.uniform(shrink - 0.1, shrink + 0.08)
        sy *= rnd.uniform(shrink - 0.1, shrink + 0.08)
        sz *= rnd.uniform(0.8, 1.0)


def cliff(name, seed, base, layers):
    rnd = random.Random(seed)
    p = Piece(name, {R: 2.0}, seed)
    stacked(p, rnd, base, layers, noise=0.2, cuts=6, flatten=0.5)
    # a couple of leaning satellite blocks at the foot
    for _ in range(3):
        a = rnd.uniform(0, math.tau)
        d = base[0] * rnd.uniform(0.85, 1.1)
        s = rnd.uniform(0.35, 0.55)
        add_hull(p, rnd, (math.cos(a) * d, math.sin(a) * d * 0.8, base[2] * 0.25), (base[0] * s, base[1] * s, base[2] * s * 0.9), (0.92, 0.88, 0.8), 0.08, rnd.uniform(0, 6))
    return p


def rock(name, seed, size, layers=1):
    rnd = random.Random(seed)
    p = Piece(name, {R: 2.0}, seed)
    stacked(p, rnd, size, layers, noise=0.2, cuts=5, flatten=0.45, spread=0.2)
    return p


def stone(name, seed, size):
    rnd = random.Random(seed)
    p = Piece(name, {R: 2.0}, seed)
    add_hull(p, rnd, (0, 0, size[2] * 0.4), size, (0.95, 0.92, 0.86), 0.05, 0.0, noise=0.22, n=40, cuts=4, flatten=0.4)
    return p


def spire(name, seed, h, w):
    rnd = random.Random(seed)
    p = Piece(name, {R: 2.0}, seed)
    z = 0.0
    sx = w
    for i in range(5):
        seg = h / 5.0
        add_hull(p, rnd, (rnd.uniform(-0.15, 0.15) * w, rnd.uniform(-0.15, 0.15) * w, z + seg * 0.6), (sx, sx * rnd.uniform(0.8, 1.1), seg * 0.8), (0.93, 0.9, 0.84), 0.12, rnd.uniform(0, 6), noise=0.14, cuts=4, flatten=0.3)
        z += seg * 0.85
        sx *= 0.8
    return p


def basalt(name, seed, count, spread, hmin, hmax):
    """A cluster of hexagonal columns of varied height, tops cut at a tilt, some broken short."""
    rnd = random.Random(seed)
    p = Piece(name, {R: 2.0}, seed)
    placed = []
    tries = 0
    while len(placed) < count and tries < 400:
        tries += 1
        x, y = rnd.uniform(-spread, spread), rnd.uniform(-spread, spread)
        r = rnd.uniform(0.26, 0.42)
        if any((x - px) ** 2 + (y - py) ** 2 < (r + pr) ** 2 * 0.95 for px, py, pr in placed):
            continue
        placed.append((x, y, r))
        h = rnd.uniform(hmin, hmax) * (1.0 - 0.55 * (math.hypot(x, y) / spread))
        tx, ty = rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35)
        rot = rnd.uniform(0, math.tau)
        verts, faces = [], []
        sides = 6
        for k in range(sides):
            a = rot + math.tau * k / sides
            verts.append((x + math.cos(a) * r, y + math.sin(a) * r, -0.4))
        for k in range(sides):
            a = rot + math.tau * k / sides
            px, py = math.cos(a) * r, math.sin(a) * r
            verts.append((x + px, y + py, h + px * tx + py * ty))
        for k in range(sides):
            a, b = k, (k + 1) % sides
            faces.append((a, b, b + sides, a + sides))
        faces.append(tuple(range(sides, 2 * sides)))
        v = 0.9 + rnd.uniform(-0.07, 0.07)
        p.raw(R, verts, faces, (v * 0.55, v * 0.55, v * 0.6, 0.0))
    return p


def crater(name, seed, size):
    """A blocky wall chunk: stacked angular hulls with steep faces (few cuts, little noise)."""
    rnd = random.Random(seed)
    p = Piece(name, {R: 2.0}, seed)
    stacked(p, rnd, size, 3, shrink=0.88, spread=0.12, base_color=(0.62, 0.6, 0.62), band=0.16, noise=0.1, cuts=7, flatten=0.35, n=60)
    return p


PIECES = {
    "HeroCliff_A": lambda: cliff("HeroCliff_A", 101, (3.4, 2.8, 2.4), 3),
    "HeroCliff_B": lambda: cliff("HeroCliff_B", 117, (4.2, 2.4, 2.0), 4),
    "HeroCliff_C": lambda: cliff("HeroCliff_C", 133, (2.8, 3.2, 2.8), 3),
    "HeroRock_A": lambda: rock("HeroRock_A", 201, (1.3, 1.1, 0.9), 2),
    "HeroRock_B": lambda: rock("HeroRock_B", 213, (1.0, 1.4, 0.8), 2),
    "HeroRock_C": lambda: rock("HeroRock_C", 227, (1.5, 1.2, 1.1), 1),
    "HeroRock_D": lambda: rock("HeroRock_D", 239, (0.9, 0.8, 1.2), 2),
    "HeroStone_A": lambda: stone("HeroStone_A", 301, (0.45, 0.38, 0.3)),
    "HeroStone_B": lambda: stone("HeroStone_B", 311, (0.3, 0.34, 0.26)),
    "HeroStone_C": lambda: stone("HeroStone_C", 323, (0.6, 0.45, 0.32)),
    "HeroStone_D": lambda: stone("HeroStone_D", 331, (0.36, 0.3, 0.4)),
    "HeroSpire_A": lambda: spire("HeroSpire_A", 401, 6.5, 1.5),
    "HeroSpire_B": lambda: spire("HeroSpire_B", 417, 4.4, 1.1),
    "HeroBasalt_A": lambda: basalt("HeroBasalt_A", 501, 14, 1.9, 1.2, 3.4),
    "HeroBasalt_B": lambda: basalt("HeroBasalt_B", 513, 9, 1.4, 0.9, 2.4),
    "HeroBasalt_C": lambda: basalt("HeroBasalt_C", 527, 20, 2.6, 1.4, 4.6),
    "HeroCrater_A": lambda: crater("HeroCrater_A", 601, (3.2, 2.2, 2.6)),
    "HeroCrater_B": lambda: crater("HeroCrater_B", 613, (2.4, 3.4, 3.0)),
}


def main():
    want = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name, fn in PIECES.items():
        if want and name not in want:
            continue
        try:
            gb.reset_scene()
            fn().export(OUT, ao_samples=16, ao_dist=1.2)
        except Exception as e:
            import traceback
            al.log(f"{name} FAILED: {e}\n{traceback.format_exc()}")
    al.log("hero_rockkit done")


if __name__ == "__main__":
    main()
