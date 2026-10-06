"""Starting Island and jungle hero structures (Graphics Pass 3B). Original work.

  blender -b --factory-startup --python Tools/Blender/hero_start.py -- [piece ...]

  HeroShipwreck     a beached wooden ship: curved ribs on a keel, half-planked hull with broken, missing strakes, raked stem and stern posts, a partial deck,
                    a snapped leaning mast with rope lashings and a fallen yard. About 6.6 m long, rolled to starboard. Origin at the keel's midpoint on the sand.
  HeroRuinArch      a weathered stone arch with a broken shoulder and fallen blocks
  HeroRuinPillar_A  a broken fluted column with its capital lying beside it;   HeroRuinPillar_B a stub column on a plinth
  HeroRuinWall      a collapsed wall fragment with a doorway gap and a stepped broken top
Material groups: __Wood (weathered planks, world UVs), __Rope, __Limestone (ruins), __Paint (vertex colour).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import archlib as al  # noqa: E402
import gblib as gb  # noqa: E402
from archlib import Piece, stone  # noqa: E402

OUT = "Assets/_Game/Art/Generated/Models"
W, ROPE, L, P = "Wood", "Rope", "Limestone", "Paint"
WEATHERED = (0.62, 0.52, 0.42, 0)


def shipwreck():
    p = Piece("HeroShipwreck", {W: 1.4, ROPE: 0.5, P: 1.0}, seed=71)
    rnd = p.rnd
    Lh, B, zc, depth = 3.35, 1.3, 1.25, 1.75

    def half_beam(x):
        return B * max(0.05, (1 - (abs(x) / (Lh + 0.35)) ** 2.4)) ** 0.5

    def section(x, th):
        """A point on the hull section at station x, angle th in [0, pi] from the starboard gunwale round the bilge to port."""
        b = half_beam(x)
        dpt = depth * (1 - 0.45 * (abs(x) / (Lh + 0.3)) ** 2)
        return Vector((x, b * math.cos(th), zc - dpt * math.sin(th) ** 0.8))

    with p.tf(Matrix.Rotation(math.radians(-13), 4, "X")):
        stations = [-Lh + i * (2 * Lh / 12) for i in range(13)]
        arcs = 12
        # ribs
        for i, x in enumerate(stations):
            if i in (1, 11, 12) and rnd.random() < 0.6:
                continue
            top = arcs if i not in (0, 12, 11, 1) else arcs - rnd.randint(2, 4)
            for k in range(top):
                a, b = section(x, math.pi * k / arcs), section(x, math.pi * (k + 1) / arcs)
                if k > arcs * 0.72 and rnd.random() < 0.35:
                    continue
                p.beam(W, a, b, 0.16, 0.09, color=(0.52, 0.42, 0.33, 0), bevel=0.012)
        # strakes (planking): long boards that run several stations, many missing near the rails and ends so the ribs show through
        for k in range(arcs):
            th0, th1, thm = math.pi * k / arcs, math.pi * (k + 1) / arcs, math.pi * (k + 0.5) / arcs
            edge = abs(k - arcs / 2 + 0.5) / (arcs / 2)       # 0 at the keel, ~1 at the rails
            i = rnd.randint(0, 2)
            while i < 12:
                span = rnd.choice((2, 3, 3, 4))
                j = min(12, i + span)
                xa, xb = stations[i], stations[j]
                end = max(abs(xa), abs(xb)) / Lh
                keep = 0.98 - 0.62 * edge ** 1.6 - 0.45 * max(0.0, end - 0.55) ** 1.2
                if rnd.random() < keep and j > i:
                    pa, pb = section(xa, thm), section(xb, thm)
                    mida, midb = (section(xa, th0) + section(xa, th1)) / 2, (section(xb, th0) + section(xb, th1)) / 2
                    ca, cb = (section(xa, th0), section(xa, th1)), (section(xb, th0), section(xb, th1))
                    mid = (pa + pb) / 2
                    out = Vector((0, mid.y, mid.z - (zc - depth * 0.28))).normalized()
                    wid = ((ca[1] - ca[0]).length + (cb[1] - cb[0]).length) / 2 * 0.96
                    tang = section((xa + xb) / 2, th1) - section((xa + xb) / 2, th0)
                    ang = math.atan2(tang.z, tang.y)
                    shade = rnd.uniform(0.86, 1.06)
                    col = (WEATHERED[0] * shade, WEATHERED[1] * shade, WEATHERED[2] * shade, 0)
                    p.beam(W, pa + out * 0.07, pb + out * 0.07, wid, 0.055, color=col, bevel=0.01, roll=ang)
                i = j + (1 if rnd.random() < 0.25 else 0)
        # keel, raked stem and stern post
        p.beam(W, (-Lh - 0.2, 0, zc - depth - 0.05), (Lh + 0.35, 0, zc - depth - 0.05), 0.22, 0.14, color=(0.45, 0.36, 0.28, 0), bevel=0.02)
        p.beam(W, (Lh + 0.3, 0, zc - depth * 0.4 - 0.1), (Lh + 0.95, 0, zc + 0.75), 0.2, 0.16, color=(0.5, 0.4, 0.3, 0), bevel=0.02)
        p.beam(W, (-Lh - 0.15, 0, zc - depth * 0.4 - 0.1), (-Lh - 0.55, 0, zc + 1.1), 0.22, 0.18, color=(0.5, 0.4, 0.3, 0), bevel=0.02)
        # deck beams and a partial deck near the stern
        for i in range(1, 12, 2):
            x = stations[i]
            b = half_beam(x)
            if i > 8 and rnd.random() < 0.5:
                continue
            p.beam(W, (x, -b - 0.05, zc - 0.3), (x, b + 0.05, zc - 0.3), 0.16, 0.11, color=(0.5, 0.4, 0.31, 0), bevel=0.014)
        for i in range(0, 4):
            x = stations[i] + 0.27
            b = half_beam(x) - 0.07
            for j in range(7):
                y = -b + (2 * b) * j / 6.0
                if rnd.random() < 0.2:
                    continue
                p.box(W, (x + 0.2, y, zc - 0.22), (0.9 + rnd.uniform(-0.1, 0.15), 2 * b / 6.5, 0.045), color=(0.6 + rnd.uniform(-0.05, 0.03), 0.5, 0.4, 0), bevel=0.01)
        # the mast stump: leaning, snapped with splinters, lashed with rope
        mx = -0.6
        base = Vector((mx, 0, zc - 0.75))
        top = Vector((mx - 0.4, 0.15, zc + 2.25))
        p.beam(W, base, top, 0.3, 0.3, color=(0.55, 0.43, 0.32, 0), bevel=0.03)
        for _ in range(5):
            sp = top + Vector((rnd.uniform(-0.1, 0.1), rnd.uniform(-0.1, 0.1), 0.0))
            p.beam(W, sp, sp + Vector((rnd.uniform(-0.1, 0.1), rnd.uniform(-0.1, 0.1), rnd.uniform(0.15, 0.4))), 0.05, 0.04, color=(0.7, 0.58, 0.4, 0), bevel=0.004)
        for k in range(5):
            c = base.lerp(top, 0.2 + k * 0.14)
            p.prism(ROPE, (c.x, c.y, c.z - 0.02), 0.19, 0.19, 0.05, 10, (0.85, 0.78, 0.6, 0), 0.0)
        # the fallen yard: a long spar slanting from the mast to the sand, with rope hanging from it
        p.beam(W, (mx - 0.4, 0.2, zc + 1.9), (mx - 2.9, 1.7, 0.1), 0.17, 0.17, color=(0.55, 0.43, 0.32, 0), bevel=0.02)
        prev = Vector((mx - 0.4, 0.2, zc + 1.9))
        for k in range(1, 9):
            t = k / 8.0
            q = Vector((mx - 0.2 * t, 0.2 - 0.9 * t, zc + 1.9 - 2.4 * t - 0.2 * math.sin(math.pi * t)))
            p.beam(ROPE, prev, q, 0.05, 0.05, color=(0.8, 0.72, 0.55, 0), bevel=0.006)
            prev = q
    return p


def ruin_arch():
    p = Piece("HeroRuinArch", {L: 1.8, P: 1.0}, seed=81)
    rnd = p.rnd

    def green():
        return (0.72 + rnd.uniform(0, 0.06), 0.82 + rnd.uniform(0, 0.06), 0.62, 0)

    p.mass(L, -1.55, -0.85, -0.3, 0.7, -1.0, 2.3, ch=0.27, bw=0.5, base=0.82)
    p.mass(L, 0.85, 1.55, -0.3, 0.7, -1.0, 1.55, ch=0.27, bw=0.5, base=0.82)       # the right shoulder collapsed lower
    p.box(L, (-1.2, 0.2, 2.36), (0.95, 1.1, 0.14), color=stone(0.85), bevel=0.04)
    n, spring, r_in, thick = 11, 2.3, 0.85, 0.3
    R = r_in + thick / 2
    arc = math.pi * R / n
    for i in range(n):
        if i < 4:
            continue                                                                # only the left two-thirds of the ring survives
        th = math.pi * (i + 0.5) / n
        x, z = R * math.cos(th), spring + R * math.sin(th)
        p.box(L, (x, 0.2, z), (arc - 0.012, 1.0, thick), rot=(0, math.atan2(-math.cos(th), -math.sin(th)), 0), color=green() if rnd.random() < 0.3 else stone(0.82), bevel=0.02)
    for _ in range(9):
        x, y = rnd.uniform(0.2, 2.3), rnd.uniform(-0.8, 1.2)
        s = rnd.uniform(0.25, 0.6)
        p.box(L, (x, y, -0.7 + s / 2), (s * rnd.uniform(0.9, 1.5), s, s * rnd.uniform(0.7, 1.1)), rot=(rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(0, 3)),
              color=green() if rnd.random() < 0.4 else stone(0.8), bevel=0.035)
    return p


def ruin_pillar_a():
    p = Piece("HeroRuinPillar_A", {L: 1.8, P: 1.0}, seed=91)
    p.box(L, (0, 0, -0.35), (1.1, 1.1, 0.7), color=stone(0.8), bevel=0.05)
    p.prism(L, (0, 0, 0.0), 0.5, 0.45, 0.2, 14, stone(0.84), 0.025)
    for k in range(3):
        p.prism(L, (0, 0, 0.2 + k * 0.52), 0.4 - k * 0.012, 0.39 - k * 0.012, 0.5, 14, stone(0.85 - 0.02 * k), 0.02)
    ring = [math.tau * i / 12 for i in range(12)]
    p.hull(L, [(math.cos(a) * 0.37, math.sin(a) * 0.37, 1.76 + math.cos(a) * 0.22) for a in ring] + [(math.cos(a) * 0.38, math.sin(a) * 0.38, 1.7) for a in ring], stone(0.84))
    p.box(L, (1.2, 0.8, -0.1), (0.95, 0.95, 0.28), rot=(0, 0.2, 0.7), color=stone(0.88), bevel=0.04)
    p.prism(L, (1.0, -0.2, -0.28), 0.38, 0.36, 1.0, 14, stone(0.82), 0.02, rot=(0, math.pi / 2, 0.4))
    return p


def ruin_pillar_b():
    p = Piece("HeroRuinPillar_B", {L: 1.8, P: 1.0}, seed=93)
    p.box(L, (0, 0, -0.3), (0.95, 0.95, 0.6), color=stone(0.8), bevel=0.05)
    p.box(L, (0, 0, 0.18), (0.78, 0.78, 0.36), color=stone(0.86), bevel=0.04)
    p.prism(L, (0, 0, 0.36), 0.33, 0.31, 0.7, 12, stone(0.84), 0.02)
    p.box(L, (0.7, -0.3, -0.18), (0.5, 0.4, 0.4), rot=(0.1, 0.2, 0.9), color=stone(0.78), bevel=0.04)
    return p


def ruin_wall():
    p = Piece("HeroRuinWall", {L: 1.8, P: 1.0}, seed=95)
    rnd = p.rnd
    p.mass(L, -1.8, -0.5, 0.0, 0.7, -0.8, 1.9, ch=0.26, bw=0.5, base=0.82)
    p.mass(L, 0.5, 2.1, 0.0, 0.7, -0.8, 1.3, ch=0.26, bw=0.5, base=0.82)
    for k in range(5):
        h = rnd.uniform(0.15, 0.5)
        p.box(L, (0.7 + k * 0.3, 0.35, 1.3 + h / 2), (0.28, 0.6, h), color=stone(rnd.uniform(0.78, 0.9)), bevel=0.03)
    for _ in range(6):
        p.box(L, (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.8, -0.2), -0.4), (rnd.uniform(0.3, 0.5), 0.3, 0.32), rot=(0, 0, rnd.uniform(0, 3)), color=stone(0.8), bevel=0.035)
    return p


PIECES = {"HeroShipwreck": shipwreck, "HeroRuinArch": ruin_arch, "HeroRuinPillar_A": ruin_pillar_a, "HeroRuinPillar_B": ruin_pillar_b, "HeroRuinWall": ruin_wall}


def main():
    want = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name, fn in PIECES.items():
        if want and name not in want:
            continue
        try:
            gb.reset_scene()
            fn().export(OUT, ao_samples=20, ao_dist=0.7)
        except Exception as e:
            import traceback
            al.log(f"{name} FAILED: {e}\n{traceback.format_exc()}")
    al.log("hero_start done")


if __name__ == "__main__":
    main()
