"""Bake the Tropical Adventure tileable PBR texture sets with Blender (Cycles, EMIT bakes of procedural
node graphs mapped onto a 4D torus so every texture tiles seamlessly).

  blender -b --factory-startup --python Tools/Blender/bake_textures.py -- <out_dir> [size]

Outputs per material: <name>_albedo.png, _normal.png, _height.png, _mask.png (G = occlusion, A = smoothness).
"""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gblib as gb  # noqa: E402
from gblib import srgb  # noqa: E402

OUT = gb.out_dir("Assets/_Game/Art/Generated/Textures")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SIZE = int(argv[1]) if len(argv) > 1 else 1024


def sand(g):
    u, v = g.uv()
    vec, w = g.torus(u, v, 1.0, 1.0)
    warp = g.noise(vec, w, scale=1.1, detail=2)
    ripple = g.add(g.mul(g.sin(g.mul(g.add(g.mul(v, 9.0), g.mul(warp, 1.4)), gb.TAU)), 0.5), 0.5)
    ripple = g.pow(ripple, 1.6)
    fine = g.noise(vec, w, scale=22.0, detail=7, rough=0.6)
    vec2, w2 = g.torus(u, v, 1.0, 1.0, offset=(3.1, 1.7, 2.2, 5.3))
    grains = g.voronoi(vec2, w2, scale=55.0)
    bumps = g.sub(1.0, g.smoothstep(0.0, 0.18, grains))
    height = g.add(g.add(g.mul(ripple, 0.45), g.mul(fine, 0.38)), g.mul(bumps, 0.17))
    tint = g.add(g.mul(fine, 0.7), g.mul(ripple, 0.3))
    albedo = g.ramp(tint, [(0.15, srgb(0.82, 0.64, 0.40)), (0.55, srgb(0.95, 0.82, 0.58)), (0.9, srgb(1.0, 0.92, 0.72))])
    speck = g.smoothstep(0.035, 0.0, grains)
    albedo = g.mix_color(g.mul(speck, 0.6), albedo, srgb(0.62, 0.48, 0.34))
    return dict(albedo=albedo, height=height, rough=g.val(0.85), normal_strength=3.0)


def lawn(g):
    u, v = g.uv()
    vec, w = g.torus(u, v, 1.0, 1.0)
    clumps = g.noise(vec, w, scale=2.6, detail=3, rough=0.5)
    vec2, w2 = g.torus(u, v, 1.0, 1.0, offset=(5.0, 2.0, 1.0, 4.0))
    tufts = g.sub(1.0, g.smoothstep(0.0, 0.5, g.voronoi(vec2, w2, scale=70.0)))
    blades = g.noise(vec2, w2, scale=95.0, detail=3, rough=0.7)
    height = g.add(g.mul(tufts, 0.45), g.add(g.mul(blades, 0.35), g.mul(clumps, 0.2)))
    dry = g.smoothstep(0.62, 0.78, g.noise(vec, w, scale=1.3, detail=2))
    shade = g.add(g.mul(clumps, 0.55), g.mul(height, 0.45))
    albedo = g.ramp(shade, [(0.2, srgb(0.10, 0.34, 0.07)), (0.5, srgb(0.27, 0.58, 0.12)), (0.85, srgb(0.56, 0.82, 0.22))])
    albedo = g.mix_color(g.mul(dry, 0.45), albedo, srgb(0.72, 0.74, 0.32))
    return dict(albedo=albedo, height=height, rough=g.val(0.78), normal_strength=2.5)


def sandstone(g):
    u, v = g.uv()
    vec, w = g.torus(u, v, 1.0, 1.0)
    warp = g.noise(vec, w, scale=0.9, detail=3)
    strata = g.add(g.mul(g.sin(g.mul(g.add(g.mul(v, 6.0), g.mul(warp, 0.9)), gb.TAU)), 0.5), 0.5)
    strata_sharp = g.smoothstep(0.35, 0.65, strata)
    detail = g.noise(vec, w, scale=9.0, detail=9, rough=0.62)
    vec2, w2 = g.torus(u, v, 1.0, 1.0, offset=(7.0, 3.0, 2.0, 1.0))
    # A few long fractures across the strata, not a mud-crack network.
    crack_field = g.noise(vec2, w2, scale=1.3, detail=3, rough=0.5)
    crack_line = g.smoothstep(0.0, 0.018, g.math("ABSOLUTE", g.sub(crack_field, 0.5)))
    crack_mask = g.smoothstep(0.55, 0.7, g.noise(vec, w, scale=0.8, detail=2))
    cracks = g.sub(1.0, g.mul(g.sub(1.0, crack_line), crack_mask))
    pits = g.smoothstep(0.0, 0.25, g.voronoi(vec, w, scale=28.0))
    height = g.mul(g.add(g.add(g.mul(strata_sharp, 0.35), g.mul(detail, 0.45)), g.mul(pits, 0.2)), g.add(g.mul(cracks, 0.6), 0.4))
    tone = g.add(g.mul(strata, 0.55), g.mul(detail, 0.45))
    albedo = g.ramp(tone, [(0.1, srgb(0.50, 0.25, 0.14)), (0.4, srgb(0.80, 0.48, 0.27)), (0.7, srgb(0.93, 0.70, 0.46)), (0.95, srgb(0.99, 0.86, 0.66))])
    albedo = g.mix_color(g.sub(1.0, cracks), albedo, srgb(0.35, 0.18, 0.10))
    return dict(albedo=albedo, height=height, rough=g.val(0.82), normal_strength=5.0, cavity_strength=1.3)


def path(g):
    u, v = g.uv()
    vec, w = g.torus(u, v, 1.0, 1.0)
    pebble_d = g.voronoi(vec, w, scale=6.0)
    pebbles = g.sub(1.0, g.smoothstep(0.3, 0.55, pebble_d))
    dirt = g.noise(vec, w, scale=14.0, detail=6)
    height = g.add(g.mul(pebbles, 0.6), g.mul(dirt, 0.4))
    vec2, w2 = g.torus(u, v, 1.0, 1.0, offset=(2.0, 9.0, 4.0, 1.0))
    pebble_tone = g.ramp(g.noise(vec2, w2, scale=20.0, detail=2), [(0.2, srgb(0.50, 0.44, 0.36)), (0.8, srgb(0.80, 0.72, 0.60))])
    albedo = g.ramp(dirt, [(0.2, srgb(0.42, 0.28, 0.16)), (0.8, srgb(0.70, 0.52, 0.32))])
    stone = pebble_tone
    albedo = g.mix_color(g.smoothstep(0.3, 0.6, pebbles), albedo, stone)
    return dict(albedo=albedo, height=height, rough=g.val(0.8), normal_strength=4.0)


def wood(g):
    u, v = g.uv()
    planks = 5.0
    vq = g.mul(g.math("FLOOR", g.mul(v, planks)), 1.0 / planks)
    seam_f = g.math("FRACT", g.mul(v, planks))
    seam = g.mul(g.smoothstep(0.0, 0.06, seam_f), g.smoothstep(1.0, 0.94, seam_f))
    vec, w = g.torus(u, v, 0.5, 9.0)
    grain = g.noise(vec, w, scale=1.4, detail=6, rough=0.55, distortion=0.3)
    vecq, wq = g.torus(u, vq, 0.2, 2.0)
    plank_tint = g.noise(vecq, wq, scale=3.0, detail=1)
    knots = g.smoothstep(0.08, 0.0, g.voronoi(*g.torus(u, v, 2.0, 2.0, offset=(1, 2, 3, 4)), scale=1.6))
    height = g.mul(g.add(g.mul(grain, 0.5), 0.5), g.add(g.mul(seam, 0.8), 0.2))
    tone = g.add(g.mul(grain, 0.6), g.mul(plank_tint, 0.4))
    albedo = g.ramp(tone, [(0.2, srgb(0.40, 0.22, 0.10)), (0.55, srgb(0.66, 0.42, 0.22)), (0.85, srgb(0.85, 0.62, 0.38))])
    albedo = g.mix_color(g.mul(knots, 0.7), albedo, srgb(0.30, 0.16, 0.08))
    albedo = g.mix_color(g.sub(1.0, seam), albedo, srgb(0.18, 0.10, 0.05))
    return dict(albedo=albedo, height=height, rough=g.val(0.62), normal_strength=4.0)


def thatch(g):
    u, v = g.uv()
    rows = 4.0
    rf = g.math("FRACT", g.mul(v, rows))
    row_shade = g.add(g.mul(g.smoothstep(0.0, 0.3, rf), 0.55), 0.45)
    vec, w = g.torus(u, v, 14.0, 0.7)
    strands = g.noise(vec, w, scale=1.6, detail=7, rough=0.65)
    vec2, w2 = g.torus(u, v, 30.0, 1.5, offset=(3, 1, 2, 5))
    fine = g.noise(vec2, w2, scale=1.0, detail=4)
    height = g.mul(g.add(g.mul(strands, 0.6), g.mul(fine, 0.4)), row_shade)
    albedo = g.ramp(g.add(g.mul(strands, 0.7), g.mul(fine, 0.3)), [(0.25, srgb(0.48, 0.33, 0.14)), (0.6, srgb(0.78, 0.60, 0.30)), (0.9, srgb(0.96, 0.83, 0.52))])
    albedo = g.multiply_color(albedo, row_shade)
    return dict(albedo=albedo, height=height, rough=g.val(0.85), normal_strength=5.0)


def bark(g):
    u, v = g.uv()
    rings = 6.0
    f = g.math("FRACT", g.mul(v, rings))
    ridge = g.mul(g.smoothstep(0.0, 0.2, f), g.smoothstep(1.0, 0.55, f))
    vec, w = g.torus(u, v, 7.0, 0.8)
    fibres = g.noise(vec, w, scale=1.5, detail=6, rough=0.6)
    height = g.add(g.mul(ridge, 0.6), g.mul(fibres, 0.4))
    albedo = g.ramp(height, [(0.15, srgb(0.24, 0.15, 0.09)), (0.5, srgb(0.50, 0.35, 0.21)), (0.9, srgb(0.74, 0.58, 0.38))])
    return dict(albedo=albedo, height=height, rough=g.val(0.8), normal_strength=6.0)


def bamboo(g):
    u, v = g.uv()
    f = g.math("FRACT", g.mul(v, 2.0))
    node = g.mul(g.smoothstep(0.0, 0.04, f), g.smoothstep(1.0, 0.96, f))
    vec, w = g.torus(u, v, 5.0, 0.6)
    streak = g.noise(vec, w, scale=1.3, detail=4)
    height = g.add(g.mul(node, 0.7), g.mul(streak, 0.3))
    albedo = g.ramp(streak, [(0.2, srgb(0.62, 0.58, 0.24)), (0.8, srgb(0.86, 0.80, 0.42))])
    albedo = g.mix_color(g.sub(1.0, node), albedo, srgb(0.42, 0.34, 0.14))
    return dict(albedo=albedo, height=height, rough=g.val(0.45), normal_strength=4.0)


def turf(g):
    u, v = g.uv()
    vec, w = g.torus(u, v, 1.0, 1.0)
    fibres = g.noise(vec, w, scale=120.0, detail=2, rough=0.7)
    wear = g.noise(vec, w, scale=3.0, detail=3)
    height = g.add(g.mul(fibres, 0.8), g.mul(wear, 0.2))
    albedo = g.ramp(g.add(g.mul(fibres, 0.5), g.mul(wear, 0.5)), [(0.2, srgb(0.13, 0.42, 0.12)), (0.8, srgb(0.27, 0.64, 0.20))])
    return dict(albedo=albedo, height=height, rough=g.val(0.9), normal_strength=1.5)


RECIPES = dict(sand=sand, lawn=lawn, sandstone=sandstone, path=path, wood=wood, thatch=thatch, bark=bark, bamboo=bamboo, turf=turf)

only = argv[2].split(",") if len(argv) > 2 else None
for name, fn in RECIPES.items():
    if only and name not in only:
        continue
    gb.bake_plane(name, SIZE, fn, OUT)
print("[bake_textures] done")
