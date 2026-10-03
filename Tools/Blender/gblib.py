"""Gamebreak Blender helpers: shader-graph DSL, seamless (torus-mapped) procedural textures,
Cycles baking, numpy post-processing (normals from height, cavity) and FBX export.

Run scripts headless:  blender -b --factory-startup --python Tools/Blender/<script>.py -- <out_dir>
"""
import math
import os
import sys

import bpy
import numpy as np

TAU = 2.0 * math.pi


def out_dir(default):
    """Output folder from the command line (after '--'), else default."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    d = argv[0] if argv else default
    os.makedirs(d, exist_ok=True)
    return d


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    try:
        prefs.compute_device_type = "HIP"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = True
        scene.cycles.device = "GPU"
    except Exception:  # CPU fallback is fine, just slower
        scene.cycles.device = "CPU"
    scene.cycles.samples = 8
    return scene


def srgb(r, g, b):
    """Designer colour (sRGB 0..1) -> linear for emission/base colour sockets."""
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (f(r), f(g), f(b), 1.0)


class G:
    """Tiny shader-graph builder. Methods return output sockets."""

    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.out = self.nt.nodes.new("ShaderNodeOutputMaterial")

    def node(self, kind, **props):
        n = self.nt.nodes.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def link(self, a, b):
        self.nt.links.new(a, b)

    def val(self, x):
        n = self.node("ShaderNodeValue")
        n.outputs[0].default_value = x
        return n.outputs[0]

    def _in(self, sock_or_value, target):
        if isinstance(sock_or_value, (int, float)):
            target.default_value = sock_or_value
        else:
            self.link(sock_or_value, target)

    def math(self, op, a, b=None, clamp=False):
        n = self.node("ShaderNodeMath", operation=op, use_clamp=clamp)
        self._in(a, n.inputs[0])
        if b is not None:
            self._in(b, n.inputs[1])
        return n.outputs[0]

    def add(self, a, b): return self.math("ADD", a, b)
    def sub(self, a, b): return self.math("SUBTRACT", a, b)
    def mul(self, a, b): return self.math("MULTIPLY", a, b)
    def sin(self, a): return self.math("SINE", a)
    def cos(self, a): return self.math("COSINE", a)
    def pow(self, a, b): return self.math("POWER", a, b)
    def clamp01(self, a): return self.math("ADD", a, 0.0, clamp=True)
    def smoothstep(self, lo, hi, x):
        n = self.node("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP")
        self._in(x, n.inputs["Value"])
        n.inputs["From Min"].default_value = lo
        n.inputs["From Max"].default_value = hi
        return n.outputs["Result"]

    def uv(self):
        tc = self.node("ShaderNodeTexCoord")
        sep = self.node("ShaderNodeSeparateXYZ")
        self.link(tc.outputs["UV"], sep.inputs[0])
        return sep.outputs["X"], sep.outputs["Y"]

    def torus(self, u, v, ru, rv, offset=(0.0, 0.0, 0.0, 0.0)):
        """Seamless 4D coordinates: (Ru cos 2πu, Ru sin 2πu, Rv cos 2πv) and W = Rv sin 2πv."""
        au, av = self.mul(u, TAU), self.mul(v, TAU)
        comb = self.node("ShaderNodeCombineXYZ")
        self.link(self.add(self.mul(self.cos(au), ru), offset[0]), comb.inputs["X"])
        self.link(self.add(self.mul(self.sin(au), ru), offset[1]), comb.inputs["Y"])
        self.link(self.add(self.mul(self.cos(av), rv), offset[2]), comb.inputs["Z"])
        w = self.add(self.mul(self.sin(av), rv), offset[3])
        return comb.outputs[0], w

    def noise(self, vec, w, scale=1.0, detail=4.0, rough=0.55, lac=2.0, distortion=0.0, kind="FBM"):
        n = self.node("ShaderNodeTexNoise", noise_dimensions="4D")
        try:
            n.noise_type = kind
        except Exception:
            pass
        self.link(vec, n.inputs["Vector"])
        self.link(w, n.inputs["W"])
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        n.inputs["Lacunarity"].default_value = lac
        n.inputs["Distortion"].default_value = distortion
        return n.outputs["Fac"]

    def voronoi(self, vec, w, scale=1.0, feature="F1", randomness=1.0, output="Distance"):
        n = self.node("ShaderNodeTexVoronoi", voronoi_dimensions="4D", feature=feature)
        self.link(vec, n.inputs["Vector"])
        self.link(w, n.inputs["W"])
        n.inputs["Scale"].default_value = scale
        n.inputs["Randomness"].default_value = randomness
        return n.outputs[output]

    def ramp(self, x, stops):
        """stops: [(pos, (r,g,b,a)), ...] in linear colour."""
        n = self.node("ShaderNodeValToRGB")
        el = n.color_ramp.elements
        el[0].position, el[0].color = stops[0]
        el[1].position, el[1].color = stops[-1]
        for pos, col in stops[1:-1]:
            e = el.new(pos)
            e.color = col
        self._in(x, n.inputs["Fac"])
        return n.outputs["Color"]

    def mix_color(self, fac, a, b):
        n = self.node("ShaderNodeMix", data_type="RGBA", blend_type="MIX")
        self._in(fac, n.inputs[0])
        self._in(a, n.inputs[6]) if not isinstance(a, tuple) else setattr(n.inputs[6], "default_value", a)
        self._in(b, n.inputs[7]) if not isinstance(b, tuple) else setattr(n.inputs[7], "default_value", b)
        return n.outputs[2]

    def multiply_color(self, a, factor):
        n = self.node("ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY")
        n.inputs[0].default_value = 1.0
        self.link(a, n.inputs[6])
        self._in(factor, n.inputs[7]) if not isinstance(factor, (int, float)) else setattr(n.inputs[7], "default_value", (factor, factor, factor, 1))
        return n.outputs[2]

    def emit(self, sock):
        """Route a socket to an emission shader (for baking raw values)."""
        e = self.node("ShaderNodeEmission")
        self.link(sock, e.inputs["Color"])
        e.inputs["Strength"].default_value = 1.0
        for l in list(self.out.inputs["Surface"].links):
            self.nt.links.remove(l)
        self.link(e.outputs[0], self.out.inputs["Surface"])


def bake_plane(name, size, build, out, extra=None):
    """
    Build a material with build(g) -> dict(albedo=socket, height=socket, rough=socket[, ...]),
    bake each channel on a unit plane, derive a tiling normal map and cavity from height, save PNGs.
    """
    reset_scene()
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    plane = bpy.context.active_object
    mat = bpy.data.materials.new(name)
    plane.data.materials.append(mat)
    g = G(mat)
    chans = build(g)

    def bake(sock, colorspace):
        img = bpy.data.images.new(f"{name}_tmp", size, size, alpha=False, float_buffer=True)
        img.colorspace_settings.name = "Non-Color"
        tex = g.node("ShaderNodeTexImage")
        tex.image = img
        g.nt.nodes.active = tex
        g.emit(sock)
        bpy.ops.object.select_all(action="DESELECT")
        plane.select_set(True)
        bpy.context.view_layer.objects.active = plane
        bpy.ops.object.bake(type="EMIT", margin=0)
        arr = np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)
        bpy.data.images.remove(img)
        g.nt.nodes.remove(tex)
        return arr

    height = bake(chans["height"], "Non-Color")[..., 0]
    albedo = bake(chans["albedo"], "sRGB")[..., :3]
    rough = bake(chans["rough"], "Non-Color")[..., 0] if "rough" in chans else np.full((size, size), 0.7, np.float32)

    cav = cavity(height, chans.get("cavity_strength", 1.0))
    albedo = albedo * cav[..., None]
    normal = height_to_normal(height, chans.get("normal_strength", 4.0))

    save_png(os.path.join(out, f"{name}_albedo.png"), linear_to_srgb(albedo))
    save_png(os.path.join(out, f"{name}_normal.png"), normal * 0.5 + 0.5)
    save_png(os.path.join(out, f"{name}_height.png"), np.repeat(height[..., None], 3, axis=2))
    # Mask map (Unity HDRP-style packing reused by our shaders): R metallic, G occlusion, B unused, A smoothness.
    mask = np.stack([np.zeros_like(height), cav, np.zeros_like(height), 1.0 - rough], axis=-1)
    save_png(os.path.join(out, f"{name}_mask.png"), mask, alpha=True)
    print(f"[gblib] baked {name} ({size}px)")


def height_to_normal(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    s = strength * h.shape[0] / 256.0
    n = np.stack([-dx * s, -dy * s, np.ones_like(h)], axis=-1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def blur(h, r):
    out = h.copy()
    for axis in (0, 1):
        acc = np.zeros_like(out)
        for k in range(-r, r + 1):
            acc += np.roll(out, k, axis=axis)
        out = acc / (2 * r + 1)
    return out


def cavity(h, strength):
    """Darken crevices (tiling-safe), returns multiplier ~0.6..1."""
    d = blur(h, 4) - h
    c = 1.0 - np.clip(d * 6.0 * strength, 0.0, 0.4)
    return c.astype(np.float32)


def linear_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def save_png(path, arr, alpha=False):
    h, w = arr.shape[:2]
    chans = 4 if alpha else 3
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=alpha)
    img.colorspace_settings.name = "Non-Color"  # values are already final
    px = np.ones((h, w, 4), np.float32)
    px[..., :chans] = np.clip(arr[..., :chans], 0, 1)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def export_fbx(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_space_transform=True, path_mode="STRIP")
    print(f"[gblib] exported {path}")


# ---------------------------------------------------------------- mesh helpers for hero models

def mesh_object(name, verts, faces, uvs=None, colors=None, smooth=True):
    """Create an object from python lists. uvs/colors are per face-corner lists aligned with faces."""
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    if uvs is not None:
        uv = me.uv_layers.new(name="UVMap")
        k = 0
        for poly in me.polygons:
            for li in poly.loop_indices:
                uv.data[li].uv = uvs[k]
                k += 1
    if colors is not None:
        attr = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        k = 0
        for poly in me.polygons:
            for li in poly.loop_indices:
                attr.data[li].color = colors[k]
                k += 1
    for p in me.polygons:
        p.use_smooth = smooth
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    return obj


def box_uv(obj, tile):
    """World-scale box projection: 1 texture tile per `tile` metres on every face (dominant axis)."""
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active
    mw = obj.matrix_world
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal)
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            co = mw @ me.vertices[me.loops[li].vertex_index].co
            if ax == 0:
                uv.data[li].uv = (co.y / tile, co.z / tile)
            elif ax == 1:
                uv.data[li].uv = (co.x / tile, co.z / tile)
            else:
                uv.data[li].uv = (co.x / tile, co.y / tile)


def ensure_color_attr(obj, value=(1, 1, 1, 1)):
    me = obj.data
    if "Col" not in me.color_attributes:
        attr = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for d in attr.data:
            d.color = value
    me.color_attributes.active_color = me.color_attributes["Col"]
    return me.color_attributes["Col"]


def bake_vertex_ao(obj, samples=48, ground=True, distance=2.0):
    """Bake ambient occlusion into the 'Col' colour attribute (R=G=B=AO, A preserved)."""
    scene = bpy.context.scene
    scene.cycles.samples = samples
    scene.render.bake.target = "VERTEX_COLORS"
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.light_settings.distance = distance
    tmp_ground = None
    if ground:
        bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, obj.matrix_world.translation.z - 0.02))
        tmp_ground = bpy.context.active_object
    attr = ensure_color_attr(obj)
    alphas = [d.color[3] for d in attr.data]
    # A temporary attribute receives the bake so we can keep alpha (wind weight) intact.
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
    for d, a, src in zip(attr.data, alphas, ao.data):
        v = src.color[0]
        d.color = (d.color[0] * v, d.color[1] * v, d.color[2] * v, a)
    obj.data.color_attributes.remove(ao)
    obj.data.color_attributes.active_color = obj.data.color_attributes["Col"]
    if tmp_ground:
        bpy.data.objects.remove(tmp_ground, do_unlink=True)


def decimate(obj, ratio):
    m = obj.modifiers.new("Decimate", "DECIMATE")
    m.ratio = ratio
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=m.name)


def export_fbx_objects(path, objects):
    """FBX for Unity: Y-up, metres, vertex colours, no materials needed (assigned by name suffix)."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False,
                             bake_space_transform=True, colors_type="LINEAR", path_mode="STRIP", embed_textures=False)
    print(f"[gblib] exported {path}")
