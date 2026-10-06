"""Temple Island hero architecture (Graphics Pass 3B). Original work, no third-party assets.

  blender -b --factory-startup --python Tools/Blender/hero_temple.py -- [piece ...]

Exports to Assets/_Game/Art/Generated/Models (front faces -Y; origin = centre of the front base at ground level):
  HeroTempleFacade     the monumental sun temple behind the Sun Stair: plinth, three setback tiers, a corbel-arch doorway, columns, stair, quoins,
                       carved bands, a sun relief, flanking carved pillars with braziers
  HeroBrazierPillar    a small carved pier with a sun panel, cap and a flaming bronze bowl (lines the terraces)
  HeroArcade           a 3 m aqueduct arcade (two arches, piers, cornice) that carries the elevated Waterwheel Mill channel
  HeroMillHouse        the mill: stone ground floor with inset door and windows, half-timber upper floor, hip roof with eaves, chimney
Material groups: __Limestone (textured masonry, world-scale UVs), __Paint (vertex colour: gold, bronze, dark insets, cloth), __Wood, __Lantern (flame).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import archlib as al  # noqa: E402
import gblib as gb  # noqa: E402
from archlib import Piece, stone  # noqa: E402

OUT = "Assets/_Game/Art/Generated/Models"
GOLD = (0.98, 0.76, 0.26, 0)
GOLD_DARK = (0.78, 0.55, 0.16, 0)
RED = (0.68, 0.09, 0.07, 0)
DARK = (0.045, 0.04, 0.035, 0)
BRONZE = (0.36, 0.25, 0.13, 0)
FLAME = (1.0, 0.62, 0.15, 0)
TIMBER = (0.55, 0.37, 0.2, 0)
PLASTER = (0.88, 0.82, 0.70, 0)
L, P, W, F = "Limestone", "Paint", "Wood", "Ember"


def carved_face(p, cx, y, zc, w, depth=0.07):
    """A stylised stone face on a pier front (y = the face plane): brows, framed eyes, a nose block, a toothed mouth."""
    s = w / 0.72
    p.box(L, (cx, y - 0.02, zc + 0.27 * s), (0.58 * s, 0.07, 0.08 * s), color=stone(1.0, 0.02), bevel=0.02)
    for sx in (-1, 1):
        p.box(L, (cx + sx * 0.17 * s, y - 0.03, zc + 0.12 * s), (0.2 * s, 0.07, 0.16 * s), color=stone(0.98), bevel=0.02)
        p.box(P, (cx + sx * 0.17 * s, y - 0.065, zc + 0.12 * s), (0.12 * s, 0.03, 0.09 * s), color=DARK, bevel=0.008)
    p.box(L, (cx, y - 0.05, zc - 0.05 * s), (0.13 * s, 0.1, 0.3 * s), color=stone(1.0, 0.02), bevel=0.025)
    p.box(P, (cx, y - 0.03, zc - 0.28 * s), (0.42 * s, 0.04, 0.09 * s), color=DARK, bevel=0.008)
    for k in range(5):
        p.box(L, (cx - 0.16 * s + k * 0.08 * s, y - 0.05, zc - 0.28 * s), (0.05 * s, 0.045, 0.075 * s), color=stone(1.0), bevel=0.008)


def carved_pier(p, cx, y0, depth, z0, z1, w):
    """A square pier of stacked courses: carved faces on the middle courses, plain base and cap."""
    p.box(L, (cx, y0 + depth / 2, z0 + 0.19), (w + 0.2, depth + 0.2, 0.38), color=stone(0.86), bevel=0.04)
    seg = (z1 - z0 - 0.38 - 0.22) / 4.0
    for i in range(4):
        za = z0 + 0.38 + i * seg
        p.box(L, (cx, y0 + depth / 2, za + seg / 2), (w, depth, seg - 0.012), color=stone(0.95 - 0.02 * (i % 2), 0.02), bevel=0.03)
        if i in (1, 2):
            carved_face(p, cx, y0, za + seg / 2, w * 0.9)
    p.box(L, (cx, y0 + depth / 2, z1 - 0.11), (w + 0.22, depth + 0.22, 0.22), color=stone(1.0), bevel=0.04)


def brazier(p, cx, cy, z, r=0.3):
    p.prism(P, (cx, cy, z), r * 0.62, r, r * 0.8, 14, BRONZE, 0.015)
    p.prism(P, (cx, cy, z + r * 0.78), r * 1.0, r * 1.0, 0.04, 14, GOLD_DARK, 0.01)
    p.prism(F, (cx, cy, z + r * 0.8), r * 0.52, r * 0.06, r * 1.15, 8, FLAME, 0.0)


# ------------------------------------------------------------------ the facade

def facade():
    p = Piece("HeroTempleFacade", {L: 1.8, P: 1.0, F: 1.0}, seed=11)
    # plinth and the setback tiers
    p.mass(L, -3.9, 3.9, -0.15, 2.9, -0.8, 0.0, ch=0.4, bw=0.8, base=0.78, bevel=0.035)
    p.mass(L, -3.5, 3.5, 0.25, 2.6, 0.0, 1.1, ch=0.275, bw=0.62, exclude_front=[(-3.6, 3.6, 0.55, 0.75)])
    p.box(L, (0, 1.42, 1.16), (7.3, 2.6, 0.12), color=stone(1.0), bevel=0.04)          # tier A cap with a drip overhang
    # carved band across tier A: a dark recess with a key pattern of alternating blocks
    p.box(P, (0, 0.27, 0.65), (7.0, 0.05, 0.2), color=DARK, bevel=0.0)
    for i in range(23):
        x = -3.3 + i * 0.3
        p.box(L, (x, 0.2, 0.65 + (0.03 if i % 2 else -0.03)), (0.2, 0.07, 0.12), color=stone(1.0, 0.02), bevel=0.012)
    # quoins: alternating long/short corner stones on tier A
    for sx in (-1, 1):
        for r in range(4):
            long = r % 2 == 0
            p.box(L, (sx * (3.5 - (0.22 if long else 0.14)), 0.2, 0.11 + r * 0.275), (0.5 if long else 0.34, 0.1, 0.25), color=stone(1.0, 0.02), bevel=0.02)
    # stair (decorative) with sloping cheeks
    p.stairs(L, -0.95, 0.95, -1.3, 0.25, 1.1, 6, color=stone(0.97), bevel=0.02)
    for sx in (-1, 1):
        x0, x1 = (sx * 1.0, sx * 1.3) if sx > 0 else (sx * 1.3, sx * 1.0)
        p.wedge(L, x0, x1, -1.3, 0.25, 0.3, 1.45, color=stone(0.95))
    # tier B with a corbel-arch doorway
    door = [(-0.5, 0.5, 1.22, 1.97), (-0.38, 0.38, 1.97, 2.17), (-0.26, 0.26, 2.17, 2.37)]
    p.mass(L, -2.6, 2.6, 0.9, 2.4, 1.22, 2.62, ch=0.24, bw=0.55, exclude_front=door)
    p.box(P, (0, 1.04, 1.62), (1.04, 0.05, 0.8), color=DARK, bevel=0.0)
    p.box(P, (0, 1.04, 2.1), (0.78, 0.05, 0.4), color=DARK, bevel=0.0)
    p.box(P, (0, 1.04, 2.37), (0.55, 0.05, 0.14), color=DARK, bevel=0.0)
    for sx in (-1, 1):
        p.box(L, (sx * 0.62, 0.84, 1.6), (0.2, 0.18, 0.76), color=stone(1.0), bevel=0.03)
        p.box(L, (sx * 0.55, 0.84, 2.07), (0.34, 0.18, 0.2), color=stone(0.98), bevel=0.03)
        p.box(L, (sx * 0.4, 0.84, 2.27), (0.34, 0.18, 0.2), color=stone(1.0), bevel=0.03)
    p.box(L, (0, 0.84, 2.48), (1.5, 0.2, 0.22), color=stone(1.0, 0.02), bevel=0.035)
    for sx in (-1, 1):
        p.column(L, sx * 1.22, 0.62, 1.22, 1.3, 0.14, color=stone(0.98))
        # carved square panels beside the door with a small sun
        p.box(L, (sx * 1.95, 0.84, 1.92), (0.72, 0.1, 0.72), color=stone(1.0), bevel=0.03)
        p.box(L, (sx * 1.95, 0.8, 1.92), (0.56, 0.06, 0.56), color=stone(0.88), bevel=0.025)
        p.sun(P, sx * 1.95, 0.77, 1.92, 0.2, rays=10, ring_grp=L)
    p.box(L, (0, 1.64, 2.68), (5.6, 1.74, 0.12), color=stone(1.0), bevel=0.04)
    # tier C: the sun shrine
    p.mass(L, -1.9, 1.9, 1.3, 2.3, 2.74, 4.0, ch=0.25, bw=0.55, exclude_front=[(-0.62, 0.62, 2.78, 3.98)])
    p.box(L, (0, 1.38, 3.38), (1.5, 0.14, 1.5), color=stone(0.82), bevel=0.04, ao=False)
    p.sun(P, 0, 1.3, 3.38, 0.62, rays=18, ring_grp=L)
    p.box(L, (0, 1.8, 4.06), (4.15, 1.2, 0.12), color=stone(1.0), bevel=0.04)
    for k, (w, h) in enumerate(((0.9, 0.18), (0.62, 0.16), (0.36, 0.16))):
        p.box(L, (0, 1.8, 4.18 + sum(x[1] for x in ((0.9, 0.18), (0.62, 0.16), (0.36, 0.16))[:k]) + h / 2), (w, w, h), color=stone(1.0), bevel=0.03)
    p.hull(P, [(-0.17, 1.63, 4.68), (0.17, 1.63, 4.68), (0.17, 1.97, 4.68), (-0.17, 1.97, 4.68), (0, 1.8, 5.05)], GOLD)
    # flanking carved pillars with braziers
    for sx in (-1, 1):
        carved_pier(p, sx * 4.42, 0.35, 0.85, -0.8, 3.3, 0.76)
        brazier(p, sx * 4.42, 0.78, 3.3, 0.3)
    return p


def brazier_pillar():
    p = Piece("HeroBrazierPillar", {L: 1.4, P: 1.0, F: 1.0}, seed=21)
    p.box(L, (0, 0.28, 0.07), (0.68, 0.56, 0.14), color=stone(0.86), bevel=0.03)
    p.box(L, (0, 0.28, 0.45), (0.5, 0.42, 0.62), color=stone(0.97), bevel=0.03)
    p.box(L, (0, 0.04, 0.46), (0.36, 0.06, 0.36), color=stone(0.84), bevel=0.02)
    p.sun(P, 0, 0.0, 0.46, 0.13, rays=10, ring_grp=L)
    p.box(L, (0, 0.28, 0.82), (0.62, 0.52, 0.12), color=stone(1.0), bevel=0.035)
    brazier(p, 0, 0.28, 0.88, 0.26)
    return p


def arcade():
    """A 3.0 m long, 2.0 m deep aqueduct arcade seen from -Y: two round arches, three piers (the ends are half piers), a cornice and a parapet lip."""
    p = Piece("HeroArcade", {L: 1.6, P: 1.0}, seed=31)
    Wd, D, H = 3.0, 2.0, 1.0
    p.box(L, (0, D / 2, -0.4), (Wd + 0.2, D + 0.2, 0.8), color=stone(0.78), bevel=0.04)           # footing
    # arch openings: centres at x=-0.75 and +0.75, radius 0.5 springing at z 0.1
    rad, spring = 0.5, 0.15
    piers = [(-1.5, -1.25), (-0.25, 0.25), (1.25, 1.5)]
    for (xa, xb) in piers:
        p.box(L, ((xa + xb) / 2, D / 2, spring / 2 + 0.0), (xb - xa, D, spring + 0.0), color=stone(0.95), bevel=0.03)
    for cx in (-0.75, 0.75):
        p.arch(L, cx, 0.0, spring, rad, 0.2, D, n=9)
        p.arch(L, cx, D - 0.2, spring, rad, 0.2, 0.2, n=9)
    # spandrel + wall above the arches up to the cornice course
    p.box(L, (0, D / 2, 0.88), (Wd, D, 0.18), color=stone(0.92), bevel=0.03)
    for (xa, xb) in ((-1.5, -1.25 - 0.0), (-0.25, 0.25), (1.25, 1.5)):
        p.box(L, ((xa + xb) / 2, D / 2, 0.5), (xb - xa + 0.0, D, 0.7), color=stone(0.95), bevel=0.03)
    # fill the corners between arch rings and the straight piers
    for cx in (-0.75, 0.75):
        p.box(L, (cx - 0.55, D / 2, 0.45), (0.1, D, 0.5), color=stone(0.9), bevel=0.02)
        p.box(L, (cx + 0.55, D / 2, 0.45), (0.1, D, 0.5), color=stone(0.9), bevel=0.02)
    p.box(L, (0, D / 2, H - 0.1), (Wd + 0.16, D + 0.16, 0.14), color=stone(1.0), bevel=0.04)         # cornice
    for i in range(12):                                                                                 # dentils along both faces
        x = -1.4 + i * 0.255
        p.box(L, (x, -0.1, H - 0.2), (0.14, 0.09, 0.09), color=stone(0.92), bevel=0.012)
        p.box(L, (x, D + 0.1, H - 0.2), (0.14, 0.09, 0.09), color=stone(0.92), bevel=0.012)
    return p


def mill_house():
    p = Piece("HeroMillHouse", {L: 1.5, W: 1.2, "Thatch": 1.4, P: 1.0}, seed=41)
    Wd, D = 2.8, 2.6
    p.mass(L, -Wd / 2, Wd / 2, 0.0, D, -0.5, 1.5, ch=0.25, bw=0.5, base=0.88,
           exclude_front=[(-0.45, 0.45, 0.0, 1.15)])
    # doorway: dark recess, plank door left ajar, timber frame
    p.box(P, (0, 0.14, 0.58), (0.92, 0.05, 1.17), color=DARK, bevel=0.0)
    p.box(W, (-0.32, 0.1, 0.55), (0.34, 0.06, 1.05), color=TIMBER, bevel=0.015)
    for sx in (-1, 1):
        p.box(W, (sx * 0.52, 0.04, 0.6), (0.14, 0.2, 1.24), color=TIMBER, bevel=0.025)
    p.box(W, (0, 0.04, 1.23), (1.2, 0.2, 0.16), color=TIMBER, bevel=0.025)
    p.box(L, (0, 0.04, 1.36), (1.5, 0.16, 0.14), color=stone(1.0), bevel=0.03)                       # lintel stone
    # window openings on both side walls (dark recess + sill + frame), as inset panels
    for sx in (-1, 1):
        for yc in (0.9, 1.9):
            p.box(P, (sx * (Wd / 2 + 0.03), yc, 0.8), (0.04, 0.5, 0.45), color=DARK, bevel=0.0)
            p.box(W, (sx * (Wd / 2 + 0.06), yc, 0.52), (0.1, 0.62, 0.07), color=TIMBER, bevel=0.015)
            p.box(W, (sx * (Wd / 2 + 0.05), yc, 1.06), (0.08, 0.62, 0.07), color=TIMBER, bevel=0.015)
    # half-timber upper floor
    p.box(L, (0, D / 2, 1.5), (Wd + 0.08, D + 0.08, 0.1), color=stone(1.0), bevel=0.03)             # string course
    p.box(L, (0, D / 2, 2.05), (Wd - 0.1, D - 0.1, 1.0), color=PLASTER, bevel=0.02, ao=False)
    for x in (-1.2, -0.6, 0.0, 0.6, 1.2):
        p.box(W, (x, -0.0, 2.05), (0.1, 0.1, 1.0), color=TIMBER, bevel=0.02)
    for yb in (0.0, D):
        p.box(W, (0, yb - 0.0, 1.58), (Wd, 0.1, 0.1), color=TIMBER, bevel=0.02)
        p.box(W, (0, yb - 0.0, 2.52), (Wd, 0.1, 0.1), color=TIMBER, bevel=0.02)
    for sx in (-1, 1):
        for yb in (0.1, D / 2, D - 0.1):
            p.box(W, (sx * (Wd / 2 - 0.0), yb, 2.05), (0.1, 0.1, 1.0), color=TIMBER, bevel=0.02)
        p.box(W, (sx * Wd / 2, D / 2, 1.58), (0.1, D, 0.1), color=TIMBER, bevel=0.02)
        p.box(W, (sx * Wd / 2, D / 2, 2.52), (0.1, D, 0.1), color=TIMBER, bevel=0.02)
    for k in range(2):
        p.box(W, (-0.3 + k * 0.6, -0.02, 2.05), (0.06, 0.06, 1.18), color=TIMBER, bevel=0.01, rot=(0, math.radians(35 - 70 * k), 0))
    # roof: hip with overhanging eaves and a ridge cap
    p.roof_hip("Thatch", 0, D / 2, 2.57, Wd + 0.5, D + 0.5, 0.85, 1.0, thick=0.15, color=(0.86, 0.76, 0.58, 0))
    p.box(W, (0, D / 2, 3.52), (1.06, 0.14, 0.1), color=TIMBER, bevel=0.025)
    # stone chimney with a cap
    p.box(L, (0.85, 1.7, 2.6), (0.5, 0.5, 1.6), color=stone(0.92), bevel=0.03)
    p.box(L, (0.85, 1.7, 3.45), (0.62, 0.62, 0.12), color=stone(1.0), bevel=0.03)
    return p


PIECES = {"HeroTempleFacade": facade, "HeroBrazierPillar": brazier_pillar, "HeroArcade": arcade, "HeroMillHouse": mill_house}


def main():
    want = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name, fn in PIECES.items():
        if want and name not in want:
            continue
        try:
            gb.reset_scene()
            piece = fn()
            piece.export(OUT, ao_samples=20, ao_dist=0.7)
        except Exception as e:  # keep going so one broken piece does not hide the rest
            import traceback
            al.log(f"{name} FAILED: {e}\n{traceback.format_exc()}")
    al.log("hero_temple done")


if __name__ == "__main__":
    main()
