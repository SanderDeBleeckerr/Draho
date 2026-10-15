"""Turn the Sketchfab "Lowpoly Cat Rig + Run Animation" (CC BY 4.0, Daily Lowpoly) into Drahomil.fbx.

What it does:
  1. Imports ThirdParty/Sketchfab/lowpoly-cat/model.glb and removes the extra empties and the stray sphere.
  2. Records the Run animation in world space.
  3. Makes the standing pose the new rest pose (the original rest pose lies on its side), in metres,
     facing +Y, up +Z (the same convention as Tools/Blender/build_drahomil.py).
  4. Writes Run again on the clean rig and adds procedural Idle and Walk clips (IK legs, baked).
  5. Adds eyes, pupils and a nose (the model has no face), and names the materials for Unity.
  6. Exports Draho/Assets/_Draho/Art/Cat/Drahomil.fbx.

Usage:
    pip install bpy          # Python 3.13
    python Tools/Blender/convert_lowpoly_cat.py -- --glb ThirdParty/Sketchfab/lowpoly-cat/model.glb \
        --out Draho/Assets/_Draho/Art/Cat/Drahomil.fbx [--renders docs/images]
"""
import argparse
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--glb", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--renders")
args = ap.parse_args(argv)

FPS = 24
RENAME = {
    "_rootJoint": "Root", "pelvis_01": "Hips", "body_1_02": "Spine", "body_2_03": "Chest",
    "neck_04": "Neck", "head_05": "Head", "ear_right_06": "Ear.R", "ear_left_07": "Ear.L",
    "leg_front_right_1_08": "UpperArm.R", "leg_front_right_2_09": "Forearm.R",
    "leg_front_right_3_010": "Hand.R", "leg_front_right_4_011": "Fingers.R",
    "leg_front_left_1_012": "UpperArm.L", "leg_front_left_2_013": "Forearm.L",
    "leg_front_left_3_014": "Hand.L", "leg_front_left_4_015": "Fingers.L",
    "leg_back_left_1_016": "Thigh.L", "leg_back_left_2_017": "Shin.L",
    "leg_back_left_3_018": "Foot.L", "leg_back_left_4_019": "Toe.L",
    "leg_back_right_1_00": "Thigh.R", "leg_back_right_2_024": "Shin.R",
    "leg_back_right_3_025": "Foot.R", "leg_back_right_4_026": "Toe.R",
    "tail_1_020": "Tail1", "tail_2_021": "Tail2", "tail_3_022": "Tail3", "tail_4_023": "Tail4",
}


def select_only(*objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def material(name, color, rough):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    return m


# ---------------------------------------------------------------------------
# 1. Import and clean up
# ---------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = FPS
bpy.ops.import_scene.gltf(filepath=args.glb)
arm = next(o for o in scene.objects if o.type == "ARMATURE")
body = next(o for o in scene.objects if o.type == "MESH" and o.parent == arm)
run_action = arm.animation_data.action
run_start, run_end = (int(round(f)) for f in run_action.frame_range)

# ---------------------------------------------------------------------------
# 2. Record Run in world space (metres, facing +Y, up +Z).
#    Source world space: centimetres, facing +X, up +Z.
# ---------------------------------------------------------------------------
TO_NEW = Matrix.Rotation(math.radians(90), 4, "Z") @ Matrix.Scale(0.01, 4)


def clean(m):
    """Drop scale: keep only rotation and translation."""
    loc, rot, _ = m.decompose()
    return Matrix.Translation(loc) @ rot.to_matrix().to_4x4()


recorded = {}  # frame -> {new bone name: world matrix}
for f in range(run_start, run_end + 1):
    scene.frame_set(f)
    recorded[f] = {RENAME.get(pb.name, pb.name): clean(TO_NEW @ arm.matrix_world @ pb.matrix)
                   for pb in arm.pose.bones}

# ---------------------------------------------------------------------------
# 3. Standing pose = new rest pose
# ---------------------------------------------------------------------------
arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = Quaternion()
    pb.location = Vector()
    pb.scale = Vector((1, 1, 1))
# The original rest pose lies on its side. In armature space the run has "up" = -Y,
# while the rest pose has "up" = +Z: rotate the root +90 degrees about X.
arm.pose.bones["_rootJoint"].rotation_quaternion = Quaternion(Vector((1, 0, 0)), math.radians(90))
bpy.context.view_layer.update()

# Bake that pose into the mesh, then make it the rest pose.
select_only(body)
for mod in list(body.modifiers):
    if mod.type == "ARMATURE":
        bpy.ops.object.modifier_apply(modifier=mod.name)
select_only(arm)
bpy.ops.object.mode_set(mode="POSE")
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode="OBJECT")

# Remove everything else (empties, the stray Icosphere, old actions).
for o in list(scene.objects):
    if o not in (arm, body):
        bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)

# Put armature and mesh in world space and apply the unit + axis change.
for o in (arm, body):
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = TO_NEW @ mw
select_only(arm, body)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Rename bones; drop the leaf "end" bones (no weights).
select_only(arm)
bpy.ops.object.mode_set(mode="EDIT")
for eb in list(arm.data.edit_bones):
    if eb.name.endswith(tuple(f"_end_0{n}" for n in range(27, 34))) or "_end_" in eb.name:
        arm.data.edit_bones.remove(eb)
    elif eb.name in RENAME:
        eb.name = RENAME[eb.name]
bpy.ops.object.mode_set(mode="OBJECT")
for vg in body.vertex_groups:
    if vg.name in RENAME:
        vg.name = RENAME[vg.name]
body.parent = arm
body.matrix_parent_inverse = arm.matrix_world.inverted()
mod = body.modifiers.new("Armature", "ARMATURE")
mod.object = arm
arm.name = "DrahomilRig"
arm.data.name = "DrahomilRig"
body.name = "DrahomilBody"

# Ground: lowest rest vertex at z = 0, body centred on x/y = 0 under the hips.
zmin = min(v.co.z for v in body.data.vertices)
hips = arm.data.bones["Hips"].head_local
chest = arm.data.bones["Chest"].tail_local
shift = Vector((-(hips.x + chest.x) / 2, -(hips.y + chest.y) / 2, -zmin))
for o in (arm, body):
    o.location += shift
select_only(arm, body)
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
for f in recorded:
    for k in recorded[f]:
        recorded[f][k] = Matrix.Translation(shift) @ recorded[f][k]

bones_by_depth = sorted(arm.pose.bones, key=lambda pb: len(pb.parent_recursive))


def key_pose(frame):
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
        pb.keyframe_insert("location", frame=frame, group=pb.name)


def new_action(name):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    return act


def reset_pose():
    for pb in arm.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()


# ---------------------------------------------------------------------------
# 4a. Run, written again on the clean rig
# ---------------------------------------------------------------------------
select_only(arm)
bpy.ops.object.mode_set(mode="POSE")
new_action("Run")
for f, mats in recorded.items():
    reset_pose()
    for pb in bones_by_depth:
        if pb.name in mats:
            pb.matrix = arm.matrix_world.inverted() @ mats[pb.name]
            bpy.context.view_layer.update()
    key_pose(f - run_start)
run_frames = run_end - run_start

# ---------------------------------------------------------------------------
# 4b. Walk and Idle: body motion by FK, legs by IK targets, then baked.
# ---------------------------------------------------------------------------
FWD, UP, SIDE = Vector((0, 1, 0)), Vector((0, 0, 1)), Vector((1, 0, 0))


def rot_world(pb, axis, angle):
    """Rotate a pose bone about a world axis (through its head), on top of its current pose."""
    bpy.context.view_layer.update()
    m = pb.matrix.copy()
    head = m.translation.copy()
    r = Matrix.Rotation(angle, 4, axis)
    pb.matrix = Matrix.Translation(head) @ r @ Matrix.Translation(-head) @ m


def smooth01(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


LEGS = {  # chain end bone (IK owner), phase offset
    "Shin.L": 0.0, "Forearm.L": 0.25, "Shin.R": 0.5, "Forearm.R": 0.75,
}
targets = {}
for owner in LEGS:
    e = bpy.data.objects.new(f"IK_{owner}", None)
    scene.collection.objects.link(e)
    e.location = arm.matrix_world @ arm.data.bones[owner].tail_local
    targets[owner] = (e, e.location.copy())


def leg_offset(u, duty, stride, lift):
    """Paw path for one leg; u in [0,1). Stance: back along the ground. Swing: forward in an arc."""
    if u < duty:
        s = u / duty
        return FWD * (stride * (0.5 - s))
    s = (u - duty) / (1 - duty)
    return FWD * (stride * (smooth01(s) - 0.5)) + UP * (lift * math.sin(math.pi * s))


def bake_clip(name, frames, pose_fn, use_ik):
    for owner, (e, base) in targets.items():
        e.animation_data_clear()
    cons = []
    if use_ik:
        for owner, (e, base) in targets.items():
            c = arm.pose.bones[owner].constraints.new("IK")
            c.target = e
            c.chain_count = 2
            cons.append((owner, c))
    tmp = new_action(name + "_fk")
    for f in range(frames + 1):
        t = f / frames
        reset_pose()
        bpy.context.view_layer.update()
        pose_fn(t, f)
        key_pose(f)
    scene.frame_start, scene.frame_end = 0, frames
    bpy.ops.nla.bake(frame_start=0, frame_end=frames, only_selected=False, visual_keying=True,
                     clear_constraints=False, use_current_action=False, bake_types={"POSE"})
    baked = arm.animation_data.action
    baked.name = name
    baked.use_fake_user = True
    for owner, c in cons:
        arm.pose.bones[owner].constraints.remove(c)
    bpy.data.actions.remove(tmp)
    return baked


WALK_FRAMES, WALK_DUTY, WALK_STRIDE, WALK_LIFT = 24, 0.62, 0.16, 0.035


def walk_pose(t, f):
    for owner, phase in LEGS.items():
        e, base = targets[owner]
        e.location = base + leg_offset((t + phase) % 1.0, WALK_DUTY, WALK_STRIDE, WALK_LIFT)
        e.keyframe_insert("location", frame=f)
    pb = arm.pose.bones
    w = 2 * math.pi * t
    pb["Hips"].location = Vector((0, 0, 0))
    rot_world(pb["Hips"], FWD, 0.05 * math.sin(w))          # hip roll
    rot_world(pb["Chest"], FWD, -0.05 * math.sin(w + 0.8))  # shoulder roll
    rot_world(pb["Neck"], SIDE, 0.04 * math.sin(2 * w))    # slight nod
    for i, name in enumerate(("Tail1", "Tail2", "Tail3", "Tail4")):
        rot_world(pb[name], UP, 0.12 * math.sin(w - 0.5 * i))
        rot_world(pb[name], SIDE, 0.06 + 0.03 * i)            # tail held up a little


IDLE_FRAMES = 120


def idle_pose(t, f):
    for owner, (e, base) in targets.items():
        e.location = base
        e.keyframe_insert("location", frame=f)
    pb = arm.pose.bones
    w = 2 * math.pi * t
    rot_world(pb["Chest"], SIDE, 0.015 * math.sin(4 * w))   # breathing
    rot_world(pb["Neck"], UP, 0.25 * math.sin(w))           # look around
    rot_world(pb["Head"], SIDE, 0.06 * math.sin(2 * w + 1))
    twitch = max(0.0, math.sin(6 * w)) ** 8
    rot_world(pb["Ear.L"], FWD, -0.3 * twitch)
    for i, name in enumerate(("Tail1", "Tail2", "Tail3", "Tail4")):
        rot_world(pb[name], UP, 0.18 * math.sin(2 * w - 0.6 * i))


walk = bake_clip("Walk", WALK_FRAMES, walk_pose, use_ik=True)
idle = bake_clip("Idle", IDLE_FRAMES, idle_pose, use_ik=True)
bpy.ops.object.mode_set(mode="OBJECT")
for owner, (e, base) in targets.items():
    bpy.data.objects.remove(e, do_unlink=True)
run = bpy.data.actions["Run"]
for a in list(bpy.data.actions):
    if a.name not in ("Run", "Walk", "Idle"):
        bpy.data.actions.remove(a)


# ---------------------------------------------------------------------------
# Measure the natural ground speed of each clip (for CatStats walk/runClipSpeed).
# ---------------------------------------------------------------------------
def ground_speed(action, frames):
    arm.animation_data.action = action
    speeds = []
    for leg in ("Fingers.L", "Fingers.R", "Toe.L", "Toe.R"):
        pts = []
        for f in range(frames + 1):
            scene.frame_set(f)
            pts.append(arm.matrix_world @ arm.pose.bones[leg].head)
        zmin = min(p.z for p in pts)
        for a, b in zip(pts, pts[1:]):
            if a.z < zmin + 0.006 and b.z < zmin + 0.006:
                speeds.append(-(b.y - a.y) * FPS)
    speeds.sort()
    return speeds[len(speeds) // 2] if speeds else 0.0


print(f"CHECK walk clip speed {ground_speed(walk, WALK_FRAMES):.3f} m/s")
print(f"CHECK run clip speed {ground_speed(run, run_frames):.3f} m/s")
arm.animation_data.action = idle
scene.frame_set(0)

# ---------------------------------------------------------------------------
# 5. Face parts and materials
# ---------------------------------------------------------------------------
body.data.materials.clear()
body.data.materials.append(material("Fur", (0.03, 0.03, 0.035), 0.6))
mat_eye = material("Eye", (0.78, 0.82, 0.12), 0.1)
mat_pupil = material("Pupil", (0.0, 0.0, 0.0), 0.1)
mat_nose = material("Nose", (0.16, 0.11, 0.12), 0.4)

deps = bpy.context.evaluated_depsgraph_get()
bvh = BVHTree.FromObject(body, deps)


def surface_hit(start, direction):
    loc, normal, _, _ = bvh.ray_cast(start, direction.normalized())
    assert loc is not None, f"no head surface hit from {tuple(start)}"
    return loc, normal


def add_part(name, mat, loc, radius, scale, normal, embed):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=radius,
                                         location=loc - normal * radius * embed)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = normal.to_track_quat("Z", "Y")
    o.data.materials.append(mat)
    return o


# The nose tip is the front-most vertex of the head; the eyes sit above and beside it.
tip = max(body.data.vertices, key=lambda v: v.co.y)
tip_co = tip.co.copy()
print(f"CHECK nose tip at {tuple(round(c, 3) for c in tip_co)}")
parts = []
for side in (-1, 1):
    loc, n = surface_hit(Vector((side * 0.02, tip_co.y + 0.2, tip_co.z + 0.022)), Vector((0, -1, 0)))
    eye = add_part(f"Eye{side}", mat_eye, loc, 0.0085, (1, 1, 0.7), n, 0.45)
    pupil = add_part(f"Pupil{side}", mat_pupil, loc + n * 0.0035, 0.004, (0.35, 1.0, 0.5), n, 0.3)
    parts += [eye, pupil]
loc, n = surface_hit(Vector((0, tip_co.y + 0.2, tip_co.z)), Vector((0, -1, 0)))
parts.append(add_part("Nose", mat_nose, loc, 0.006, (1.3, 1, 0.8), n, 0.5))

for p in parts:
    vg = p.vertex_groups.new(name="Head")
    vg.add(list(range(len(p.data.vertices))), 1.0, "REPLACE")
select_only(body, *parts)
bpy.ops.object.join()
body = bpy.context.object

print(f"CHECK triangles {sum(len(p.vertices) - 2 for p in body.data.polygons)}")
print(f"CHECK bones {len(arm.data.bones)} materials {[m.name for m in body.data.materials]}")
size = [max(v.co[i] for v in body.data.vertices) - min(v.co[i] for v in body.data.vertices) for i in range(3)]
print(f"CHECK size x {size[0]:.3f} y {size[1]:.3f} z {size[2]:.3f} m (y = nose to tail tip)")

# ---------------------------------------------------------------------------
# 6. Export
# ---------------------------------------------------------------------------
os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
select_only(arm, body)
arm.animation_data.action = idle
bpy.ops.export_scene.fbx(
    filepath=args.out, use_selection=True, object_types={"ARMATURE", "MESH"},
    axis_forward="Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, mesh_smooth_type="FACE")
print("EXPORTED", args.out)

# ---------------------------------------------------------------------------
# 7. Preview renders
# ---------------------------------------------------------------------------
if args.renders:
    os.makedirs(args.renders, exist_ok=True)
    bpy.ops.mesh.primitive_plane_add(size=4)
    bpy.context.object.data.materials.append(material("Floor", (0.45, 0.43, 0.4), 0.8))
    world = bpy.data.worlds.new("W")
    world.color = (0.6, 0.62, 0.66)
    scene.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    scene.collection.objects.link(sun)
    scene.render.engine = "CYCLES"  # CPU only: works on a headless machine without OpenGL
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.62, 0.66, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam

    def shoot(path, loc, target, lens=50, res=(900, 600)):
        cam.location = loc
        cam.data.lens = lens
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.resolution_x, scene.render.resolution_y = res
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)

    arm.animation_data.action = idle
    scene.frame_set(0)
    shoot(os.path.join(args.renders, "lowpoly_cat.png"), (0.75, 0.75, 0.4), (0, 0, 0.12))
    shoot(os.path.join(args.renders, "lowpoly_cat_face.png"), (0.12, 0.6, 0.24), (0, 0.2, 0.19), lens=70)
    for name, action, frames in (("walk", walk, WALK_FRAMES), ("run", run, run_frames)):
        arm.animation_data.action = action
        for i, f in enumerate((0, frames // 4, frames // 2, 3 * frames // 4)):
            scene.frame_set(f)
            shoot(os.path.join(args.renders, f"lowpoly_cat_{name}_{i}.png"), (0.9, 0.0, 0.15), (0, 0, 0.12),
                  res=(500, 330))
