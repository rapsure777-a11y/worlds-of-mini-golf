"""Hero tiki poles and masks for the Starting Island (Blender, Z-up; exported Y-up for Unity).

  blender -b --factory-startup --python Tools/Blender/hero_tiki.py -- <out_dir>

Exports (default Assets/_Game/Art/Generated/Models):
  HeroTikiPole_0.fbx   2.3 m carved pole: three stacked faces, flared crown with a bird head, two painted wings
  HeroTikiPole_1.fbx   1.7 m pole: two faces and a cap, different paint
  HeroTikiMask_0.fbx   standing mask on a stake (about 1.75 m): shield board, relief face, feather crest
Objects are named by material suffix: __Totem (carved wood, cylinder UVs), __Wood (planks, world-scale box UVs), __Paint (vertex colour).
Front faces -Y in Blender (Unity -Z): place with a yaw that points the model's back away from the viewer, as with the clubhouse.
Origin: centre of the base at ground level. Vertex colour RGB = paint x baked AO; A = 0. Original work, no third-party assets.
"""
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gblib as gb  # noqa: E402

OUT = gb.out_dir("Assets/_Game/Art/Generated/Models")
WOOD_TILE = 1.4
parts = {}

TEAL = (0.10, 0.62, 0.66, 0)
ORANGE = (0.95, 0.55, 0.12, 0)
RED = (0.78, 0.12, 0.10, 0)
CREAM = (0.98, 0.95, 0.85, 0)
DARK = (0.08, 0.06, 0.05, 0)
YELLOW = (1.0, 0.82, 0.22, 0)


def reset():
    gb.reset_scene()
    parts.clear()
    parts.update({"Totem": [], "Wood": [], "Paint": []})


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


def cylinder(group, base, radius_fn, height, sides=16, rings=8, tile=1.0, color_fn=None, cap=True):
    """Vertical cylinder; radius_fn(theta, t) in metres; UVs: u around (metres/tile), v along."""
    verts, faces, uvs, cols = [], [], [], []
    for j in range(rings + 1):
        t = j / rings
        for i in range(sides + 1):
            th = math.tau * i / sides
            r = radius_fn(th, t)
            verts.append((base[0] + math.cos(th) * r, base[1] + math.sin(th) * r, base[2] + t * height))
    for j in range(rings):
        for i in range(sides):
            a = j * (sides + 1) + i
            faces.append((a, a + 1, a + sides + 2, a + sides + 1))
            for (ii, jj) in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                th = math.tau * ii / sides
                r = radius_fn(th, jj / rings)
                uvs.append((th * r / tile, jj / rings * height / tile))
                cols.append(color_fn(th, jj / rings) if color_fn else (1, 1, 1, 0))
    if cap:
        c = len(verts)
        verts.append((base[0], base[1], base[2] + height))
        last = rings * (sides + 1)
        for i in range(sides):
            faces.append((last + i, last + i + 1, c))
            for _ in range(3):
                uvs.append((0.5, 0.5))
                cols.append(color_fn(0, 1) if color_fn else (1, 1, 1, 0))
    o = gb.mesh_object("cyl", verts, faces, uvs, cols)
    parts[group].append(o)
    return o


# ------------------------------------------------------------------ poles
def pole(tiers, tier_h, base_r, bands, wings, crown):
    """Carved pole facing -Y: each tier carries a face (brow, eyes, nose, grinning mouth with teeth) and a painted band."""
    height = tier_h * tiers + 0.35
    fwd = -math.pi / 2  # -Y

    def features(th, t):
        z = t * height
        if z > tier_h * tiers:
            return 0.0, (1, 1, 1, 0)
        f = (z % tier_h) / tier_h
        phi = math.atan2(math.sin(th - fwd), math.cos(th - fwd))
        d, col = 0.0, (1, 1, 1, 0)
        if f < 0.04 or f > 0.97:
            return -0.025, (0.7, 0.6, 0.5, 0)
        if abs(phi) < 1.25:
            d += 0.045 * math.exp(-((f - 0.74) / 0.05) ** 2) * (1 - (phi / 1.25) ** 2)
            for side in (-1, 1):
                de = math.hypot((phi - side * 0.38) / 0.2, (f - 0.6) / 0.085)
                if de < 1.0:
                    d -= 0.045 * (1 - de ** 2)
                    col = DARK if de < 0.4 else CREAM
            d += 0.06 * math.exp(-(phi / 0.13) ** 2) * math.exp(-((f - 0.45) / 0.1) ** 2)
            dm = math.hypot(phi / 0.62, (f - 0.24) / 0.07)
            if dm < 1.0:
                d -= 0.055 * (1 - dm ** 2)
                teeth = abs(f - 0.24) < 0.03 and (math.sin(phi * 30) > 0)
                col = CREAM if teeth else (0.55, 0.08, 0.06, 0)
        if 0.86 < f < 0.94:
            col = bands[int(z / tier_h) % len(bands)]
        return d, col

    def rad(th, t):
        z = t * height
        if z <= tier_h * tiers:
            return base_r + features(th, t)[0]
        u = (z - tier_h * tiers) / 0.35
        return base_r * (1.0 + 0.5 * math.sin(min(1.0, u) * math.pi * 0.5)) if crown else base_r * (1.0 - 0.3 * u)

    cylinder("Totem", (0, 0, 0), rad, height, sides=40, rings=80 * tiers // 2 + 40, tile=WOOD_TILE,
             color_fn=lambda th, t: features(th, t)[1])
    top = height
    if wings:
        # Two painted wings on the middle tier, swept slightly up and back.
        zc = tier_h * (tiers - 1) + tier_h * 0.5
        for side in (-1, 1):
            m = Matrix.Translation((side * (base_r + 0.27), 0.0, zc)) @ Matrix.Rotation(side * math.radians(-18), 4, "Y")
            box("Wood", m.to_translation(), (0.56, 0.05, 0.3), rot=m.to_euler(), color=(0.9, 0.75, 0.6, 0), bevel=0.01)
            for k, col in enumerate((TEAL, ORANGE, RED)):
                mm = m @ Matrix.Translation((0, -0.03, 0.09 - k * 0.09))
                box("Paint", mm.to_translation(), (0.5, 0.012, 0.05), rot=mm.to_euler(), color=col)
    if crown:
        # A bird head on top: round head, hooked beak, two eyes.
        box("Totem", (0, 0, top + 0.12), (0.34, 0.34, 0.26), color=(0.9, 0.78, 0.62, 0), bevel=0.06)
        box("Paint", (0, -0.2, top + 0.1), (0.1, 0.2, 0.09), rot=(math.radians(20), 0, 0), color=YELLOW, bevel=0.015)
        box("Paint", (0, -0.31, top + 0.03), (0.07, 0.1, 0.07), rot=(math.radians(50), 0, 0), color=ORANGE, bevel=0.01)
        for side in (-1, 1):
            box("Paint", (side * 0.1, -0.15, top + 0.2), (0.07, 0.03, 0.07), color=CREAM)
            box("Paint", (side * 0.1, -0.165, top + 0.2), (0.035, 0.03, 0.035), color=DARK)
        for k in range(5):
            a = math.radians(-60 + k * 30)
            m = Matrix.Translation((math.sin(a) * 0.08, 0.0, top + 0.27)) @ Matrix.Rotation(-a, 4, "Y")
            box("Paint", m.to_translation(), (0.05, 0.03, 0.32), rot=m.to_euler(),
                color=(RED, YELLOW, TEAL, YELLOW, RED)[k], bevel=0.008)
    else:
        box("Totem", (0, 0, top + 0.02), (base_r * 2.1, base_r * 2.1, 0.07), color=(0.85, 0.7, 0.55, 0), bevel=0.02)


# ------------------------------------------------------------------ mask on a stake
def mask():
    z0 = 1.05
    cylinder("Wood", (0, 0, 0), lambda th, t: 0.045 + 0.004 * math.sin(th * 3 + t * 8), z0 + 0.25, sides=10, rings=6, tile=WOOD_TILE)
    # Shield: a tall board with a diamond top.
    box("Totem", (0, 0, z0 + 0.35), (0.5, 0.07, 0.62), color=(0.92, 0.78, 0.62, 0), bevel=0.012)
    m = Matrix.Translation((0, 0, z0 + 0.66)) @ Matrix.Rotation(math.radians(45), 4, "Y")
    box("Totem", m.to_translation(), (0.35, 0.07, 0.35), rot=m.to_euler(), color=(0.92, 0.78, 0.62, 0), bevel=0.012)
    y = -0.04
    zf = z0 + 0.35
    # Painted border.
    for sx in (-1, 1):
        box("Paint", (sx * 0.235, y - 0.004, zf), (0.03, 0.012, 0.6), color=TEAL)
    box("Paint", (0, y - 0.004, zf - 0.285), (0.44, 0.012, 0.03), color=TEAL)
    # Brow, eyes, nose, mouth.
    box("Wood", (0, y - 0.025, zf + 0.15), (0.36, 0.045, 0.05), color=(0.85, 0.7, 0.55, 0), bevel=0.01)
    for sx in (-1, 1):
        box("Paint", (sx * 0.1, y - 0.01, zf + 0.07), (0.13, 0.03, 0.08), color=DARK, bevel=0.01)
        box("Paint", (sx * 0.1, y - 0.028, zf + 0.07), (0.1, 0.02, 0.06), color=CREAM, bevel=0.008)
        box("Paint", (sx * 0.1, y - 0.04, zf + 0.07), (0.045, 0.02, 0.045), color=DARK)
    box("Wood", (0, y - 0.045, zf - 0.03), (0.075, 0.07, 0.2), color=(0.85, 0.7, 0.55, 0), bevel=0.012)
    box("Paint", (0, y - 0.012, zf - 0.19), (0.3, 0.03, 0.095), color=(0.55, 0.08, 0.06, 0), bevel=0.012)
    for k in range(5):
        box("Paint", (-0.1 + k * 0.05, y - 0.03, zf - 0.19), (0.036, 0.02, 0.06), color=CREAM)
    # Feather crest.
    for k in range(7):
        a = math.radians(-66 + k * 22)
        m = Matrix.Translation((math.sin(a) * 0.12, 0.0, z0 + 0.96 + math.cos(a) * 0.03)) @ Matrix.Rotation(-a, 4, "Y")
        box("Paint", m.to_translation(), (0.06, 0.025, 0.34), rot=m.to_euler(), color=(RED, YELLOW, TEAL, ORANGE, TEAL, YELLOW, RED)[k], bevel=0.008)


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
        gb.bake_vertex_ao(o, samples=24, distance=0.5)
    gb.export_fbx_objects(os.path.join(OUT, f"{name}.fbx"), out)
    print(f"[hero_tiki] {name}: " + ", ".join(f"{o.name}={len(o.data.polygons)}" for o in out))


reset()
pole(3, 0.62, 0.2, [TEAL, ORANGE], wings=True, crown=True)
finish("HeroTikiPole_0")
reset()
pole(2, 0.62, 0.19, [ORANGE, RED], wings=False, crown=False)
finish("HeroTikiPole_1")
reset()
mask()
finish("HeroTikiMask_0")
print("[hero_tiki] done")
