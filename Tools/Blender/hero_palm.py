"""Hero coconut palms for Tropical Adventure (Blender, Z-up; exported Y-up for Unity).

  blender -b --factory-startup --python Tools/Blender/hero_palm.py -- <out_dir>

Each variant exports <out>/HeroPalm_<n>.fbx with two objects named by material suffix:
  ...__Bark   trunk, crown boss and coconuts (tiled bark texture, box/cylinder UVs)
  ...__Leaves fronds using the leaf atlas cells 'palm_frond' / 'palm_dry' (alpha cut-out, two-sided)
Vertex colour alpha = wind weight. Vertex colour RGB = baked ambient occlusion.
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
FROND_CELL = (0, 0, 512, 1024)
DRY_CELL = (0, 1024, 512, 1024)


def cell_uv(cell, u, v):
    x, y, w, h = cell
    return ((x + u * w) / ATLAS, (y + v * h) / ATLAS)


def trunk(height, lean, seed):
    rnd = random.Random(seed)
    sides, rings = 14, 90
    seg_len = 0.32
    verts, faces, uvs, cols = [], [], [], []
    path = []
    wob = Vector((rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), 0))
    for i in range(rings):
        t = i / (rings - 1)
        path.append(Vector((lean * height * t ** 1.8, 0.0, t * height)) + wob * math.sin(t * math.pi) * height * 0.3)
    frame_n = Vector((0, 1, 0))
    ring_starts = []
    for i, p in enumerate(path):
        t = i / (rings - 1)
        tan = (path[min(i + 1, rings - 1)] - path[max(i - 1, 0)]).normalized()
        frame_n = (frame_n - tan * frame_n.dot(tan)).normalized()
        frame_b = tan.cross(frame_n)
        h = t * height
        f = (h / seg_len) % 1.0
        # Each segment flares at its base: an overlapping stacked look.
        flare = 1.0 + 0.16 * (1.0 - f) ** 3
        base_r = (0.24 - 0.11 * t) * (1.35 - 0.35 * min(1.0, t * 12))
        ring_starts.append(len(verts))
        for s in range(sides + 1):
            a = s / sides * math.tau
            d = frame_n * math.cos(a) + frame_b * math.sin(a)
            n = noise.noise(Vector((math.cos(a) * 2, math.sin(a) * 2, h * 3 + seed))) * 0.04
            verts.append(tuple(p + d * base_r * (flare + n)))
    for i in range(rings - 1):
        for s in range(sides):
            a = ring_starts[i] + s
            b, c, d = a + 1, ring_starts[i + 1] + s + 1, ring_starts[i + 1] + s
            faces.append((a, b, c, d))
            for vi, (uu, vv) in zip((a, b, c, d), ((s, i), (s + 1, i), (s + 1, i + 1), (s, i + 1))):
                uvs.append((uu / sides * 1.0, path[vv].z / 1.9))
                cols.append((1, 1, 1, (path[vv].z / height) ** 2 * 0.2))
    obj = gb.mesh_object("Trunk", verts, faces, uvs, cols)
    return obj, path[-1], (path[-1] - path[-4]).normalized()


def frond(origin, direction, length, width, droop, rise, cell, segs=16, twist=0.0, wind=1.0):
    """Curved, slightly V-folded frond strip mapped to an atlas cell (base at v=0, tip at v=1)."""
    verts, faces, uvs, cols = [], [], [], []
    side = direction.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    rows = []
    for i in range(segs + 1):
        t = i / segs
        p = origin + direction * (t * length) + Vector((0, 0, 1)) * (rise * t * length - droop * t * t * length)
        # Width follows the drawn frond silhouette; fold rises the midrib.
        w = width * (0.35 + 0.65 * math.sin(math.pi * min(1.0, t * 1.08 + 0.02)))
        tw = math.radians(twist) * t
        sd = side * math.cos(tw) + Vector((0, 0, 1)) * math.sin(tw)
        up = sd.cross(direction).normalized()
        fold = w * 0.22
        row = []
        for k, u in enumerate((0.0, 0.5, 1.0)):
            off = (u - 0.5) * 2 * w
            lift = fold * (1 - abs(u - 0.5) * 2)
            row.append(len(verts))
            verts.append(tuple(p + sd * off + up * lift))
        rows.append(row)
    for i in range(segs):
        for k in range(2):
            a, b = rows[i][k], rows[i][k + 1]
            c, d = rows[i + 1][k + 1], rows[i + 1][k]
            faces.append((a, b, c, d))
            for vi, (uu, vv) in zip((a, b, c, d), ((k, i), (k + 1, i), (k + 1, i + 1), (k, i + 1))):
                uvs.append(cell_uv(cell, uu * 0.5, vv / segs))
                cols.append((1, 1, 1, min(1.0, 0.15 + 0.85 * (vv / segs)) * wind))
    return gb.mesh_object("Frond", verts, faces, uvs, cols)


def sphere(center, r, segs=10, rings=8, uv_scale=1.0):
    verts, faces, uvs, cols = [], [], [], []
    for j in range(rings + 1):
        phi = math.pi * j / rings
        for i in range(segs + 1):
            th = math.tau * i / segs
            verts.append(tuple(center + Vector((math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi))) * r))
    for j in range(rings):
        for i in range(segs):
            a = j * (segs + 1) + i
            faces.append((a, a + segs + 1, a + segs + 2, a + 1))
            for (ii, jj) in ((i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)):
                uvs.append((ii / segs * uv_scale, jj / rings * uv_scale * 0.5))
                cols.append((1, 1, 1, 0.1))
    return gb.mesh_object("Sphere", verts, faces, uvs, cols)


def build(index, height, lean, fronds, seed):
    gb.reset_scene()
    rnd = random.Random(seed)
    trunk_obj, top, top_dir = trunk(height, lean, seed)
    parts = [trunk_obj]
    # Crown boss: fibrous bulb.
    boss = sphere(top + Vector((0, 0, 0.05)), 0.26, 12, 8, uv_scale=2.0)
    parts.append(boss)
    for i in range(rnd.randint(3, 5)):
        ang = rnd.uniform(0, math.tau)
        c = top + Vector((math.cos(ang) * 0.2, math.sin(ang) * 0.2, -0.22 + rnd.uniform(-0.05, 0.05)))
        parts.append(sphere(c, 0.12, 10, 8))
    bark = gb.join(parts, f"HeroPalm_{index}__Bark")

    leaves = []
    yaw0 = rnd.uniform(0, math.tau)
    for i in range(fronds):
        layer = i % 2
        yaw = yaw0 + i * math.tau / fronds + rnd.uniform(-0.15, 0.15)
        d = Vector((math.cos(yaw), math.sin(yaw), 0))
        length = rnd.uniform(2.3, 2.9) * (0.85 if layer else 1.0)
        leaves.append(frond(top + Vector((0, 0, 0.12 * layer)), d, length, 0.62, droop=rnd.uniform(0.75, 1.05) - 0.25 * layer,
                            rise=rnd.uniform(0.35, 0.6) + 0.25 * layer, cell=FROND_CELL, twist=rnd.uniform(-25, 25)))
    # A few young fronds pointing up and dried fronds hanging beneath the crown.
    for i in range(3):
        yaw = yaw0 + rnd.uniform(0, math.tau)
        d = Vector((math.cos(yaw) * 0.35, math.sin(yaw) * 0.35, 0.94)).normalized()
        leaves.append(frond(top, d, 1.4, 0.4, droop=0.25, rise=0.0, cell=FROND_CELL, segs=10, wind=0.7))
    for i in range(3):
        yaw = yaw0 + rnd.uniform(0, math.tau)
        d = Vector((math.cos(yaw) * 0.45, math.sin(yaw) * 0.45, -0.89)).normalized()
        leaves.append(frond(top + Vector((0, 0, -0.1)), d, 1.9, 0.5, droop=0.0, rise=0.15, cell=DRY_CELL, segs=10, wind=0.6))
    leaf_obj = gb.join(leaves, f"HeroPalm_{index}__Leaves")
    gb.ensure_color_attr(leaf_obj)
    gb.bake_vertex_ao(bark, samples=32, distance=1.5)
    gb.bake_vertex_ao(leaf_obj, samples=24, distance=1.2)
    gb.export_fbx_objects(os.path.join(OUT, f"HeroPalm_{index}.fbx"), [bark, leaf_obj])


build(0, 4.6, 0.32, 13, 11)
build(1, 5.6, 0.42, 14, 23)
build(2, 3.8, 0.22, 12, 37)
print("[hero_palm] done")
