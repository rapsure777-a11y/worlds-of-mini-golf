"""Hero jungle assets for Jungle Island (Blender, Z-up; exported Y-up for Unity).

  blender -b --factory-startup --python Tools/Blender/hero_jungle.py -- <out_dir>

Exports (written to <out>, default Assets/_Game/Art/Generated/Models):
  HeroJungleTree_0.fbx, HeroJungleTree_1.fbx   buttressed rainforest trees, 11.5 m / 9.5 m, with a branching canopy of leaf cards
      ...__Bark    trunk, buttress roots and branches (tiled bark texture, cylinder UVs)
      ...__Leaves  canopy cards from the leaf atlas ('broadleaf', 'monstera', 'bush_b') plus hanging vines ('ivy'); two-sided cut-out
  HeroVines_0.fbx                              a bundle of hanging vine strands (about 4.5 m) to hang from bridges, ledges and branches
      HeroVines_0__Leaves

Origin: tree base at the ground; vines hang down from the origin. Vertex colour RGB = baked ambient occlusion, A = wind weight.
Must be run locally (Blender 5.x): Cloud Claude could not execute it. The Unity world code skips these models with a warning if absent.
"""
import math
import os
import random
import sys

from mathutils import Vector, noise

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gblib as gb  # noqa: E402

OUT = gb.out_dir("Assets/_Game/Art/Generated/Models")
ATLAS = 2048.0
# Pixel rectangles (x, y, w, h), origin bottom-left. Keep in sync with leaf_atlas.py / HeroKit.LeafAtlas.
CELLS = {
    "broadleaf": (512, 0, 512, 512), "monstera": (1024, 0, 512, 512), "bush_b": (1024, 512, 512, 512),
    "ivy": (1024, 1536, 512, 512),
}


def cell_uv(name, u, v):
    x, y, w, h = CELLS[name]
    inset = 3.0
    return ((x + inset + u * (w - 2 * inset)) / ATLAS, (y + inset + v * (h - 2 * inset)) / ATLAS)


def tube(name, path, radius_fn, sides=14, tile=1.6, wind_fn=None):
    """Swept tube along path (list of Vectors). radius_fn(theta, t, i) in metres. UV: u around, v along (metres / tile)."""
    verts, faces, uvs, cols = [], [], [], []
    n = len(path)
    frame_n = Vector((0, 1, 0))
    rings = []
    length = [0.0]
    for i in range(1, n):
        length.append(length[-1] + (path[i] - path[i - 1]).length)
    for i, p in enumerate(path):
        t = i / (n - 1)
        tan = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
        if abs(tan.dot(frame_n)) > 0.95:
            frame_n = Vector((1, 0, 0))
        frame_n = (frame_n - tan * frame_n.dot(tan)).normalized()
        frame_b = tan.cross(frame_n)
        rings.append(len(verts))
        for s in range(sides + 1):
            a = s / sides * math.tau
            d = frame_n * math.cos(a) + frame_b * math.sin(a)
            verts.append(tuple(p + d * radius_fn(a, t, i)))
    for i in range(n - 1):
        for s in range(sides):
            a = rings[i] + s
            b, c, d = a + 1, rings[i + 1] + s + 1, rings[i + 1] + s
            faces.append((a, b, c, d))
            for vi, (ss, ii) in zip((a, b, c, d), ((s, i), (s + 1, i), (s + 1, i + 1), (s, i + 1))):
                uvs.append((ss / sides * 1.2, length[ii] / tile))
                cols.append((1, 1, 1, wind_fn(ii / (n - 1)) if wind_fn else 0.0))
    return gb.mesh_object(name, verts, faces, uvs, cols)


def card(center, normal, size, cell, roll, wind, aspect=1.0):
    """Quad facing `normal` (leaf cards are two-sided in Unity), rotated by roll, mapped to an atlas cell."""
    normal = Vector(normal).normalized()
    ref = Vector((0, 0, 1)) if abs(normal.z) < 0.9 else Vector((1, 0, 0))
    right = normal.cross(ref).normalized()
    up = right.cross(normal).normalized()
    c, s = math.cos(roll), math.sin(roll)
    right, up = right * c + up * s, up * c - right * s
    hw, hh = size * aspect * 0.5, size * 0.5
    pts = [center - right * hw - up * hh, center + right * hw - up * hh, center + right * hw + up * hh, center - right * hw + up * hh]
    uvc = [cell_uv(cell, 0, 0), cell_uv(cell, 1, 0), cell_uv(cell, 1, 1), cell_uv(cell, 0, 1)]
    return [tuple(p) for p in pts], uvc, [(1, 1, 1, wind)] * 4


def cards_object(name, cards):
    verts, faces, uvs, cols = [], [], [], []
    for pts, uvc, col in cards:
        base = len(verts)
        verts.extend(pts)
        faces.append((base, base + 1, base + 2, base + 3))
        uvs.extend(uvc)
        cols.extend(col)
    return gb.mesh_object(name, verts, faces, uvs, cols, smooth=False)


def vine_strand(origin, length, seed, width=0.22, cell="ivy"):
    """Hanging ribbon with a gentle S sway; returns card-style (pts, uvs, cols) quads stacked along the strand."""
    rnd = random.Random(seed)
    segs = 9
    out = []
    phase = rnd.uniform(0, math.tau)
    amp = rnd.uniform(0.08, 0.22)
    yaw = rnd.uniform(0, math.pi)
    side = Vector((math.cos(yaw), math.sin(yaw), 0))
    pts = []
    for i in range(segs + 1):
        t = i / segs
        off = side * (math.sin(phase + t * 5.0) * amp * t)
        pts.append(origin + Vector((0, 0, -length * t)) + off)
    for i in range(segs):
        a, b = pts[i], pts[i + 1]
        ta, tb = i / segs, (i + 1) / segs
        wa = width * (1.0 - 0.45 * ta)
        wb = width * (1.0 - 0.45 * tb)
        face = Vector((-side.y, side.x, 0))  # strip faces perpendicular to its sway axis
        quad = [a - side * wa, a + side * wa, b + side * wb, b - side * wb]
        # texture: base of the ivy leaf cell at the top of the strand, tip at the bottom (v runs 1 -> 0 downward)
        uvc = [cell_uv(cell, 0.15, 1 - ta), cell_uv(cell, 0.85, 1 - ta), cell_uv(cell, 0.85, 1 - tb), cell_uv(cell, 0.15, 1 - tb)]
        cols = [(1, 1, 1, 0.25 + 0.75 * ta), (1, 1, 1, 0.25 + 0.75 * ta), (1, 1, 1, 0.25 + 0.75 * tb), (1, 1, 1, 0.25 + 0.75 * tb)]
        out.append(([tuple(p) for p in quad], uvc, cols))
    return out


def jungle_tree(index, height, seed, canopy_r, buttresses):
    gb.reset_scene()
    rnd = random.Random(seed)
    lean = Vector((rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), 0))
    # Trunk path: slight S curve.
    rings = 60
    path = []
    for i in range(rings):
        t = i / (rings - 1)
        wob = Vector((math.sin(t * 3.1 + seed) * 0.25, math.cos(t * 2.3 + seed) * 0.25, 0)) * t * height * 0.08
        path.append(Vector((0, 0, t * height)) + lean * t * t * 0.6 + wob)
    phase = rnd.uniform(0, math.tau)

    def trunk_r(th, t, i):
        base = 0.52 - 0.22 * t
        # Buttress roots: ridges flaring near the ground.
        ridge = abs(math.sin(buttresses * (th + phase) * 0.5)) ** 3
        flare = max(0.0, 1.0 - t * 9.0) ** 2
        n = noise.noise(Vector((math.cos(th) * 2, math.sin(th) * 2, t * 6 + seed))) * 0.05
        return base * (1.0 + flare * (0.35 + 1.5 * ridge)) * (1 + n)

    trunk = tube("Trunk", path, trunk_r, sides=20, tile=1.7, wind_fn=lambda t: 0.0)
    parts = [trunk]
    # Branches reaching canopy clusters.
    tips = []
    nb = rnd.randint(4, 5)
    for k in range(nb):
        ang = k * math.tau / nb + rnd.uniform(-0.3, 0.3)
        d = Vector((math.cos(ang), math.sin(ang), 0))
        start_i = int(rings * rnd.uniform(0.62, 0.8))
        start = path[start_i]
        reach = canopy_r * rnd.uniform(0.7, 1.0)
        end = start + d * reach + Vector((0, 0, rnd.uniform(0.6, 1.8)))
        bp = []
        for j in range(12):
            u = j / 11
            bp.append(start.lerp(end, u) + Vector((0, 0, math.sin(u * math.pi) * 0.45)))
        parts.append(tube("Branch", bp, lambda th, t, i: 0.19 * (1.0 - 0.7 * t), sides=9, tile=1.4))
        tips.append(end)
    tips.append(path[-1] + Vector((0, 0, 0.3)))
    bark = gb.join(parts, f"HeroJungleTree_{index}__Bark")

    cards = []
    cells = ["broadleaf", "monstera", "bush_b", "broadleaf"]
    for tip in tips:
        centre = tip + Vector((0, 0, 0.5))
        for _ in range(34):
            d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.15, 1.0))).normalized()
            pos = centre + Vector((d.x * 1.0, d.y * 1.0, d.z * 0.7)) * rnd.uniform(0.5, 1.5)
            size = rnd.uniform(1.4, 2.4)
            cards.append(card(pos, d, size, rnd.choice(cells), rnd.uniform(0, math.tau), min(1.0, 0.45 + 0.4 * (pos.z / height))))
    # Hanging vines from the branch tips and the trunk.
    for tip in tips[:-1]:
        for k in range(3):
            o = tip + Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), -0.2))
            cards.extend(vine_strand(o, rnd.uniform(2.6, 5.5), seed * 31 + len(cards) + k))
    leaves = cards_object(f"HeroJungleTree_{index}__Leaves", cards)
    gb.bake_vertex_ao(bark, samples=32, distance=2.0)
    gb.ensure_color_attr(leaves)
    gb.bake_vertex_ao(leaves, samples=16, distance=1.5, ground=False)
    gb.export_fbx_objects(os.path.join(OUT, f"HeroJungleTree_{index}.fbx"), [bark, leaves])


def vines(index, seed, strands=8):
    gb.reset_scene()
    rnd = random.Random(seed)
    cards = []
    for k in range(strands):
        o = Vector((rnd.uniform(-0.7, 0.7), rnd.uniform(-0.25, 0.25), rnd.uniform(-0.1, 0.1)))
        cards.extend(vine_strand(o, rnd.uniform(2.8, 4.6), seed * 7 + k, width=rnd.uniform(0.18, 0.3)))
    obj = cards_object(f"HeroVines_{index}__Leaves", cards)
    gb.ensure_color_attr(obj)
    gb.export_fbx_objects(os.path.join(OUT, f"HeroVines_{index}.fbx"), [obj])


jungle_tree(0, 11.5, 101, 4.2, 6)
jungle_tree(1, 9.5, 207, 3.6, 5)
vines(0, 55)
print("[hero_jungle] done")
