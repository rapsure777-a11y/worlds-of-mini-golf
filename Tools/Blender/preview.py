"""Renders quick workbench previews of exported FBX models: blender -b --python preview.py -- ModelA ModelB ...  -> Screenshots/preview/<Model>_<view>.png"""
import math, os, sys
import bpy
from mathutils import Vector

names = sys.argv[sys.argv.index("--") + 1:]
out = os.path.join(os.getcwd(), "Screenshots", "preview")
os.makedirs(out, exist_ok=True)
for name in names:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(os.getcwd(), "Assets/_Game/Art/Generated/Models", name + ".fbx"))
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "VERTEX"
    sc.render.resolution_x, sc.render.resolution_y = 900, 700
    sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.55, 0.7, 0.9)
    pts = [o.matrix_world @ Vector(c) for o in bpy.data.objects if o.type == "MESH" for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (lo + hi) / 2
    r = max((hi - lo).length * 0.62, 1.0)
    for view, (ax, ay) in {"front": (0.0, -1.0), "three": (0.75, -0.75), "side": (1.0, 0.0)}.items():
        cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
        cam.data.lens = 55
        d = Vector((ax, ay, 0.22)).normalized()
        cam.location = c + d * r * 2.0
        cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, f"{name}_{view}.png")
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(cam)
