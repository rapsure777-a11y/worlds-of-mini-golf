"""Gamebreak architecture kit helpers (Blender 4/5, Z-up, exported Y-up for Unity).

Pure-python mesh building (no operators in the inner loops): chamfered boxes, tapered prisms, stairs, ashlar courses with excluded openings,
voussoir arches, convex hulls, hip roofs. Every box edge is chamfered so the lighting catches it (nothing is a razor-sharp cube).

Conventions: front of a piece faces -Y (Unity +Z after export), width along X, depth along +Y, up is +Z, origin = centre of the front base
at ground level. Vertex colour RGB = tint x baked AO, A = 0. Faces are flat shaded (FBX 'FACE' smoothing).

Usage (see hero_temple.py):  p = Piece("HeroX", {"Limestone": 1.6, "Paint": 1.0});  p.box("Limestone", ...);  p.export(OUT)
"""
import itertools
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gblib as gb  # noqa: E402

LOG = open(os.path.join(os.getcwd(), "Logs", "blender_arch.log"), "a")


def log(msg):
    LOG.write(msg + "\n")
    LOG.flush()


def stone(v=1.0, warm=0.0):
    """A stone tint: brightness v with a small warm/cool bias."""
    return (min(1.0, v * (1.0 + warm)), v, min(1.0, v * (1.0 - warm)), 0.0)


def jitter(rnd, base=0.93, spread=0.07, warm=0.02):
    v = base + rnd.uniform(-spread, spread)
    return stone(v, rnd.uniform(-warm, warm))


# ---------------------------------------------------------------- geometry generators

def _newell(vs):
    n = Vector((0, 0, 0))
    for i in range(len(vs)):
        a, b = vs[i], vs[(i + 1) % len(vs)]
        n.x += (a.y - b.y) * (a.z + b.z)
        n.y += (a.z - b.z) * (a.x + b.x)
        n.z += (a.x - b.x) * (a.y + b.y)
    return n


def _orient(verts, idx, outward):
    pts = [Vector(verts[i]) for i in idx]
    if _newell(pts).dot(outward) < 0:
        return tuple(reversed(idx))
    return tuple(idx)


def chamfer_box(size, d):
    """Vertices/faces of a box centred at the origin with every edge chamfered by d (0 = plain box)."""
    a, b, c = size[0] / 2.0, size[1] / 2.0, size[2] / 2.0
    d = max(0.0, min(d, a * 0.48, b * 0.48, c * 0.48))
    half = (a, b, c)
    verts, faces = [], []
    if d < 1e-5:
        for sx, sy, sz in itertools.product((-1, 1), repeat=3):
            verts.append((sx * a, sy * b, sz * c))
        corner = {k: i for i, k in enumerate(itertools.product((-1, 1), repeat=3))}
        for axis in range(3):
            for s in (-1, 1):
                idx = [corner[k] for k in corner if k[axis] == s]
                other = [i for i in range(3) if i != axis]
                k0 = {v: k for k, v in corner.items()}
                idx.sort(key=lambda i: math.atan2(k0[i][other[1]], k0[i][other[0]]))
                out = Vector((0, 0, 0)); out[axis] = s
                faces.append(_orient(verts, idx, out))
        return verts, faces
    vid = {}

    def V(corner, axis):
        key = (corner, axis)
        if key not in vid:
            co = [corner[i] * (half[i] if i == axis else half[i] - d) for i in range(3)]
            vid[key] = len(verts)
            verts.append(tuple(co))
        return vid[key]

    corners = list(itertools.product((-1, 1), repeat=3))
    for axis in range(3):
        for s in (-1, 1):
            cs = [k for k in corners if k[axis] == s]
            other = [i for i in range(3) if i != axis]
            cs.sort(key=lambda k: math.atan2(k[other[1]], k[other[0]]))
            idx = [V(k, axis) for k in cs]
            out = Vector((0, 0, 0)); out[axis] = s
            faces.append(_orient(verts, idx, out))
    for A, B in ((0, 1), (0, 2), (1, 2)):
        C = 3 - A - B
        for sA, sB in itertools.product((-1, 1), repeat=2):
            lo = [0, 0, 0]; hi = [0, 0, 0]
            lo[A] = hi[A] = sA; lo[B] = hi[B] = sB; lo[C] = -1; hi[C] = 1
            lo, hi = tuple(lo), tuple(hi)
            idx = [V(lo, A), V(hi, A), V(hi, B), V(lo, B)]
            out = Vector((0, 0, 0)); out[A] = sA; out[B] = sB
            faces.append(_orient(verts, idx, out))
    for k in corners:
        idx = [V(k, 0), V(k, 1), V(k, 2)]
        faces.append(_orient(verts, idx, Vector(k)))
    return verts, faces


def prism_geom(rb, rt, h, sides, chamfer=0.0, base_z=0.0):
    """Vertical tapered prism from z=base_z to base_z+h (radius rb at the bottom, rt at the top), optional chamfered caps."""
    rings = []
    if chamfer > 0 and h > chamfer * 2.5:
        rings = [(0.0, max(0.01, rb - chamfer)), (chamfer, rb), (h - chamfer, rt), (h, max(0.01, rt - chamfer))]
    else:
        rings = [(0.0, rb), (h, rt)]
    verts, faces = [], []
    for z, r in rings:
        for i in range(sides):
            th = math.tau * i / sides
            verts.append((math.cos(th) * r, math.sin(th) * r, base_z + z))
    n = len(rings)
    for j in range(n - 1):
        for i in range(sides):
            a = j * sides + i
            b = j * sides + (i + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple(range((n - 1) * sides, n * sides)))
    return verts, faces


def hull_geom(points):
    """Convex hull of points as (verts, faces) with coplanar triangles merged."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in points]
    bm.verts.ensure_lookup_table()
    res = bmesh.ops.convex_hull(bm, input=vs, use_existing_faces=False)
    interior = res.get("geom_interior", [])
    unused = res.get("geom_unused", [])
    bmesh.ops.delete(bm, geom=list(set(interior + unused)), context="VERTS")
    bmesh.ops.dissolve_limit(bm, angle_limit=0.0017, verts=bm.verts[:], edges=bm.edges[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    verts = [tuple(v.co) for v in bm.verts]
    bm.verts.index_update()
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    return verts, faces


# ---------------------------------------------------------------- the piece

class Piece:
    def __init__(self, name, tiles, seed=1):
        self.name = name
        self.tiles = tiles
        self.rnd = random.Random(seed)
        self.d = {k: dict(v=[], f=[], c=[]) for k in tiles}

    # --- raw adders
    def raw(self, grp, verts, faces, color, rot=None, pos=(0, 0, 0)):
        d = self.d[grp]
        R = Euler(rot).to_matrix() if rot is not None else Matrix.Identity(3)
        base = len(d["v"])
        for v in verts:
            d["v"].append(tuple(Vector(pos) + R @ Vector(v)))
        for f in faces:
            d["f"].append(tuple(base + i for i in f))
            d["c"].append(color)

    def box(self, grp, center, size, rot=(0, 0, 0), color=(1, 1, 1, 0), bevel=0.03, ao=True):
        v, f = chamfer_box(size, bevel)
        if not ao:
            color = (color[0], color[1], color[2], -1.0)   # flag: skip the AO darkening on this box (restored to alpha 0 on export)
        self.raw(grp, v, f, color, rot, center)

    def prism(self, grp, base, rb, rt, h, sides=12, color=(1, 1, 1, 0), chamfer=0.0, rot=None):
        v, f = prism_geom(rb, rt, h, sides, chamfer)
        self.raw(grp, v, f, color, rot, base)

    def hull(self, grp, points, color=(1, 1, 1, 0), pos=(0, 0, 0)):
        v, f = hull_geom(points)
        self.raw(grp, v, f, color, None, pos)

    # --- compound helpers
    def stairs(self, grp, x0, x1, y_front, y_back, z_top, steps, color=(1, 1, 1, 0), bevel=0.02):
        """Steps rising toward +Y: the highest tread (z_top) lies at y_back; the lowest begins at y_front. Solid underneath."""
        run = (y_back - y_front) / steps
        rise = z_top / steps
        for i in range(steps):
            yc = y_front + run * (i + 0.5 + (steps - 1 - i) * 0.0)
            # each step block spans from its front to the back wall
            yf = y_front + run * i
            self.box(grp, ((x0 + x1) / 2, (yf + y_back) / 2, rise * (i + 1) / 2 - 0.0), (x1 - x0, y_back - yf, rise * (i + 1)), color=color, bevel=bevel)

    def ashlar(self, grp, x0, x1, z0, z1, y, depth, ch=0.26, bw=0.55, exclude=(), base=0.93, spread=0.06, veneer=0.12, bevel=0.022, core=True,
               core_color=None, warm=0.025):
        """A masonry face on the plane y (facing -Y) spanning x0..x1, z0..z1, `depth` thick. Blocks (veneer thick) in running bond over a core.
        `exclude` = list of (xa, xb, za, zb) rectangles left open (doorways, windows)."""
        rnd = self.rnd
        if core:
            cc = core_color or stone(base * 0.78)
            # one core box per solid x-strip around the openings would be exact; a single recessed core plus dark openings reads fine
            self.box(grp, ((x0 + x1) / 2, y + veneer + (depth - veneer) / 2 - 0.01, (z0 + z1) / 2), (x1 - x0 - 0.02, depth - veneer, z1 - z0 - 0.02), color=cc, bevel=0.0)
        rows = max(1, round((z1 - z0) / ch))
        rh = (z1 - z0) / rows
        for r in range(rows):
            za, zb = z0 + r * rh, z0 + (r + 1) * rh
            x = x0 - (bw * 0.5 if r % 2 else 0.0)
            while x < x1 - 1e-4:
                w = bw * rnd.uniform(0.8, 1.3)
                xa, xb = max(x, x0), min(x + w, x1)
                x += w
                if xb - xa < 0.08:
                    continue
                blocked = False
                for (ea, eb, fa, fb) in exclude:
                    if xb > ea + 1e-4 and xa < eb - 1e-4 and zb > fa + 1e-4 and za < fb - 1e-4:
                        blocked = True
                        break
                if blocked:
                    continue
                self.box(grp, ((xa + xb) / 2, y + veneer / 2 + rnd.uniform(-0.012, 0.012), (za + zb) / 2),
                         (xb - xa - 0.012, veneer, rh - 0.012), color=jitter(rnd, base, spread, warm), bevel=bevel)

    def arch(self, grp, cx, y, z_spring, r_in, thick, depth, n=9, color=None, bevel=0.02):
        """Semicircular voussoir ring in the XZ plane (front faces -Y): inner radius r_in, ring thickness `thick`, `depth` along +Y from y."""
        rnd = self.rnd
        R = r_in + thick / 2
        arc = math.pi * R / n
        for i in range(n):
            th = math.pi * (i + 0.5) / n
            x, z = cx + R * math.cos(th), z_spring + R * math.sin(th)
            phi = math.atan2(-math.cos(th), -math.sin(th))
            sz = thick * (1.12 if i == n // 2 else 1.0)
            self.box(grp, (x, y + depth / 2, z), (arc * 1.0 - 0.012, depth, sz), rot=(0, phi, 0),
                     color=color or jitter(rnd, 0.95, 0.04), bevel=bevel)

    def roof_hip(self, grp, cx, cy, z0, w, d, h, ridge, thick=0.12, color=(1, 1, 1, 0)):
        """A solid hip roof: eaves rectangle w x d at z0 (thickness `thick` below), ridge length `ridge` along X at z0+h."""
        pts = []
        for sx, sy in itertools.product((-1, 1), repeat=2):
            pts.append((cx + sx * w / 2, cy + sy * d / 2, z0))
            pts.append((cx + sx * w / 2, cy + sy * d / 2, z0 - thick))
        pts.append((cx - ridge / 2, cy, z0 + h))
        pts.append((cx + ridge / 2, cy, z0 + h))
        self.hull(grp, pts, color)

    def column(self, grp, cx, cy, z0, h, r, color=(1, 1, 1, 0), sides=14, base_h=0.14, cap_h=0.16):
        self.prism(grp, (cx, cy, z0), r * 1.35, r * 1.3, base_h, sides + 2, color, 0.02)
        self.prism(grp, (cx, cy, z0 + base_h), r * 1.02, r * 0.9, h - base_h - cap_h, sides, color, 0.0)
        self.box(grp, (cx, cy, z0 + h - cap_h / 2), (r * 3.0, r * 3.0, cap_h), color=color, bevel=0.025)

    # --- finish
    def finish(self, tile_ao=True, ao_samples=20, ao_dist=0.6):
        objs = []
        for grp, dd in self.d.items():
            if not dd["f"]:
                continue
            me = bpy.data.meshes.new(f"{self.name}__{grp}")
            me.from_pydata(dd["v"], [], dd["f"])
            me.update()
            attr = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
            for poly, col in zip(me.polygons, dd["c"]):
                poly.use_smooth = False
                for li in poly.loop_indices:
                    attr.data[li].color = col
            obj = bpy.data.objects.new(f"{self.name}__{grp}", me)
            bpy.context.scene.collection.objects.link(obj)
            gb.box_uv(obj, self.tiles[grp])
            objs.append(obj)
        return objs

    def export(self, out_dir, ao_samples=20, ao_dist=0.7):
        objs = self.finish()
        for o in objs:
            gb.ensure_color_attr(o)
        for o in objs:
            gb.bake_vertex_ao(o, samples=ao_samples, distance=ao_dist, ground=True)
        gb.export_fbx_objects(os.path.join(out_dir, f"{self.name}.fbx"), objs)
        tris = sum(len(o.data.polygons) for o in objs)
        log(f"{self.name}: {tris} faces, groups {[o.name.split('__')[1] for o in objs]}")
        return objs


# ---------------------------------------------------------------- extensions: transform stack and masonry masses

class _Tf:
    def __init__(self, piece, m):
        self.p, self.m = piece, m

    def __enter__(self):
        self.old = self.p.M
        self.p.M = self.old @ self.m
        return self.p

    def __exit__(self, *a):
        self.p.M = self.old


Piece.M = Matrix.Identity(4)
Piece.tf = lambda self, m: _Tf(self, m)


def _raw_tf(self, grp, verts, faces, color, rot=None, pos=(0, 0, 0)):
    d = self.d[grp]
    R = Euler(rot).to_matrix() if rot is not None else Matrix.Identity(3)
    base = len(d["v"])
    M = self.M
    for v in verts:
        d["v"].append(tuple(M @ (Vector(pos) + R @ Vector(v))))
    flip = M.determinant() < 0
    for f in faces:
        idx = tuple(base + i for i in f)
        d["f"].append(tuple(reversed(idx)) if flip else idx)
        d["c"].append(color)


Piece.raw = _raw_tf


def _mass(self, grp, x0, x1, y0, y1, z0, z1, ch=0.27, bw=0.6, exclude_front=(), base=0.93, bevel=0.022, sides=True, front=True, back=False, spread=0.06):
    """A stone mass: running-bond veneer on the front (-Y) and both sides over a recessed core."""
    if front:
        self.ashlar(grp, x0, x1, z0, z1, y0, y1 - y0, ch=ch, bw=bw, exclude=exclude_front, base=base, bevel=bevel, spread=spread)
    else:
        self.box(grp, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0 - 0.02, y1 - y0 - 0.02, z1 - z0 - 0.02), color=stone(base * 0.78), bevel=0)
    if sides:
        L = y1 - y0
        with self.tf(Matrix.Translation((x0, y0, 0)) @ Matrix.Rotation(-math.pi / 2, 4, "Z")):
            self.ashlar(grp, -L, 0.0, z0, z1, 0.0, x1 - x0, ch=ch, bw=bw, core=False, base=base, bevel=bevel, spread=spread)
        with self.tf(Matrix.Translation((x1, y0, 0)) @ Matrix.Rotation(math.pi / 2, 4, "Z")):
            self.ashlar(grp, 0.0, L, z0, z1, 0.0, x1 - x0, ch=ch, bw=bw, core=False, base=base, bevel=bevel, spread=spread)


Piece.mass = _mass


def _stairs(self, grp, x0, x1, y_front, y_back, z_top, steps, color=(1, 1, 1, 0), bevel=0.02):
    """Steps rising toward +Y; the top tread (z_top) ends at y_back. Each step is a solid block down to z=0."""
    run = (y_back - y_front) / steps
    rise = z_top / steps
    for i in range(steps):
        yf = y_front + run * i
        self.box(grp, ((x0 + x1) / 2, (yf + y_back) / 2, rise * (i + 1) / 2), (x1 - x0, y_back - yf, rise * (i + 1)), color=color, bevel=bevel)


Piece.stairs = _stairs


def _wedge(self, grp, x0, x1, y_front, y_back, z_front, z_back, color=(1, 1, 1, 0), z_base=0.0):
    """A stair cheek: sloping top from z_front (at y_front) to z_back (at y_back)."""
    pts = []
    for x in (x0, x1):
        pts += [(x, y_front, z_base), (x, y_back, z_base), (x, y_back, z_back), (x, y_front, z_front)]
    self.hull(grp, pts, color)


Piece.wedge = _wedge


def _sun(self, grp, cx, y, cz, r, rays=16, color=(0.98, 0.78, 0.28, 0), ring_color=None, depth=0.07, ring_grp=None):
    """A sun relief on a wall facing -Y: gold disc, rim ring and rays. (y = the wall plane; the relief stands in front of it.)"""
    rg = ring_grp or grp
    self.prism(grp, (cx, y + 0.005, cz), r * 0.62, r * 0.62, depth, 24, color, 0.012, rot=(math.pi / 2, 0, 0))
    self.prism(rg, (cx, y + 0.005, cz), r * 0.80, r * 0.80, depth * 0.55, 28, ring_color or stone(0.9), 0.01, rot=(math.pi / 2, 0, 0))
    for k in range(rays):
        a = math.tau * k / rays
        rr = r * 0.97
        self.box(grp, (cx + rr * math.cos(a), y - depth * 0.35, cz + rr * math.sin(a)), (r * 0.16, depth * 0.7, r * 0.34),
                 rot=(0, math.pi / 2 - a, 0), color=color, bevel=0.008)


Piece.sun = _sun


def bake_ao(obj, samples=20, distance=0.7, floor=0.42, ground=True):
    """Vertex AO into 'Col' (tint x lerp(floor, 1, ao)): never pitch black. Big faces whose corner vertices are buried in neighbouring
    geometry would otherwise bake to 0 and render black."""
    scene = bpy.context.scene
    scene.cycles.samples = samples
    scene.render.bake.target = "VERTEX_COLORS"
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.light_settings.distance = distance
    tmp = None
    if ground:
        bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.02))
        tmp = bpy.context.active_object
    attr = obj.data.color_attributes["Col"]
    if "AO" in obj.data.color_attributes:
        obj.data.color_attributes.remove(obj.data.color_attributes["AO"])
    ao = obj.data.color_attributes.new("AO", "FLOAT_COLOR", "CORNER")
    obj.data.color_attributes.active_color = ao
    if not obj.data.materials:
        obj.data.materials.append(bpy.data.materials.new("bake_tmp"))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type="AO")
    for d, src in zip(attr.data, ao.data):
        v = floor + (1.0 - floor) * src.color[0]
        if d.color[3] < -0.5:
            d.color = (d.color[0], d.color[1], d.color[2], 0.0)
            continue
        d.color = (d.color[0] * v, d.color[1] * v, d.color[2] * v, d.color[3])
    obj.data.color_attributes.remove(ao)
    obj.data.color_attributes.active_color = obj.data.color_attributes["Col"]
    if tmp:
        bpy.data.objects.remove(tmp, do_unlink=True)


def _export(self, out_dir, ao_samples=20, ao_dist=0.7, floor=0.42):
    objs = self.finish()
    for o in objs:
        bake_ao(o, ao_samples, ao_dist, floor)
    gb.export_fbx_objects(os.path.join(out_dir, f"{self.name}.fbx"), objs)
    tris = sum(len(o.data.polygons) for o in objs)
    log(f"{self.name}: {tris} faces, groups {[o.name.split('__')[1] for o in objs]}")
    return objs


Piece.export = _export


def _beam(self, grp, a, b, w=0.12, t=0.1, color=(1, 1, 1, 0), bevel=0.012, roll=0.0):
    """A chamfered beam from point a to point b (cross-section w x t)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    ln = d.length
    if ln < 1e-4:
        return
    q = d.to_track_quat("X", "Z")
    if roll:
        q = q @ Euler((roll, 0, 0)).to_quaternion()
    eul = q.to_euler()
    self.box(grp, tuple((a + b) / 2), (ln, w, t), rot=tuple(eul), color=color, bevel=bevel)


Piece.beam = _beam
