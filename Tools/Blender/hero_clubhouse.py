"""Hero tiki clubhouse for Tropical Adventure (Blender, Z-up; exported Y-up for Unity).

  blender -b --factory-startup --python Tools/Blender/hero_clubhouse.py -- <out_dir>

Front faces -Y in Blender (Unity: -Z), deck centred on the origin, ground at z = 0.
Objects are named by material suffix: __Wood, __Thatch, __Bamboo, __Totem, __Lantern, __Paint.
UVs are world-scale box/cylinder projections so the tiling textures keep a consistent texel density.
Vertex colour RGB = paint x baked AO; A = wind weight.
"""
import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector, noise

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gblib as gb  # noqa: E402

OUT = gb.out_dir("Assets/_Game/Art/Generated/Models")
gb.reset_scene()  # Cycles, needed for the AO bake
rnd = random.Random(42)

DECK_W, DECK_D, DECK_Z = 4.6, 3.6, 0.75
WOOD_TILE, THATCH_TILE, BAMBOO_TILE = 1.6, 1.4, 0.9

parts = {k: [] for k in ("Wood", "Thatch", "Bamboo", "Totem", "Lantern", "Paint")}


def box(group, center, size, rot=(0, 0, 0), color=(1, 1, 1, 1)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    gb.ensure_color_attr(o, color)
    parts[group].append(o)
    return o


def cylinder(group, base, radius_fn, height, sides=16, rings=8, tile=1.0, axis=None, color_fn=None, cap=True):
    """Vertical (or axis-aligned) cylinder; radius_fn(theta, t) in metres; UVs: u around (metres/tile), v along."""
    verts, faces, uvs, cols = [], [], [], []
    axis = Vector(axis or (0, 0, 1)).normalized()
    ref = Vector((1, 0, 0)) if abs(axis.x) < 0.9 else Vector((0, 1, 0))
    e1 = (ref - axis * ref.dot(axis)).normalized()
    e2 = axis.cross(e1)
    for j in range(rings + 1):
        t = j / rings
        for i in range(sides + 1):
            th = math.tau * i / sides
            r = radius_fn(th, t)
            verts.append(tuple(Vector(base) + axis * (t * height) + (e1 * math.cos(th) + e2 * math.sin(th)) * r))
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
        verts.append(tuple(Vector(base) + axis * height))
        last = rings * (sides + 1)
        for i in range(sides):
            faces.append((last + i, last + i + 1, c))
            for k in (0, 1, 2):
                uvs.append((0.5, 0.5))
                cols.append(color_fn(0, 1) if color_fn else (1, 1, 1, 0))
    o = gb.mesh_object("cyl", verts, faces, uvs, cols)
    parts[group].append(o)
    return o


def bamboo_pole(a, b, r=0.045):
    a, b = Vector(a), Vector(b)
    L = (b - a).length
    nodes = max(1, int(L / 0.45))
    def rad(th, t):
        f = (t * nodes) % 1.0
        return r * (1.0 + 0.18 * math.exp(-((f - 0.0) / 0.05) ** 2) + 0.18 * math.exp(-((f - 1.0) / 0.05) ** 2))
    cylinder("Bamboo", a, rad, L, sides=10, rings=nodes * 6, tile=BAMBOO_TILE, axis=b - a)


# ------------------------------------------------------------------ deck and frame
for i in range(int(DECK_D / 0.2)):
    y = -DECK_D / 2 + 0.1 + i * 0.2
    box("Wood", (rnd.uniform(-0.03, 0.03), y, DECK_Z - 0.03 + rnd.uniform(-0.008, 0.008)), (DECK_W, 0.18, 0.06),
        rot=(rnd.uniform(-0.01, 0.01), 0, rnd.uniform(-0.006, 0.006)))
for y in (-DECK_D / 2, DECK_D / 2):
    box("Wood", (0, y, DECK_Z - 0.14), (DECK_W + 0.1, 0.16, 0.2))
for x in (-DECK_W / 2, DECK_W / 2):
    box("Wood", (x, 0, DECK_Z - 0.14), (0.16, DECK_D + 0.1, 0.2))

# Back and middle stilts (front corners are the totems).
post_h = 3.35
for (x, y) in [(-DECK_W / 2 + 0.1, DECK_D / 2 - 0.1), (DECK_W / 2 - 0.1, DECK_D / 2 - 0.1), (-DECK_W / 2 + 0.1, 0), (DECK_W / 2 - 0.1, 0)]:
    cylinder("Wood", (x, y, 0), lambda th, t: 0.12 + 0.015 * math.sin(th * 3 + t * 9), post_h, sides=12, rings=10, tile=WOOD_TILE)

# ------------------------------------------------------------------ tiki totems
def totem(x, y, face_dir):
    tier_h = 0.95
    tiers = 3
    height = tier_h * tiers + 0.45
    base_r = 0.23
    fwd = math.atan2(face_dir[1], face_dir[0])

    def features(th, t):
        z = t * height
        if z > tier_h * tiers:
            return 0.0, (1, 1, 1, 0)
        f = (z % tier_h) / tier_h
        phi = math.atan2(math.sin(th - fwd), math.cos(th - fwd))  # 0 = facing out
        d, col = 0.0, (1, 1, 1, 0)
        if f < 0.04 or f > 0.97:
            return -0.03, (0.7, 0.6, 0.5, 0)  # groove between faces
        if abs(phi) < 1.25:
            # Brow ridge.
            d += 0.045 * math.exp(-((f - 0.74) / 0.05) ** 2) * (1 - (phi / 1.25) ** 2)
            # Eyes (recessed, painted white with dark pupils).
            for side in (-1, 1):
                de = math.hypot((phi - side * 0.38) / 0.2, (f - 0.6) / 0.085)
                if de < 1.0:
                    d -= 0.05 * (1 - de ** 2)
                    col = (0.08, 0.06, 0.05, 0) if de < 0.4 else (0.98, 0.95, 0.85, 0)
            # Nose.
            d += 0.06 * math.exp(-(phi / 0.13) ** 2) * math.exp(-((f - 0.45) / 0.1) ** 2)
            # Wide grinning mouth with teeth.
            dm = math.hypot(phi / 0.62, (f - 0.24) / 0.07)
            if dm < 1.0:
                d -= 0.06 * (1 - dm ** 2)
                teeth = abs(f - 0.24) < 0.03 and (math.sin(phi * 30) > 0)
                col = (0.98, 0.96, 0.88, 0) if teeth else (0.55, 0.08, 0.06, 0)
        # Painted band at the top of each tier.
        if 0.86 < f < 0.94:
            col = ((0.10, 0.62, 0.66, 0) if int(z / tier_h) % 2 == 0 else (0.95, 0.55, 0.12, 0))
        return d, col

    def rad(th, t):
        return base_r * (1.0 if t * height < tier_h * tiers else 0.75) + features(th, t)[0]

    cylinder("Totem", (x, y, 0), rad, height, sides=48, rings=90, tile=WOOD_TILE,
             color_fn=lambda th, t: features(th, t)[1])


totem(-DECK_W / 2 + 0.1, -DECK_D / 2 + 0.1, (0, -1))
totem(DECK_W / 2 - 0.1, -DECK_D / 2 + 0.1, (0, -1))

# ------------------------------------------------------------------ roof: three thatched tiers with ragged fringe
def thatch_tier(rx, ry, z0, z1, ragged, seed):
    sides, rings = 48, 6
    verts, faces, uvs, cols = [], [], [], []
    slant = math.hypot(max(rx, ry), z1 - z0)
    for j in range(rings + 1):
        t = j / rings
        for i in range(sides + 1):
            th = math.tau * i / sides
            k = 1 - t
            rr = 1 + (noise.noise(Vector((math.cos(th) * 3, math.sin(th) * 3, seed))) * 0.06 if j == 0 else 0)
            x, y = math.cos(th) * rx * k * rr, math.sin(th) * ry * k * rr
            z = z0 + (z1 - z0) * t ** 0.85
            if j == 0:
                z -= ragged * (0.5 + 0.5 * math.sin(th * 23 + seed)) * (0.6 + 0.4 * math.sin(th * 7))
            verts.append((x, y, z))
    for j in range(rings):
        for i in range(sides):
            a = j * (sides + 1) + i
            faces.append((a, a + 1, a + sides + 2, a + sides + 1))
            for (ii, jj) in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                th = math.tau * ii / sides
                uvs.append((th * max(rx, ry) / THATCH_TILE, jj / rings * slant / THATCH_TILE))
                cols.append((1, 1, 1, 0.04 if jj == 0 else 0.0))
    o = gb.mesh_object("thatch", verts, faces, uvs, cols)
    # Thickness so it reads solid from below.
    sol = o.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = 0.12
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier="Solid")
    parts["Thatch"].append(o)


thatch_tier(3.3, 2.75, 3.05, 4.25, 0.22, 1.0)
thatch_tier(2.3, 1.95, 3.85, 4.95, 0.18, 2.0)
thatch_tier(1.35, 1.15, 4.6, 5.6, 0.14, 3.0)
bamboo_pole((0, 0, 5.3), (0, 0, 6.1), 0.05)

# ------------------------------------------------------------------ bar counter, sign, railings, steps, stools
bar_y = -DECK_D / 2 + 0.25
for i in range(14):
    x = -1.4 + i * 0.215
    box("Wood", (x, bar_y - 0.02, DECK_Z + 0.5), (0.2, 0.06, 1.0), rot=(0, rnd.uniform(-0.02, 0.02), 0))
box("Wood", (0, bar_y + 0.05, DECK_Z + 1.04), (3.2, 0.5, 0.08))
bamboo_pole((-1.6, bar_y - 0.08, DECK_Z + 0.98), (1.6, bar_y - 0.08, DECK_Z + 0.98), 0.05)
bamboo_pole((-1.6, bar_y - 0.08, DECK_Z + 0.1), (1.6, bar_y - 0.08, DECK_Z + 0.1), 0.045)

# Sign board between the totems with raised 3D lettering.
sign_z = 2.75
box("Wood", (0, -DECK_D / 2 + 0.05, sign_z), (2.6, 0.08, 0.55), color=(0.75, 0.62, 0.5, 0))
curve = bpy.data.curves.new("SignText", type="FONT")
curve.body = "TIKI CLUB"
curve.size = 0.36
curve.extrude = 0.025
curve.align_x = "CENTER"
curve.align_y = "CENTER"
txt = bpy.data.objects.new("SignText", curve)
bpy.context.scene.collection.objects.link(txt)
txt.location = (0, -DECK_D / 2 - 0.02, sign_z)
txt.rotation_euler = (math.pi / 2, 0, 0)
bpy.context.view_layer.objects.active = txt
txt.select_set(True)
bpy.ops.object.convert(target="MESH")
gb.ensure_color_attr(txt, (1.0, 0.85, 0.35, 0))
parts["Paint"].append(txt)

for (a, b) in [((-DECK_W / 2 + 0.1, -0.2, DECK_Z), (-DECK_W / 2 + 0.1, DECK_D / 2 - 0.1, DECK_Z)),
               ((DECK_W / 2 - 0.1, DECK_D / 2 - 0.1, DECK_Z), (-DECK_W / 2 + 0.1, DECK_D / 2 - 0.1, DECK_Z))]:
    for h in (0.45, 0.9):
        bamboo_pole((a[0], a[1], a[2] + h), (b[0], b[1], b[2] + h))
for i in range(3):
    box("Wood", (DECK_W / 2 + 0.35 + i * 0.3, -0.6, DECK_Z * (3 - i) / 3 - 0.06), (0.32, 1.0, 0.08))
for i, x in enumerate((-1.0, 0.0, 1.0)):
    sx, sy = x, -DECK_D / 2 - 0.75
    for k in range(3):
        a = k * math.tau / 3
        bamboo_pole((sx + math.cos(a) * 0.14, sy + math.sin(a) * 0.14, 0), (sx + math.cos(a) * 0.1, sy + math.sin(a) * 0.1, 0.72), 0.025)
    cylinder("Wood", (sx, sy, 0.72), lambda th, t: 0.2, 0.06, sides=16, rings=1, tile=WOOD_TILE)

# ------------------------------------------------------------------ lanterns and surfboards
for x in (-1.5, 0.0, 1.5):
    top = Vector((x, -DECK_D / 2 - 0.35, 3.05))
    bamboo_pole(top, top + Vector((0, 0, -0.35)), 0.012)
    cylinder("Lantern", top + Vector((0, 0, -0.62)), lambda th, t: 0.13 * math.sin(math.pi * (0.15 + t * 0.7)) ** 0.5, 0.3,
             sides=12, rings=6, tile=0.5, color_fn=lambda th, t: (1, 0.75, 0.35, 0.3))


def surfboard(center, angle, colors):
    sides, rings = 16, 24
    verts, faces, cols = [], [], []
    L, Wd, T = 2.0, 0.28, 0.035
    for j in range(rings + 1):
        t = j / rings
        w = Wd * math.sin(math.pi * min(1, 0.05 + t * 0.95)) ** 0.7
        for i in range(sides + 1):
            th = math.tau * i / sides
            verts.append((math.cos(th) * w, math.sin(th) * T, t * L))
    for j in range(rings):
        for i in range(sides):
            a = j * (sides + 1) + i
            faces.append((a, a + 1, a + sides + 2, a + sides + 1))
            for jj in (j, j, j + 1, j + 1):
                tt = jj / rings
                cols.append(colors[0] if abs(tt - 0.5) > 0.12 else colors[1])
    o = gb.mesh_object("surf", verts, faces, None, cols)
    o.location = center
    o.rotation_euler = (math.radians(-12), 0, angle)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    parts["Paint"].append(o)


surfboard((-DECK_W / 2 - 0.25, -0.8, 0.0), math.radians(-80), [(0.1, 0.65, 0.75, 0), (1.0, 0.85, 0.3, 0)])
surfboard((-DECK_W / 2 - 0.3, -0.2, 0.0), math.radians(-95), [(1.0, 0.45, 0.25, 0), (1.0, 1.0, 0.95, 0)])

# ------------------------------------------------------------------ join per material, UVs, AO, export
out_objs = []
for group, objs in parts.items():
    if not objs:
        continue
    o = gb.join(objs, f"TikiClubhouse__{group}")
    if group in ("Wood",):
        gb.box_uv(o, WOOD_TILE)
    out_objs.append(o)
for o in out_objs:
    gb.bake_vertex_ao(o, samples=32, distance=1.2)
gb.export_fbx_objects(os.path.join(OUT, "HeroTikiClubhouse.fbx"), out_objs)
print("[hero_clubhouse] done:", ", ".join(f"{o.name}={len(o.data.polygons)}" for o in out_objs))
