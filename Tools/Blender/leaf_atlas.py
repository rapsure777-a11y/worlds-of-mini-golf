"""Draw the tropical foliage atlas (albedo + alpha, normal) with numpy inside Blender.

  blender -b --factory-startup --python Tools/Blender/leaf_atlas.py -- <out_dir>

Layout (2048 x 2048, origin bottom-left, cells listed as x, y, w, h in pixels) is mirrored in Unity by
Gamebreak.MiniGolf.Editor.Art.LeafAtlas: keep the two in sync.
"""
import math
import os
import sys

import numpy as np

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gblib as gb  # noqa: E402

OUT = gb.out_dir("Assets/_Game/Art/Generated/Textures")
S = 2048
SS = 2  # supersampling for anti-aliased edges

CELLS = {
    "palm_frond":   (0, 0, 512, 1024),
    "palm_dry":     (0, 1024, 512, 1024),
    "broadleaf":    (512, 0, 512, 512),
    "monstera":     (1024, 0, 512, 512),
    "fern":         (1536, 0, 512, 512),
    "bush_a":       (512, 512, 512, 512),
    "bush_b":       (1024, 512, 512, 512),
    "grass":        (1536, 512, 512, 512),
    "hibiscus":     (512, 1024, 512, 512),
    "plumeria":     (1024, 1024, 512, 512),
    "flowers_purple": (1536, 1024, 512, 512),
    "banana":       (512, 1536, 512, 512),
    "ivy":          (1024, 1536, 512, 512),
    "bush_c":       (1536, 1536, 512, 512),
}

W = S * SS
albedo = np.zeros((W, W, 3), np.float32)
alpha = np.zeros((W, W), np.float32)
height = np.zeros((W, W), np.float32)
rng = np.random.default_rng(7)


def lin(c):
    return np.array(gb.srgb(*c)[:3], np.float32)


def leaf(cx, cy, angle, length, width, prof, c_base, c_tip, vein=0.25, lateral=14, holes=None, split=0.0,
         edge_dark=0.25, curl=0.0, seed=0):
    """Rasterise one leaf. (cx, cy) base in supersampled pixels, angle radians (0 = up)."""
    ca, sa = math.cos(angle), math.sin(angle)
    r = length * 1.05 + width
    x0, x1 = int(max(0, cx - r)), int(min(W, cx + r))
    y0, y1 = int(max(0, cy - r)), int(min(W, cy + r))
    if x1 <= x0 or y1 <= y0:
        return
    ys, xs = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    dx, dy = xs - cx, ys - cy
    # Leaf-local coordinates: t along the midrib (0 base .. 1 tip), s across (-1..1 of local half-width).
    along = dx * -sa + dy * ca
    across = dx * ca + dy * sa
    t = along / length
    across = across - curl * (t ** 2) * width
    halfw = np.nan_to_num(width * prof(np.clip(t, 0, 1))) + 1e-3
    s = across / halfw
    inside = (t >= 0) & (t <= 1) & (np.abs(s) <= 1)
    # Soft edge for anti-aliasing (in supersampled pixels).
    edge = np.clip((1 - np.abs(s)) * halfw / 1.5, 0, 1) * np.clip(t * length / 1.5, 0, 1) * np.clip((1 - t) * length / 1.5, 0, 1)
    a = np.where(inside, edge, 0.0)
    if split > 0:
        # Monstera-style splits: cut slots from the edge inward along lateral directions.
        phase = (t * lateral + np.abs(s) * 0.6) % 1.0
        slot = (phase < split) & (np.abs(s) > 0.45) & (t > 0.15) & (t < 0.92)
        a = np.where(slot, 0.0, a)
    if holes:
        for (ht, hs, hr) in holes:
            d = np.sqrt(((t - ht) * length) ** 2 + ((s - hs) * halfw) ** 2)
            a = np.where(d < hr * width, 0.0, a)
    if not np.any(a > 0):
        return
    # Height: dome across, raised midrib, lateral vein ridges.
    dome = 1 - s ** 2
    midrib = np.exp(-(s * halfw / (width * 0.05 + 1)) ** 2)
    lat = np.abs(np.sin((t * lateral - np.abs(s) * 0.9) * math.pi))
    veins = np.exp(-((1 - lat) * 9) ** 2) * (np.abs(s) > 0.05)
    h = 0.5 * dome + 0.35 * midrib * vein * 3 + 0.2 * veins * vein
    # Colour: base -> tip gradient, lighter veins, darker toward the edge, a little mottling.
    g = np.clip(t, 0, 1)[..., None]
    col = c_base[None, None, :] * (1 - g) + c_tip[None, None, :] * g
    col = col * (1 - edge_dark * (np.abs(s) ** 3))[..., None]
    col = col * (1 + 0.35 * veins * vein)[..., None] * (1 + 0.45 * midrib * vein)[..., None]
    mott = 1 + 0.08 * np.sin(xs * 0.05 + seed) * np.sin(ys * 0.043 + seed * 2)
    col = col * mott[..., None]
    # Composite over.
    region_a = alpha[y0:y1, x0:x1]
    albedo[y0:y1, x0:x1] = albedo[y0:y1, x0:x1] * (1 - a[..., None]) + col * a[..., None]
    height[y0:y1, x0:x1] = np.where(a > 0.5, np.maximum(height[y0:y1, x0:x1] * 0.7, h), height[y0:y1, x0:x1])
    alpha[y0:y1, x0:x1] = region_a + a * (1 - region_a)


def _pos(x):
    return np.maximum(x, 0.0)


def ellipse_prof(t):
    return _pos(np.sin(np.pi * np.clip(t, 0, 1))) ** 0.7


def lance_prof(t):
    return _pos(np.sin(np.pi * np.clip(t, 0, 1) ** 0.8)) ** 0.9


def heart_prof(t):
    return np.clip(_pos(np.sin(np.pi * (np.clip(t, 0, 1) * 0.85 + 0.15))) ** 0.6, 0, 1)


def blade_prof(t):
    return _pos(1 - np.clip(t, 0, 1)) ** 0.7


def cell_px(name):
    x, y, w, h = CELLS[name]
    return x * SS, y * SS, w * SS, h * SS


def palm_frond(name, base_col, tip_col, dry=False):
    x, y, w, h = cell_px(name)
    cx = x + w / 2
    base_y = y + h * 0.02
    length = h * 0.95
    # Rachis.
    leaf(cx, base_y, 0.0, length, w * 0.018, lambda t: 1 - t * 0.7, base_col * 0.7, tip_col * 0.8, vein=0.0, edge_dark=0.0)
    n = 34
    for i in range(n):
        tt = 0.06 + 0.92 * i / n
        py = base_y + length * tt
        span = (math.sin(math.pi * min(1, tt * 1.1)) ** 0.8) * w * 0.47
        for side in (-1, 1):
            ang = side * (math.radians(62) - tt * math.radians(25)) + rng.normal(0, 0.05)
            if dry and rng.random() < 0.15:
                continue
            ln = span * (0.85 + 0.2 * rng.random())
            leaf(cx, py, ang, ln, ln * 0.085, lance_prof, base_col, tip_col, vein=0.35, lateral=3, edge_dark=0.2,
                 curl=0.15 * side, seed=i)


def bush_cluster(name, base_col, tip_col, count=9, size=0.36):
    x, y, w, h = cell_px(name)
    cx, cy = x + w / 2, y + h * 0.18
    for i in range(count):
        ang = (i / (count - 1) - 0.5) * math.radians(150) + rng.normal(0, 0.1)
        ln = h * size * (0.75 + 0.35 * rng.random())
        leaf(cx + rng.normal(0, w * 0.02), cy, ang, ln, ln * 0.33, ellipse_prof, base_col, tip_col, vein=0.5, lateral=6, seed=i)


def flower(name, petal_col, centre_col, petals=5, petal_len=0.38, petal_w=0.22, prof=ellipse_prof):
    x, y, w, h = cell_px(name)
    cx, cy = x + w / 2, y + h / 2
    for i in range(petals):
        ang = i / petals * 2 * math.pi + rng.normal(0, 0.05)
        ln = h * petal_len
        leaf(cx, cy, ang, ln, ln * petal_w, prof, petal_col * 0.75, petal_col, vein=0.3, lateral=8, edge_dark=0.1, seed=i)
    leaf(cx, cy - h * 0.06, 0.0, h * 0.14, h * 0.05, ellipse_prof, centre_col, centre_col * 1.2, vein=0.0)


# --- draw ---
palm_frond("palm_frond", lin((0.10, 0.36, 0.08)), lin((0.42, 0.80, 0.20)))
palm_frond("palm_dry", lin((0.42, 0.30, 0.14)), lin((0.72, 0.56, 0.28)), dry=True)

x, y, w, h = cell_px("broadleaf")
leaf(x + w / 2, y + h * 0.04, 0.0, h * 0.92, w * 0.38, heart_prof, lin((0.08, 0.34, 0.08)), lin((0.30, 0.66, 0.16)), vein=0.6, lateral=11)
x, y, w, h = cell_px("monstera")
leaf(x + w / 2, y + h * 0.04, 0.0, h * 0.92, w * 0.42, heart_prof, lin((0.04, 0.26, 0.07)), lin((0.18, 0.52, 0.14)), vein=0.6, lateral=7,
     split=0.16, holes=[(0.55, 0.35, 0.05), (0.62, -0.38, 0.045), (0.4, -0.3, 0.04), (0.45, 0.42, 0.035)])
x, y, w, h = cell_px("fern")
cx = x + w / 2
leaf(cx, y + h * 0.03, 0.0, h * 0.93, w * 0.012, lambda t: 1 - t * 0.8, lin((0.10, 0.30, 0.06)), lin((0.30, 0.60, 0.15)), vein=0.0)
for i in range(26):
    tt = 0.06 + 0.9 * i / 26
    span = (math.sin(math.pi * min(1, tt * 1.05)) ** 0.9) * w * 0.4
    for side in (-1, 1):
        leaf(cx, y + h * 0.03 + h * 0.93 * tt, side * math.radians(70), span, span * 0.18, lance_prof,
             lin((0.12, 0.40, 0.08)), lin((0.40, 0.75, 0.20)), vein=0.25, lateral=5, seed=i)
bush_cluster("bush_a", lin((0.10, 0.38, 0.08)), lin((0.38, 0.74, 0.18)))
bush_cluster("bush_b", lin((0.04, 0.24, 0.07)), lin((0.16, 0.50, 0.14)), count=11, size=0.32)
bush_cluster("bush_c", lin((0.20, 0.42, 0.06)), lin((0.62, 0.80, 0.22)), count=8, size=0.4)
x, y, w, h = cell_px("grass")
for i in range(26):
    bx = x + w * (0.15 + 0.7 * rng.random())
    ang = rng.normal(0, 0.3)
    ln = h * (0.55 + 0.4 * rng.random())
    leaf(bx, y + 2, ang, ln, w * 0.022, blade_prof, lin((0.12, 0.40, 0.06)), lin((0.55, 0.82, 0.25)), vein=0.2, lateral=1, curl=rng.normal(0, 0.6), seed=i)
flower("hibiscus", lin((0.95, 0.10, 0.16)), lin((1.0, 0.85, 0.2)), petals=5, petal_len=0.42, petal_w=0.5)
flower("plumeria", lin((1.0, 0.97, 0.88)), lin((1.0, 0.80, 0.18)), petals=5, petal_len=0.4, petal_w=0.36)
x, y, w, h = cell_px("flowers_purple")
for i in range(7):
    px, py = x + w * (0.2 + 0.6 * rng.random()), y + h * (0.2 + 0.6 * rng.random())
    for p in range(5):
        ang = p / 5 * 2 * math.pi
        ln = h * 0.12
        leaf(px, py, ang, ln, ln * 0.55, ellipse_prof, lin((0.45, 0.16, 0.70)), lin((0.75, 0.42, 0.95)), vein=0.2, lateral=4, seed=p + i)
x, y, w, h = cell_px("banana")
leaf(x + w / 2, y + h * 0.03, 0.0, h * 0.94, w * 0.3, lambda t: _pos(np.sin(np.pi * np.clip(t, 0, 1))) ** 0.35, lin((0.16, 0.42, 0.08)),
     lin((0.42, 0.75, 0.18)), vein=0.5, lateral=26, split=0.05)
x, y, w, h = cell_px("ivy")
for i in range(9):
    px, py = x + w * (0.15 + 0.7 * rng.random()), y + h * (0.1 + 0.75 * rng.random())
    leaf(px, py, rng.normal(0, 0.6), h * 0.22, h * 0.13, heart_prof, lin((0.06, 0.30, 0.08)), lin((0.24, 0.58, 0.16)), vein=0.5, lateral=5, seed=i)

# --- downsample, normals, save ---
def down(a):
    if a.ndim == 2:
        return a.reshape(S, SS, S, SS).mean(axis=(1, 3))
    return a.reshape(S, SS, S, SS, a.shape[2]).mean(axis=(1, 3))


A = down(alpha)
C = down(albedo * alpha[..., None]) / np.maximum(A[..., None], 1e-4)
H = gb.blur(down(height), 1)
# Bleed colour outward into transparent texels so mip-maps don't darken leaf edges.
for _ in range(8):
    mask = A > 0.01
    nb = (np.roll(C, 1, 0) + np.roll(C, -1, 0) + np.roll(C, 1, 1) + np.roll(C, -1, 1)) / 4
    C = np.where(mask[..., None], C, nb)
N = gb.height_to_normal(H, 6.0)
rgba = np.concatenate([gb.linear_to_srgb(C), A[..., None]], axis=-1)
gb.save_png(os.path.join(OUT, "leaves_albedo.png"), rgba, alpha=True)
gb.save_png(os.path.join(OUT, "leaves_normal.png"), N * 0.5 + 0.5)
print("[leaf_atlas] done")
