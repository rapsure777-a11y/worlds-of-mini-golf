"""Hero dock props for Tropical Adventure: barrel and crates (Blender, Z-up; exported Y-up for Unity).

  blender -b --factory-startup --python Tools/Blender/hero_props.py -- <out_dir>

Exports (default Assets/_Game/Art/Generated/Models):
  HeroBarrel_0.fbx   0.88 m tall staved barrel with a bulge, chamfered rim, recessed plank lid and three iron hoops
  HeroCrate_0.fbx    0.60 m shipping crate: corner posts, gapped slats, cross brace, lid planks, iron corner plates
  HeroCrate_1.fbx    0.45 m crate (same build, different seed)
Objects are named by material suffix: __Wood (tiling wood texture, world-scale box UVs) and __Paint (iron, vertex colour).
Origin: centre of the base at ground level. Vertex colour RGB = tint x baked AO; A = 0 (no wind).
"""
import math
import os
import random
import sys

import bpy
from mathutils import Euler, Matrix, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gblib as gb  # noqa: E402

OUT = gb.out_dir("Assets/_Game/Art/Generated/Models")
WOOD_TILE = 1.2
IRON = (0.17, 0.16, 0.16, 0)

parts = {}


def reset():
    gb.reset_scene()
    parts.clear()
    parts.update({"Wood": [], "Paint": []})


def box(group, center, size, rot=(0, 0, 0), color=(1, 1, 1, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        m = o.modifiers.new("bevel", "BEVEL")
        m.width = bevel
        m.segments = 2
        m.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier="bevel")
    gb.ensure_color_attr(o, color)
    parts[group].append(o)
    return o


def lathe(group, profile, sides, color, radius_mod=None, tile=WOOD_TILE):
    """Surface of revolution around Z. profile = [(radius, z)] bottom to top. radius_mod(theta, z) -> multiplier (stave grooves)."""
    verts, faces, uvs, cols = [], [], [], []
    n = len(profile)
    for j, (r, z) in enumerate(profile):
        for i in range(sides + 1):
            th = math.tau * i / sides
            rr = r * (radius_mod(th, z) if radius_mod else 1.0)
            verts.append((math.cos(th) * rr, math.sin(th) * rr, z))
    for j in range(n - 1):
        for i in range(sides):
            a = j * (sides + 1) + i
            faces.append((a, a + 1, a + sides + 2, a + sides + 1))
            for (ii, jj) in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                r = profile[jj][0]
                uvs.append((math.tau * ii / sides * r / tile, profile[jj][1] / tile))
                cols.append(color)
    o = gb.mesh_object(group + "_lathe", verts, faces, uvs, cols)
    parts[group].append(o)
    return o


def hoop(z, radius, height, thick, sides=28):
    """Iron band: a short outer shell with a visible thickness (outer wall, top and bottom lips)."""
    prof = [(radius, z - height * 0.5), (radius + thick, z - height * 0.5 + thick * 0.4), (radius + thick, z + height * 0.5 - thick * 0.4),
            (radius, z + height * 0.5)]
    lathe("Paint", prof, sides, IRON)


def build_barrel():
    h = 0.88
    sides = 24
    stave_n = 12

    def bulge(z):
        t = z / h
        return 0.255 + 0.065 * math.sin(math.pi * t) ** 1.3

    rows = 14
    prof = [(bulge(h * k / rows), h * k / rows) for k in range(rows + 1)]

    def grooves(th, z):
        # Shallow V-groove where two staves meet, so the barrel reads as planks even from a metre away.
        f = (th / math.tau * stave_n) % 1.0
        edge = min(f, 1.0 - f)
        return 1.0 - 0.018 * max(0.0, 1.0 - edge * 14.0)

    lathe("Wood", prof, sides * 2, (0.95, 0.9, 0.85, 0), radius_mod=grooves)
    # Chamfered rim and a recessed lid made of planks.
    lathe("Wood", [(bulge(h) , h), (bulge(h) - 0.012, h + 0.012), (bulge(h) - 0.03, h + 0.012), (bulge(h) - 0.03, h - 0.045)], sides * 2, (0.8, 0.72, 0.6, 0))
    for k in range(-3, 4):
        w = 0.0725
        x = k * w
        half = math.sqrt(max(0.0, (bulge(h) - 0.032) ** 2 - x * x))
        if half > 0.02:
            box("Wood", (x, 0, h - 0.052), (w - 0.006, half * 2, 0.014), color=(0.92 + 0.02 * (k % 3), 0.86, 0.78, 0), bevel=0.003)
    lathe("Wood", [(0.0, 0.0), (bulge(0.0) * 0.96, 0.0)], sides * 2, (0.5, 0.45, 0.4, 0))  # base disc
    # Iron hoops (two near the ends, one lower in the belly, one near the top).
    for z in (0.085, 0.27, 0.62, 0.795):
        hoop(z, bulge(z) + 0.004, 0.05, 0.012)


def build_crate(size, seed):
    rnd = random.Random(seed)
    s = size
    b = s * 0.11          # corner post / frame thickness
    core = s - 2 * b * 0.5
    # Dark inner core so the gaps between slats read as depth rather than see-through holes.
    box("Wood", (0, 0, s * 0.5), (s * 0.93, s * 0.93, s * 0.93), color=(0.28, 0.2, 0.14, 0))
    # Corner posts.
    for x in (-1, 1):
        for y in (-1, 1):
            box("Wood", (x * (s * 0.5 - b * 0.5), y * (s * 0.5 - b * 0.5), s * 0.5), (b, b, s),
                color=(0.8, 0.68, 0.52, 0), bevel=s * 0.012)
    # Slats on the four sides: gapped planks that stick out slightly past the core.
    slats = 3
    gap = s * 0.018
    sh = (s - 2 * b - gap * (slats + 1)) / slats
    for side in range(4):
        rot = (0, 0, math.pi / 2 * side)
        for k in range(slats):
            z = b + gap + sh * 0.5 + k * (sh + gap)
            tint = rnd.uniform(0.88, 1.06)
            # Place on +Y face then rotate about Z.
            c = Euler(rot).to_matrix() @ Vector((0, s * 0.5 - b * 0.28, z))
            box("Wood", tuple(c), (s - 2 * b * 0.95, b * 0.55, sh), rot=rot, color=(tint, tint * 0.97, tint * 0.92, 0), bevel=s * 0.008)
        # Diagonal brace on two opposite sides: a plank tilted 45 degrees in the face plane.
        if side % 2 == 0:
            length = (s - 2 * b) * math.sqrt(2.0) * 0.97
            m = Matrix.Rotation(math.pi / 2 * side, 4, "Z") @ Matrix.Translation((0, s * 0.5 + b * 0.02, s * 0.5)) @ Matrix.Rotation(math.radians(45), 4, "Y")
            box("Wood", m.to_translation(), (length, b * 0.34, b * 0.7), rot=m.to_euler(), color=(0.75, 0.63, 0.5, 0), bevel=s * 0.006)
    # Lid: planks across the top, slightly proud, with a rim.
    planks = 4
    pw = (s - 2 * b * 0.2) / planks
    for k in range(planks):
        tint = rnd.uniform(0.9, 1.08)
        box("Wood", (-s * 0.5 + pw * 0.5 + k * pw + b * 0.1, 0, s + s * 0.006), (pw - gap * 0.7, s * 0.97, s * 0.03),
            color=(tint, tint * 0.97, tint * 0.9, 0), bevel=s * 0.006)
    # Iron corner plates (small L-brackets on the top corners) and a few nail heads on each post.
    for x in (-1, 1):
        for y in (-1, 1):
            cx, cy = x * (s * 0.5 - b * 0.1), y * (s * 0.5 - b * 0.1)
            box("Paint", (cx, cy, s * 0.97), (b * 1.15, b * 1.15, s * 0.05), color=IRON, bevel=s * 0.006)
            box("Paint", (cx, cy, s * 0.03), (b * 1.15, b * 1.15, s * 0.05), color=IRON, bevel=s * 0.006)
            for zz in (0.25, 0.5, 0.75):
                box("Paint", (x * (s * 0.5 + b * 0.03), y * (s * 0.5 - b * 0.5), s * zz), (b * 0.12, b * 0.2, b * 0.2), color=IRON)


def finish(name):
    out = []
    for group, objs in parts.items():
        if not objs:
            continue
        o = gb.join(objs, f"{name}__{group}")
        if group == "Wood":
            gb.box_uv(o, WOOD_TILE)
        out.append(o)
    for o in out:
        gb.ensure_color_attr(o)
        gb.bake_vertex_ao(o, samples=24, distance=0.25)
    gb.export_fbx_objects(os.path.join(OUT, f"{name}.fbx"), out)
    print(f"[hero_props] {name}: " + ", ".join(f"{o.name}={len(o.data.polygons)}" for o in out))


reset()
build_barrel()
finish("HeroBarrel_0")
reset()
build_crate(0.60, 11)
finish("HeroCrate_0")
reset()
build_crate(0.45, 23)
finish("HeroCrate_1")
print("[hero_props] done")
