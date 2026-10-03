"""Hero sandstone formations for Tropical Adventure (Blender, Z-up; exported Y-up for Unity).

  blender -b --factory-startup --python Tools/Blender/hero_rocks.py -- <out_dir>

Sculpted procedurally: dense meshes shaped by tiered profiles + layered noise (strata ledges, erosion),
then ambient occlusion baked into vertex colours. Textured in Unity by Gamebreak/RockTriplanar
(no UVs needed). Objects are named <Asset>__Rock.
"""
import math
import os
import random
import sys

from mathutils import Vector, noise

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gblib as gb  # noqa: E402

OUT = gb.out_dir("Assets/_Game/Art/Generated/Models")


def fbm(p, octaves=5, seed=0.0):
    return noise.fractal(p + Vector((seed, seed * 1.7, seed * 0.3)), 0.6, 2.1, octaves, noise_basis="PERLIN_ORIGINAL")


def strata(z, tier_h, lip=0.12):
    """Radius multiplier for stacked layers: each tier bulges just below its top, with a recessed seam."""
    f = (z / tier_h) % 1.0
    ledge = math.exp(-((f - 0.82) / 0.09) ** 2) * lip
    seam = -math.exp(-((f - 0.02) / 0.04) ** 2) * lip * 0.8
    return 1.0 + ledge + seam


def revolve(name, profile_r, height, ang_segs, z_segs, seed, warp_amp=0.18, tier_h=1.1, lean=(0, 0)):
    """Closed surface of revolution r(theta, z) with noise; z from 0 to height, capped top/bottom."""
    verts, faces = [], []
    for j in range(z_segs + 1):
        z = height * j / z_segs
        t = j / z_segs
        for i in range(ang_segs):
            th = math.tau * i / ang_segs
            d = Vector((math.cos(th), math.sin(th), 0))
            n = fbm(Vector((d.x * 1.3, d.y * 1.3, z * 0.55)), 5, seed) * warp_amp
            r = profile_r(t, th) * strata(z, tier_h) * (1 + n)
            off = Vector((lean[0], lean[1], 0)) * (t ** 1.5)
            verts.append(tuple(d * r + off + Vector((0, 0, z))))
    for j in range(z_segs):
        for i in range(ang_segs):
            a = j * ang_segs + i
            b = j * ang_segs + (i + 1) % ang_segs
            faces.append((a, b, b + ang_segs, a + ang_segs))
    # Caps: a slightly domed, noisy top; flat bottom.
    top_c = len(verts)
    topz = height + 0.15
    verts.append((lean[0], lean[1], topz))
    last = z_segs * ang_segs
    for i in range(ang_segs):
        faces.append((last + i, last + (i + 1) % ang_segs, top_c))
    bot_c = len(verts)
    verts.append((0, 0, -0.05))
    for i in range(ang_segs):
        faces.append(((i + 1) % ang_segs, i, bot_c))
    return gb.mesh_object(name, verts, faces, smooth=True)


def finish(obj, name, target_faces, ao_distance):
    # Extra small-scale erosion via a displace modifier with a cloud texture, then decimate to budget.
    tex = bpy.data.textures.new(name + "_erosion", type="CLOUDS")
    tex.noise_scale = 0.35
    tex.noise_depth = 3
    sub = obj.modifiers.new("Sub", "SUBSURF")
    sub.levels = 1
    disp = obj.modifiers.new("Erode", "DISPLACE")
    disp.texture = tex
    disp.strength = 0.12
    disp.mid_level = 0.5
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier="Sub")
    bpy.ops.object.modifier_apply(modifier="Erode")
    faces = len(obj.data.polygons)
    if faces > target_faces:
        gb.decimate(obj, target_faces / faces)
    for p in obj.data.polygons:
        p.use_smooth = True
    obj.name = f"{name}__Rock"
    gb.ensure_color_attr(obj, (1, 1, 1, 0))
    gb.bake_vertex_ao(obj, samples=48, distance=ao_distance)
    gb.export_fbx_objects(os.path.join(OUT, f"{name}.fbx"), [obj])
    print(f"[hero_rocks] {name}: {len(obj.data.polygons)} faces")


import bpy  # noqa: E402  (after gblib sets up)


def mesa(name, height, radius, seed):
    gb.reset_scene()
    rnd = random.Random(seed)
    tiers = [rnd.uniform(0.85, 1.05) for _ in range(8)]

    def prof(t, th):
        # Narrower toward the top with tier-wise random setbacks, flared base.
        k = int(t * 6)
        setback = tiers[k % len(tiers)]
        base_flare = 1.0 + 0.35 * max(0.0, 0.12 - t) / 0.12
        return radius * (1.0 - 0.38 * t) * setback * base_flare * (1 + 0.12 * math.sin(th * 3 + seed))

    obj = revolve(name, prof, height, 96, 110, seed, warp_amp=0.16, tier_h=height / 6.0,
                  lean=(rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4)))
    finish(obj, name, 14000, 3.0)


def boulders(name, count, spread, seed):
    gb.reset_scene()
    rnd = random.Random(seed)
    parts = []
    for i in range(count):
        r = rnd.uniform(0.5, 1.3) * (1.5 if i == 0 else 1.0)
        c = Vector((rnd.uniform(-spread, spread), rnd.uniform(-spread, spread), 0)) if i else Vector((0, 0, 0))
        h = r * rnd.uniform(1.0, 1.6)
        o = revolve(f"b{i}", lambda t, th, r=r: r * math.sin(math.pi * min(1.0, 0.25 + t * 0.85)) ** 0.6 * (1 + 0.2 * math.sin(th * 2 + i)),
                    h, 48, 32, seed + i * 3, warp_amp=0.25, tier_h=h / 2.5)
        o.location = c
        o.rotation_euler = (0, 0, rnd.uniform(0, math.tau))
        parts.append(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj = gb.join(parts, name)
    finish(obj, name, 9000, 1.5)


def sea_arch(name, span, height, seed):
    """Arch: a thick tube swept along a half-ellipse, legs sunk below the waterline, plus a cap ridge."""
    gb.reset_scene()
    verts, faces = [], []
    steps, sides = 90, 40
    path = []
    for i in range(steps + 1):
        a = math.pi * i / steps
        path.append(Vector((math.cos(a) * span * 0.5, 0.0, math.sin(a) * height - 1.2)))
    for i, p in enumerate(path):
        t = i / steps
        tan = (path[min(i + 1, steps)] - path[max(i - 1, 0)]).normalized()
        nrm = Vector((0, 1, 0))
        bin_ = tan.cross(nrm).normalized()
        # Thick legs, thinner crown.
        thick = 1.35 - 0.55 * math.sin(math.pi * t)
        for s in range(sides):
            th = math.tau * s / sides
            d = nrm * math.cos(th) * 1.25 + bin_ * math.sin(th)
            n = fbm(p * 0.45 + d, 5, seed) * 0.35
            r = thick * strata(p.z + 2.0, 0.9, 0.1) * (1 + n)
            verts.append(tuple(p + d * r))
    for i in range(steps):
        for s in range(sides):
            a = i * sides + s
            b = i * sides + (s + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    obj = gb.mesh_object(name, verts, faces, smooth=True)
    finish(obj, name, 14000, 3.0)


def cliff_wall(name, length, height, depth, seed):
    """Long terraced cliff face: a displaced, curved slab (front face detailed, back simple)."""
    gb.reset_scene()
    nx, nz = 120, 60
    verts, faces = [], []
    for j in range(nz + 1):
        z = height * j / nz
        for i in range(nx + 1):
            x = (i / nx - 0.5) * length
            curve = -0.04 * x * x
            n = fbm(Vector((x * 0.25, z * 0.35, 0)), 6, seed)
            terr = (strata(z, height / 5.0, 0.6) - 1.0) * depth
            y = curve + n * depth * 0.9 + terr + (z / height) ** 2 * depth * 0.8
            taper = min(1.0, (0.5 * length - abs(x)) / 2.5)
            verts.append((x, y * max(0.2, taper), z * max(0.0, min(1.0, taper * 1.4))))
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + nx + 2, a + nx + 1))
    # Back and top slab so it is solid from behind.
    back = len(verts)
    for (x, y, z) in [(-length / 2, depth * 3, 0), (length / 2, depth * 3, 0), (length / 2, depth * 3, height), (-length / 2, depth * 3, height)]:
        verts.append((x, y, z))
    faces.append((back, back + 3, back + 2, back + 1))
    obj = gb.mesh_object(name, verts, faces, smooth=True)
    finish(obj, name, 16000, 3.0)


mesa("HeroMesa_0", 9.0, 2.6, 5)
mesa("HeroMesa_1", 6.5, 2.1, 9)
sea_arch("HeroSeaArch", 7.0, 5.2, 13)
cliff_wall("HeroCliffWall", 16.0, 7.0, 1.4, 17)
boulders("HeroBoulders_0", 4, 1.6, 21)
boulders("HeroBoulders_1", 3, 1.2, 29)
print("[hero_rocks] done")
