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
CROUCH_FRAMES, CROUCH_DUTY, CROUCH_STRIDE, CROUCH_LIFT = 28, 0.7, 0.12, 0.02
CROUCH_DROP = 0.065  # how far the body sinks when the cat ducks
TAILS = ("Tail1", "Tail2", "Tail3", "Tail4")
# Sign guide (world axes, the cat faces +Y): rotating about SIDE (+X) by a positive angle swings a
# downward leg forward, pitches the body nose-up, and lowers a backward-pointing tail.


def move_world(pb, offset):
    bpy.context.view_layer.update()
    pb.matrix = Matrix.Translation(offset) @ pb.matrix


def set_legs(t, phases, duty, stride, lift, f):
    for owner, phase in phases.items():
        e, base = targets[owner]
        e.location = base + (leg_offset((t + phase) % 1.0, duty, stride, lift) if duty else Vector())
        e.keyframe_insert("location", frame=f)


def walk_pose(t, f):
    set_legs(t, LEGS, WALK_DUTY, WALK_STRIDE, WALK_LIFT, f)
    pb = arm.pose.bones
    w = 2 * math.pi * t
    rot_world(pb["Hips"], FWD, 0.05 * math.sin(w))          # hip roll
    rot_world(pb["Chest"], FWD, -0.05 * math.sin(w + 0.8))  # shoulder roll
    rot_world(pb["Neck"], SIDE, 0.04 * math.sin(2 * w))    # slight nod
    for i, name in enumerate(TAILS):
        rot_world(pb[name], UP, 0.12 * math.sin(w - 0.5 * i))
        rot_world(pb[name], SIDE, -(0.10 + 0.06 * i))         # tail up in a soft curve


IDLE_FRAMES = 120


def idle_pose(t, f):
    set_legs(t, LEGS, 0, 0, 0, f)
    pb = arm.pose.bones
    w = 2 * math.pi * t
    rot_world(pb["Chest"], SIDE, 0.015 * math.sin(4 * w))   # breathing
    rot_world(pb["Neck"], UP, 0.25 * math.sin(w))           # look around
    rot_world(pb["Head"], SIDE, 0.06 * math.sin(2 * w + 1))
    twitch = max(0.0, math.sin(6 * w)) ** 8
    rot_world(pb["Ear.L"], FWD, -0.3 * twitch)
    for i, name in enumerate(TAILS):
        rot_world(pb[name], UP, 0.18 * math.sin(2 * w - 0.6 * i))
        rot_world(pb[name], SIDE, -(0.05 + 0.05 * i))


def crouch_body(pb, w):
    """Low, stretched stalking body: hips and chest down, head low and level, tail low."""
    move_world(pb["Root"], -UP * CROUCH_DROP)
    rot_world(pb["Neck"], SIDE, -0.25)                      # neck forward and down
    rot_world(pb["Head"], SIDE, 0.3)                        # eyes stay level
    for ear in ("Ear.L", "Ear.R"):
        rot_world(pb[ear], SIDE, 0.25)                      # ears a little back
    for i, name in enumerate(TAILS):
        rot_world(pb[name], SIDE, 0.06)                     # tail low, almost on the floor
        rot_world(pb[name], UP, 0.06 * math.sin(w - 0.7 * i))


def crouch_idle_pose(t, f):
    set_legs(t, LEGS, 0, 0, 0, f)
    pb = arm.pose.bones
    w = 2 * math.pi * t
    crouch_body(pb, 2 * w)
    rot_world(pb["Chest"], SIDE, 0.012 * math.sin(4 * w))   # breathing
    wiggle = max(0.0, math.sin(3 * w)) ** 6                  # bum wiggle before a pounce
    rot_world(pb["Hips"], FWD, 0.08 * wiggle * math.sin(24 * w))


def crouch_walk_pose(t, f):
    set_legs(t, LEGS, CROUCH_DUTY, CROUCH_STRIDE, CROUCH_LIFT, f)
    pb = arm.pose.bones
    w = 2 * math.pi * t
    crouch_body(pb, w)
    rot_world(pb["Hips"], FWD, 0.04 * math.sin(w))
    rot_world(pb["Chest"], FWD, -0.04 * math.sin(w + 0.8))


AIR_FRAMES = 12


def jump_pose(t, f):
    """Rising: front paws reach forward, hind legs push out behind, tail streams back."""
    pb = arm.pose.bones
    w = 2 * math.pi * t
    rot_world(pb["Root"], SIDE, 0.18)                       # nose up
    for side in ("L", "R"):
        rot_world(pb[f"UpperArm.{side}"], SIDE, 1.0)
        rot_world(pb[f"Forearm.{side}"], SIDE, -0.5)
        rot_world(pb[f"Thigh.{side}"], SIDE, -0.9)
        rot_world(pb[f"Shin.{side}"], SIDE, -0.3)
    rot_world(pb["Neck"], SIDE, -0.15)
    for i, name in enumerate(TAILS):
        rot_world(pb[name], SIDE, 0.05 + 0.03 * math.sin(w - 0.6 * i))


def fall_pose(t, f):
    """Falling: front paws reach down for the landing, hind legs tucked under, tail up for balance."""
    pb = arm.pose.bones
    w = 2 * math.pi * t
    rot_world(pb["Root"], SIDE, -0.12)                      # nose down
    for side in ("L", "R"):
        rot_world(pb[f"UpperArm.{side}"], SIDE, 0.45)
        rot_world(pb[f"Forearm.{side}"], SIDE, -0.15)
        rot_world(pb[f"Thigh.{side}"], SIDE, 0.55)
        rot_world(pb[f"Shin.{side}"], SIDE, -0.5)
    rot_world(pb["Neck"], SIDE, 0.2)
    for i, name in enumerate(TAILS):
        rot_world(pb[name], SIDE, -(0.1 + 0.06 * i) + 0.04 * math.sin(w - 0.6 * i))


walk = bake_clip("Walk", WALK_FRAMES, walk_pose, use_ik=True)
idle = bake_clip("Idle", IDLE_FRAMES, idle_pose, use_ik=True)
crouch_idle = bake_clip("CrouchIdle", IDLE_FRAMES, crouch_idle_pose, use_ik=True)
crouch_walk = bake_clip("CrouchWalk", CROUCH_FRAMES, crouch_walk_pose, use_ik=True)
jump = bake_clip("Jump", AIR_FRAMES, jump_pose, use_ik=False)
fall = bake_clip("Fall", AIR_FRAMES, fall_pose, use_ik=False)
bpy.ops.object.mode_set(mode="OBJECT")
for owner, (e, base) in targets.items():
    bpy.data.objects.remove(e, do_unlink=True)
run = bpy.data.actions["Run"]
CLIPS = ("Run", "Walk", "Idle", "CrouchIdle", "CrouchWalk", "Jump", "Fall")
for a in list(bpy.data.actions):
    if a.name not in CLIPS:
        bpy.data.actions.remove(a)


# ---------------------------------------------------------------------------
# Measure the natural ground speed of each clip (for CatStats walk/run/crouchClipSpeed).
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
print(f"CHECK crouch clip speed {ground_speed(crouch_walk, CROUCH_FRAMES):.3f} m/s")

# ---------------------------------------------------------------------------
# 5. Face: low-poly, flat-shaded parts in the same style as the body
# ---------------------------------------------------------------------------
# Work in the rest pose: the parts are placed on the rest mesh and skinned to the Head/Ear bones.
arm.animation_data.action = None
reset_pose()
bpy.context.view_layer.update()

body.data.materials.clear()
body.data.materials.append(material("Fur", (0.03, 0.03, 0.035), 0.6))
MATS = {
    "Eye": material("Eye", (0.86, 0.66, 0.1), 0.15),      # amber
    "Pupil": material("Pupil", (0.0, 0.0, 0.0), 0.1),
    "Glint": material("Glint", (1.0, 1.0, 1.0), 0.1),
    "Nose": material("Nose", (0.3, 0.19, 0.21), 0.45),     # dusky pink-grey
    "InnerEar": material("InnerEar", (0.36, 0.22, 0.25), 0.6),
    "Whisker": material("Whisker", (0.82, 0.82, 0.8), 0.4),
}

import bmesh  # noqa: E402  (after bpy, see build_drahomil.py)

rest_mesh = body.data.copy()
rest_obj = bpy.data.objects.new("RestProbe", rest_mesh)
scene.collection.objects.link(rest_obj)
rest_obj.matrix_world = body.matrix_world
bvh = BVHTree.FromObject(rest_obj, bpy.context.evaluated_depsgraph_get())


def surface_hit(start, direction):
    loc, normal, _, _ = bvh.ray_cast(start, direction.normalized())
    assert loc is not None, f"no head surface hit from {tuple(start)}"
    return loc, normal


def frame_at(normal, up_hint=UP):
    """Local axes on the surface: x = sideways, y = up along the surface, z = out of the surface."""
    z = normal.normalized()
    x = up_hint.cross(z).normalized()
    y = z.cross(x).normalized()
    return x, y, z


def part(name, mat, verts, faces, bone, closed=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    if closed:  # Unity hides back faces: point every face outwards
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
    me.update()
    o = bpy.data.objects.new(name, me)
    scene.collection.objects.link(o)
    o.data.materials.append(MATS[mat])
    for poly in o.data.polygons:
        poly.use_smooth = False
    vg = o.vertex_groups.new(name=bone)
    vg.add(list(range(len(verts))), 1.0, "REPLACE")
    return o


def gem(name, mat, center, axes, radii, bone, subdiv=1):
    """Faceted ellipsoid (an icosphere, scaled along the given axes)."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    x, y, z = axes
    verts = [center + x * (v.co.x * radii[0]) + y * (v.co.y * radii[1]) + z * (v.co.z * radii[2]) for v in bm.verts]
    faces = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return part(name, mat, verts, faces, bone)


def prism(name, mat, a, b, radius_a, radius_b, bone, sides=3):
    """Thin tapered prism from a to b (whiskers)."""
    d = (b - a).normalized()
    ref = UP if abs(d.dot(UP)) < 0.9 else FWD
    u = d.cross(ref).normalized()
    v = d.cross(u).normalized()
    verts = []
    for p, r in ((a, radius_a), (b, radius_b)):
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            verts.append(p + (u * math.cos(ang) + v * math.sin(ang)) * r)
    faces = [[k, (k + 1) % sides, sides + (k + 1) % sides, sides + k] for k in range(sides)]
    faces += [list(range(sides))[::-1], list(range(sides, 2 * sides))]
    return part(name, mat, verts, faces, bone)


parts = []
tip = max(body.data.vertices, key=lambda v: v.co.y).co.copy()
print(f"CHECK nose tip at {tuple(round(c, 3) for c in tip)}")
_, tip_normal = surface_hit(Vector((0, tip.y + 0.2, tip.z)), -FWD)
print(f"CHECK nose normal {tuple(round(c, 2) for c in tip_normal)}")

# Eyes: almond-shaped, slightly tilted (outer corner up), with a slit pupil and a small glint.
EYE_W, EYE_H, EYE_D = 0.0125, 0.0085, 0.0045
for side in (-1, 1):
    loc, n = surface_hit(Vector((side * 0.021, tip.y + 0.2, tip.z + 0.021)), Vector((0, -1, 0)))
    x, y, z = frame_at(n)
    tilt = Matrix.Rotation(math.radians(-14 * side), 3, z)
    x, y = tilt @ x, tilt @ y
    centre = loc - z * EYE_D * 0.35
    parts.append(gem(f"Eye{side}", "Eye", centre, (x, y, z), (EYE_W, EYE_H, EYE_D), "Head", subdiv=2))

    def on_eye(u, v, lift):
        """Point on the eye's ellipsoid surface at (u, v) in eye units (-1..1), raised by lift."""
        h = max(0.0, 1.0 - u * u - v * v)
        return centre + x * (u * EYE_W) + y * (v * EYE_H) + z * (EYE_D * math.sqrt(h) + lift)

    # Slit pupil: a thin vertical lens that follows the curve of the eye.
    ring = [(0.19 * math.sin(a), 0.86 * math.cos(a)) for a in (2 * math.pi * k / 10 for k in range(10))]
    pupil_verts = [on_eye(u, v, 0.0004) for u, v in ring] + [on_eye(0, 0, 0.0008)]
    parts.append(part(f"Pupil{side}", "Pupil", pupil_verts, [[(k + 1) % 10, k, 10] for k in range(10)], "Head",
                      closed=False))
    parts.append(gem(f"Glint{side}", "Glint", on_eye(-0.3 * side, 0.4, 0.0004), (x, y, z),
                     (0.0012, 0.0012, 0.0006), "Head", subdiv=1))

# Nose: a small inverted triangle, slightly raised, on the nose tip.
loc, n = surface_hit(Vector((0, tip.y + 0.2, tip.z + 0.002)), Vector((0, -1, 0)))
x, y, z = frame_at(n)
base = loc - z * 0.0012
nose_verts = [base - x * 0.0075 + y * 0.004, base + x * 0.0075 + y * 0.004, base - y * 0.0055,
              base + y * 0.0005 + z * 0.0032]
parts.append(part("Nose", "Nose", nose_verts, [[0, 1, 3], [1, 2, 3], [2, 0, 3], [0, 2, 1]], "Head"))
nose_bottom = base - y * 0.0055

# Mouth: a thin dark line down from the nose that splits in two (the cat "Y").
split = nose_bottom - y * 0.005
for i, (a, b) in enumerate(((nose_bottom, split),
                            (split, split - y * 0.004 - x * 0.0055),
                            (split, split - y * 0.004 + x * 0.0055))):
    pa, na = surface_hit(Vector((a.x, tip.y + 0.2, a.z)), -FWD)
    pb_, nb = surface_hit(Vector((b.x, tip.y + 0.2, b.z)), -FWD)
    parts.append(prism(f"Mouth{i}", "Pupil", pa + na * 0.0004, pb_ + nb * 0.0004, 0.00045, 0.00035, "Head", sides=3))

# Whiskers: three per side from the muzzle, fanning out and drooping a little.
for side in (-1, 1):
    root, n = surface_hit(Vector((side * 0.08, tip.y - 0.012, tip.z - 0.006)), Vector((-side, 0, 0)))
    for k, (rise, sweep) in enumerate(((0.22, 0.25), (0.0, 0.3), (-0.2, 0.35))):
        d = Vector((side, sweep, rise)).normalized()
        a = root - n * 0.001 + UP * (0.0035 * (1 - k))
        b = a + d * 0.045 - UP * 0.004
        parts.append(prism(f"Whisker{side}_{k}", "Whisker", a, b, 0.00045, 0.00015, "Head"))

# Inner ears: a pink triangle on the front of each ear.
for side, ear in ((-1, "Ear.R"), (1, "Ear.L")):
    gi = body.vertex_groups[ear].index
    ear_vs = [v.co.copy() for v in body.data.vertices
              if any(g.group == gi and g.weight > 0.5 for g in v.groups) and v.normal.y > 0.25]
    if len(ear_vs) < 3:
        print(f"WARNING no front faces found for {ear}")
        continue
    top = max(ear_vs, key=lambda c: c.z)
    low = sorted(ear_vs, key=lambda c: c.z)[: max(3, len(ear_vs) // 3)]
    inner = min(low, key=lambda c: abs(c.x))
    outer = max(low, key=lambda c: abs(c.x))
    centre = (top + inner + outer) / 3
    tri = []
    for c in (inner, outer, top):
        q = centre + (c - centre) * 0.62
        hit, nn = surface_hit(q + Vector((0, 0.05, 0)), Vector((0, -1, 0)))
        tri.append(hit + nn * 0.0008)
    parts.append(part(f"InnerEar{side}", "InnerEar", tri, [[0, 1, 2], [0, 2, 1]], ear, closed=False))

bpy.data.objects.remove(rest_obj, do_unlink=True)
bpy.data.meshes.remove(rest_mesh)
select_only(body, *parts)
bpy.ops.object.join()
body = bpy.context.object

print(f"CHECK triangles {sum(len(p.vertices) - 2 for p in body.data.polygons)}")
print(f"CHECK bones {len(arm.data.bones)} materials {[m.name for m in body.data.materials]}")
print(f"CHECK clips {sorted(a.name for a in bpy.data.actions)}")
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
    shoot(os.path.join(args.renders, "lowpoly_cat_face_front.png"), (0.0, 0.55, 0.22), (0, 0.25, 0.205), lens=120,
          res=(700, 600))
    shoot(os.path.join(args.renders, "lowpoly_cat_face_side.png"), (0.3, 0.45, 0.24), (0, 0.25, 0.205), lens=110,
          res=(700, 600))
    for name, action in (("crouchidle", crouch_idle), ("jump", jump), ("fall", fall)):
        arm.animation_data.action = action
        scene.frame_set(0)
        shoot(os.path.join(args.renders, f"lowpoly_cat_pose_{name}.png"), (0.9, 0.0, 0.2), (0, 0, 0.14),
              res=(500, 330))
    for name, action, frames in (("walk", walk, WALK_FRAMES), ("run", run, run_frames),
                                 ("crouchwalk", crouch_walk, CROUCH_FRAMES)):
        arm.animation_data.action = action
        for i, f in enumerate((0, frames // 4, frames // 2, 3 * frames // 4)):
            scene.frame_set(f)
            shoot(os.path.join(args.renders, f"lowpoly_cat_{name}_{i}.png"), (0.9, 0.0, 0.15), (0, 0, 0.12),
                  res=(500, 330))
