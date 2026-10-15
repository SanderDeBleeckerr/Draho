"""Turn the Sketchfab "Pro Line Contemporary Sliding Door new" (CC BY 4.0, faiyaz5yaz) into SlidingDoor.fbx.

The source has the whole frame as one mesh and both glass panes as another. This script splits them
into loose parts and builds three objects:
  Frame       outer frame + the fixed sash (does not move)
  FixedGlass  the fixed glass pane
  Slider      the sliding sash + its glass pane (Unity moves this object)
It also removes the colour swatches that come with the model, converts centimetres to metres and
puts the origin at the bottom centre. Width is along X, height along Z, depth along Y.

Usage:
    python Tools/Blender/convert_sliding_door.py -- --glb ThirdParty/Sketchfab/sliding-door/model.glb \
        --out Draho/Assets/_Draho/Art/Props/SlidingDoor.fbx
"""
import argparse
import os
import sys

import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--glb", required=True)
ap.add_argument("--out", required=True)
args = ap.parse_args(argv)


def select_only(*objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def world_bounds(objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    return (Vector([min(p[i] for p in pts) for i in range(3)]),
            Vector([max(p[i] for p in pts) for i in range(3)]))


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.glb)
scene = bpy.context.scene

meshes = [o for o in scene.objects if o.type == "MESH"]
frame_src = next(o for o in meshes if "Color_000" in o.name)
glass_src = next(o for o in meshes if "Glass" in o.name)
for o in list(scene.objects):  # colour swatches, empties
    if o not in (frame_src, glass_src):
        bpy.data.objects.remove(o, do_unlink=True)

# Bake transforms (centimetres -> metres) into the meshes.
for o in (frame_src, glass_src):
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = Matrix.Scale(0.01, 4) @ mw
select_only(frame_src, glass_src)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Origin at the bottom centre of the whole door.
lo, hi = world_bounds([frame_src, glass_src])
shift = Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z))
for o in (frame_src, glass_src):
    o.location += shift
select_only(frame_src, glass_src)
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
lo, hi = world_bounds([frame_src, glass_src])
width = hi.x - lo.x
print(f"CHECK door size {width:.2f} x {hi.z - lo.z:.2f} m, depth {hi.y - lo.y:.3f} m")


def split_loose(obj):
    select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    return list(bpy.context.selected_objects)


frame_parts = split_loose(frame_src)
glass_parts = split_loose(glass_src)
print(f"CHECK {len(frame_parts)} frame parts, {len(glass_parts)} glass parts")

fixed, slider, fixed_glass = [], [], []
z_top = hi.z
for o in frame_parts:
    a, b = world_bounds([o])
    # The outer frame reaches (almost) the floor or the top, or spans most of the width.
    # The two sashes sit inside it; the left sash slides.
    if a.z < 0.03 or b.z > z_top - 0.02 or b.x - a.x > 0.6 * width:
        fixed.append(o)
    elif (a.x + b.x) / 2 < 0:
        slider.append(o)
    else:
        fixed.append(o)
for o in glass_parts:
    a, b = world_bounds([o])
    (slider if (a.x + b.x) / 2 < 0 else fixed_glass).append(o)
assert slider and fixed_glass, "could not find the two sashes"


def merge(objs, name):
    select_only(*objs)
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    o.data.name = name
    return o


frame = merge(fixed, "Frame")
slider_obj = merge(slider, "Slider")
glass = merge(fixed_glass, "FixedGlass")

# Materials named for Unity: DoorFrame (white) and DoorGlass (transparent).
for o in (frame, slider_obj, glass):
    for slot in o.material_slots:
        if slot.material is None:
            continue
        slot.material.name = "DoorGlass" if "Glass" in slot.material.name or "glass" in slot.material.name.lower() \
            else "DoorFrame"

# The slider's origin at its own bottom centre, so Unity can move it cleanly.
a, b = world_bounds([slider_obj])
select_only(slider_obj)
scene.cursor.location = Vector(((a.x + b.x) / 2, (a.y + b.y) / 2, a.z))
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
sa, sb = world_bounds([slider_obj])
ga, gb = world_bounds([glass])
print(f"CHECK slider x {sa.x:.2f}..{sb.x:.2f} y {sa.y:.3f}..{sb.y:.3f}; fixed glass x {ga.x:.2f}..{gb.x:.2f} "
      f"y {ga.y:.3f}..{gb.y:.3f}")

os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
select_only(frame, slider_obj, glass)
bpy.ops.export_scene.fbx(
    filepath=args.out, use_selection=True, object_types={"MESH"},
    axis_forward="Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    bake_anim=False, mesh_smooth_type="FACE")
print("EXPORTED", args.out)

if os.environ.get("DOOR_RENDER"):  # debug: closed and open renders
    import math
    cam = bpy.data.objects.new("C", bpy.data.cameras.new("C"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (1.2, -3.2, 1.6)
    cam.rotation_euler = (Vector((0, 0, 1.1)) - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    sun = bpy.data.objects.new("S", bpy.data.lights.new("S", "SUN"))
    sun.data.energy = 3
    sun.rotation_euler = (0.8, 0.2, 0.5)
    scene.collection.objects.link(sun)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.render.resolution_x, scene.render.resolution_y = 500, 500
    for state, dx in (("closed", 0.0), ("open", 0.85)):
        slider_obj.location.x += dx
        scene.render.filepath = os.environ["DOOR_RENDER"] + f"_{state}.png"
        bpy.ops.render.render(write_still=True)
        slider_obj.location.x -= dx
