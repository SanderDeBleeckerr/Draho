"""Build Drahomil (placeholder cat): mesh, rig, Idle/Walk/Run, FBX export.

Run headless with Blender's Python module:
    python build_drahomil.py --out <fbx path> [--renders <dir>]

Coordinates: meters, cat faces +Y, Z up. The FBX export maps +Y to Unity +Z.
"""
import argparse
import math
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ap = argparse.ArgumentParser()
ap.add_argument("--out", required=True)
ap.add_argument("--renders")
args = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
FPS = 30
scene.render.fps = FPS
FWD = Vector((0, 1, 0))


# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------
def select_only(*objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def material(name, color, rough, sheen=0.0, spec=0.5):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Sheen Weight"].default_value = sheen
    b.inputs["Specular IOR Level"].default_value = spec
    m.diffuse_color = (*color, 1)  # FBX/Unity reads this
    return m


def sphere(loc, r, scale=(1, 1, 1), segs=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=segs // 2, radius=r, location=loc)
    o = bpy.context.object
    o.scale = scale
    return o


# --------------------------------------------------------------------------
# 1. Body volume: ellipsoid torso + one skin tube per limb, voxel-merged.
#    Each tube is a single chain (no branches), because the skin modifier
#    makes box-shaped hulls at branch vertices.
# --------------------------------------------------------------------------
def tube(name, points):
    me = bpy.data.meshes.new(name)
    me.from_pydata([p for p, _ in points], [(i, i + 1) for i in range(len(points) - 1)], [])
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    ob.modifiers.new("Skin", "SKIN")
    for sv, (_, r) in zip(me.skin_vertices[0].data, points):
        sv.radius = (r, r)
    me.skin_vertices[0].data[0].use_root = True
    ob.modifiers.new("Sub", "SUBSURF").levels = 2
    return ob


TAIL_PTS = [(0, -0.17, 0.198), (0, -0.23, 0.18), (0, -0.29, 0.172),
            (0, -0.335, 0.195), (0, -0.36, 0.24), (0, -0.368, 0.29)]
parts = [tube("Torso", [((0, -0.155, 0.196), 0.05), ((0, -0.11, 0.2), 0.061),
                        ((0, -0.04, 0.203), 0.064), ((0, 0.03, 0.202), 0.063),
                        ((0, 0.09, 0.207), 0.058), ((0, 0.13, 0.222), 0.047),
                        ((0, 0.17, 0.248), 0.04), ((0, 0.2, 0.275), 0.036)]),
         tube("Tail", list(zip(TAIL_PTS, (0.018, 0.016, 0.015, 0.014, 0.012, 0.009))))]
for s in (1, -1):
    x = 0.04 * s
    parts.append(tube("Front", [((x * 0.7, 0.11, 0.195), 0.03), ((x, 0.125, 0.15), 0.026),
                                ((x, 0.133, 0.085), 0.019), ((x, 0.138, 0.022), 0.016),
                                ((x, 0.158, 0.013), 0.0145)]))
    parts.append(tube("Hind", [((x * 0.7, -0.11, 0.19), 0.04), ((x, -0.10, 0.15), 0.036),
                               ((x, -0.07, 0.095), 0.024), ((x, -0.135, 0.048), 0.016),
                               ((x, -0.122, 0.018), 0.0155), ((x, -0.10, 0.013), 0.0145)]))

HEAD_C = Vector((0, 0.215, 0.29))
parts += [sphere(HEAD_C, 0.046, (1.08, 1.0, 0.9)),                       # cranium
         sphere((0, 0.252, 0.274), 0.022, (1.15, 0.85, 0.8)),           # muzzle
         sphere((0.014, 0.248, 0.272), 0.017, (1, 0.9, 0.85)),          # whisker pads
         sphere((-0.014, 0.248, 0.272), 0.017, (1, 0.9, 0.85)),
         sphere((0, 0.238, 0.258), 0.016, (1, 1, 0.7))]                 # chin
for s in (1, -1):
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=0.021, radius2=0.002, depth=0.042,
                                    location=(0.029 * s, 0.207, 0.333))
    ear = bpy.context.object
    ear.scale = (1, 0.42, 1)
    ear.rotation_euler = (math.radians(-12), math.radians(20 * s), math.radians(-12 * s))
    parts.append(ear)

select_only(*parts)
bpy.ops.object.convert(target="MESH")
bpy.ops.object.join()
body = bpy.context.object
body.name = "Drahomil"
body.data.name = "DrahomilMesh"
body.data.transform(body.matrix_world)   # join keeps the first part's scale
body.matrix_world = Matrix.Identity(4)

rem = body.modifiers.new("Remesh", "REMESH")
rem.mode = "VOXEL"
rem.voxel_size = 0.0025
sm = body.modifiers.new("Smooth", "LAPLACIANSMOOTH")
sm.iterations = 12
sm.lambda_factor = 0.6
bpy.ops.object.convert(target="MESH")
dense_tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
dec = body.modifiers.new("Decimate", "DECIMATE")
dec.ratio = 14000 / dense_tris
bpy.ops.object.convert(target="MESH")
for p in body.data.polygons:
    p.use_smooth = True
fur = material("Fur", (0.022, 0.022, 0.026), 0.5, sheen=0.35)
body.data.materials.append(fur)

# --------------------------------------------------------------------------
# 2. Face details placed ON the surface (ray cast), so nothing floats
# --------------------------------------------------------------------------
bvh = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get())


def surface_hit(start, direction):
    hit, normal, _, _ = bvh.ray_cast(Vector(start), Vector(direction).normalized())
    assert hit is not None, f"no surface from {start}"
    return hit, normal


def gap_to_surface(obj):
    """Smallest distance from any vertex of obj to the body surface (0 = touching)."""
    mw = obj.matrix_world
    inside = 0
    best = 1.0
    for v in obj.data.vertices:
        p = mw @ v.co
        loc, _, _, d = bvh.find_nearest(p)
        best = min(best, d)
        # A vertex is inside the head if a ray towards +Y from it hits the surface first
        # from the inside (normal facing along the ray).
        h, n, _, _ = bvh.ray_cast(p, FWD)
        if h is not None and n.dot(FWD) > 0:
            inside += 1
    return best, inside, len(obj.data.vertices)


accessories = []
eye_mat = material("Eye", (0.62, 0.66, 0.05), 0.05, spec=1.0)
pupil_mat = material("Pupil", (0.0, 0.0, 0.0), 0.05, spec=1.0)
nose_mat = material("Nose", (0.06, 0.04, 0.045), 0.35)
inner_ear_mat = material("InnerEar", (0.16, 0.09, 0.09), 0.6)

EYE_R = 0.0092
for s in (1, -1):
    outward = Vector((0.22 * s, 1, 0)).normalized()     # eyes look slightly sideways
    aim = Vector((0.021 * s, 0.25, 0.297))
    hit, n = surface_hit(aim + outward * 0.2, -outward)
    centre = hit - n * (EYE_R * 0.45)                     # 55 % of the eye sticks out
    eye = sphere(centre, EYE_R)
    eye.data.materials.append(eye_mat)
    pupil = sphere(centre + n * (EYE_R * 0.92), EYE_R, (0.22, 0.22, 0.75), segs=16)
    pupil.rotation_euler = n.to_track_quat("Z", "Y").to_euler()
    pupil.scale = (0.22, 0.75, 0.12)
    pupil.data.materials.append(pupil_mat)
    accessories += [eye, pupil]

NOSE_R = 0.0068
hit, n = surface_hit((0, 0.4, 0.284), (0, -1, 0))
nose = sphere(hit - n * (NOSE_R * 0.55), NOSE_R, (1.25, 0.75, 0.75), segs=16)
nose.data.materials.append(nose_mat)
accessories.append(nose)

for s in (1, -1):
    # Inner ear: a thin shell pressed into the front face of the ear.
    hit, n = surface_hit((0.031 * s, 0.4, 0.33), (0, -1, 0))
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.014, radius2=0.001, depth=0.03,
                                    location=hit - n * 0.002)
    inner = bpy.context.object
    inner.scale = (1, 0.18, 1)
    inner.rotation_euler = (math.radians(-12), math.radians(20 * s), math.radians(-12 * s))
    inner.data.materials.append(inner_ear_mat)
    accessories.append(inner)

for a in accessories:
    bpy.context.view_layer.update()
    a.data.transform(a.matrix_world)
    a.matrix_world = Matrix.Identity(4)
    for p in a.data.polygons:
        p.use_smooth = True

gap, inside, total = gap_to_surface(nose)
print(f"CHECK nose: nearest gap to face = {gap * 1000:.2f} mm, "
      f"{inside}/{total} vertices inside the head")
assert inside > 0 and gap < 0.0005, "nose is not attached to the face"
for a, label in ((accessories[0], "eye R"), (accessories[2], "eye L")):
    g, i, t = gap_to_surface(a)
    print(f"CHECK {label}: gap {g * 1000:.2f} mm, {i}/{t} vertices inside")
    assert i > 0, f"{label} floats"

# --------------------------------------------------------------------------
# 3. Armature
# --------------------------------------------------------------------------
arm_data = bpy.data.armatures.new("DrahomilRig")
rig = bpy.data.objects.new("DrahomilRig", arm_data)
scene.collection.objects.link(rig)
select_only(rig)
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones
UP, FWD_V = Vector((0, 0, 1)), Vector((0, 1, 0))


def bone(name, head, tail, parent=None, roll_ref=None, connect=False):
    b = eb.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    d = (b.tail - b.head).normalized()
    # Make every bone's local X axis = world +X, so X rotation = pitch for all bones.
    ref = roll_ref or (UP if abs(d.z) < 0.7 else (FWD_V if d.z < 0 else -FWD_V))
    b.align_roll(ref)
    if parent:
        b.parent = eb[parent]
        b.use_connect = connect
    return b


bone("Root", (0, 0, 0), (0, 0.08, 0))
bone("Hips", (0, -0.03, 0.208), (0, -0.12, 0.198), "Root")
bone("Spine", (0, -0.03, 0.208), (0, 0.05, 0.206), "Root")
bone("Chest", (0, 0.05, 0.206), (0, 0.12, 0.208), "Spine", connect=True)
bone("Neck", (0, 0.12, 0.208), (0, 0.18, 0.255), "Chest", connect=True)
bone("Head", (0, 0.18, 0.255), (0, 0.285, 0.285), "Neck", connect=True)
prev = "Hips"
for i in range(len(TAIL_PTS) - 1):
    bone(f"Tail{i + 1}", TAIL_PTS[i], TAIL_PTS[i + 1], prev, connect=i > 0)
    prev = f"Tail{i + 1}"
for side, s in (("L", -1), ("R", 1)):   # cat's left is -X while facing +Y
    x = 0.04 * s
    bone(f"UpperArm.{side}", (x, 0.12, 0.165), (x, 0.133, 0.085), "Chest")
    bone(f"Forearm.{side}", (x, 0.133, 0.085), (x, 0.138, 0.022), f"UpperArm.{side}", connect=True)
    bone(f"Hand.{side}", (x, 0.138, 0.022), (x, 0.162, 0.012), f"Forearm.{side}", connect=True,
         roll_ref=UP)
    bone(f"Thigh.{side}", (x, -0.105, 0.165), (x, -0.07, 0.095), "Hips")
    bone(f"Shin.{side}", (x, -0.07, 0.095), (x, -0.135, 0.048), f"Thigh.{side}", connect=True)
    bone(f"Foot.{side}", (x, -0.135, 0.048), (x, -0.122, 0.018), f"Shin.{side}", connect=True)
    bone(f"Toe.{side}", (x, -0.122, 0.018), (x, -0.098, 0.012), f"Foot.{side}", connect=True,
         roll_ref=UP)
bpy.ops.object.mode_set(mode="OBJECT")

select_only(rig, body)
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
empty_groups = [g.name for g in body.vertex_groups
                if not any(g.index in [x.group for x in v.groups] for v in body.data.vertices)]
print("CHECK weights: empty groups ->", empty_groups or "none")

for a in accessories:
    g = a.vertex_groups.new(name="Head")
    g.add(range(len(a.data.vertices)), 1.0, "REPLACE")
select_only(body, *accessories)
bpy.ops.object.join()

# --------------------------------------------------------------------------
# 4. Animations (in place, bone-local rotations; +X rotation = limb swings forward)
# --------------------------------------------------------------------------
rig.animation_data_create()
pb = rig.pose.bones
for p in pb:
    p.rotation_mode = "XYZ"


def smooth01(t):
    return 0.5 - 0.5 * math.cos(math.pi * t)


def leg_cycle(phase, duty, amp):
    """Returns (swing angle, lift 0..1). Stance: foot goes back. Swing: foot comes forward."""
    phase %= 1.0
    if phase < duty:
        return amp - 2 * amp * (phase / duty), 0.0
    t = (phase - duty) / (1 - duty)
    return -amp + 2 * amp * smooth01(t), math.sin(math.pi * t)


def make_action(name, frames, pose_fn):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    act.use_frame_range = True
    act.frame_start, act.frame_end = 0, frames
    rig.animation_data.action = act
    for f in range(frames + 1):
        t = f / frames
        for p in pb:
            p.rotation_euler = (0, 0, 0)
            p.location = (0, 0, 0)
        pose_fn(t)
        for p in pb:
            p.keyframe_insert("rotation_euler", frame=f)
        pb["Root"].keyframe_insert("location", frame=f)
    return act


def tail_wave(t, amp_side, amp_lift, freq=1.0, lag=0.18):
    for i in range(1, len(TAIL_PTS)):
        k = i / (len(TAIL_PTS) - 1)
        b = pb[f"Tail{i}"]
        b.rotation_euler.z = amp_side * k * math.sin(2 * math.pi * (freq * t - lag * i))
        b.rotation_euler.x += amp_lift * k


def gait(t, duty, amp_f, amp_h, phases, bob, spine_flex, tail):
    for side in ("L", "R"):
        a, lift = leg_cycle(t + phases[f"F{side}"], duty, amp_f)
        pb[f"UpperArm.{side}"].rotation_euler.x = a + 0.15 * lift
        pb[f"Forearm.{side}"].rotation_euler.x = -1.1 * lift
        pb[f"Hand.{side}"].rotation_euler.x = -0.9 * lift + 0.25 * max(0.0, -a)
        a, lift = leg_cycle(t + phases[f"H{side}"], duty, amp_h)
        pb[f"Thigh.{side}"].rotation_euler.x = a + 0.35 * lift
        pb[f"Shin.{side}"].rotation_euler.x = -0.55 * lift
        pb[f"Foot.{side}"].rotation_euler.x = 0.9 * lift
        pb[f"Toe.{side}"].rotation_euler.x = -0.5 * lift + 0.3 * max(0.0, a)
    w = 2 * math.pi * t
    pb["Root"].location.z = bob * math.cos(2 * w)
    pb["Spine"].rotation_euler.x = spine_flex * math.sin(w)
    pb["Hips"].rotation_euler.x = -spine_flex * math.sin(w)
    pb["Spine"].rotation_euler.z = 0.04 * math.sin(w)
    pb["Neck"].rotation_euler.x = -0.03 * math.cos(2 * w)
    pb["Head"].rotation_euler.x = 0.03 * math.cos(2 * w)
    tail_wave(t, *tail)


def idle(t):
    w = 2 * math.pi * t
    pb["Chest"].rotation_euler.x = 0.012 * math.sin(2 * w)          # breathing
    pb["Neck"].rotation_euler.z = 0.18 * math.sin(w)                 # look around
    pb["Head"].rotation_euler.z = 0.10 * math.sin(w - 0.6)
    pb["Head"].rotation_euler.x = 0.05 * math.sin(2 * w)
    tail_wave(t, 0.28, 0.0, freq=1.0)


WALK = dict(HL=0.0, FL=0.25, HR=0.5, FR=0.75)          # lateral-sequence walk
RUN = dict(HL=0.0, HR=0.1, FL=0.5, FR=0.6)              # bound / gallop
acts = [make_action("Idle", 120, idle),
        make_action("Walk", 27, lambda t: gait(t, 0.62, 0.6, 0.6, WALK, 0.003, 0.02, (0.12, 0.05))),
        make_action("Run", 12, lambda t: gait(t, 0.38, 0.75, 0.8, RUN, 0.008, 0.16, (0.08, -0.25)))]
for act in acts:
    track = rig.animation_data.nla_tracks.new()
    track.name = act.name
    track.strips.new(act.name, 0, act)
    track.mute = True
rig.animation_data.action = None

body = bpy.data.objects["Drahomil"]
tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
print(f"CHECK stats: {tris} triangles, {len(rig.data.bones)} bones, "
      f"{len(body.data.materials)} materials")

# --------------------------------------------------------------------------
# 5. Export FBX for Unity (+Y forward -> Unity +Z, meters, no leaf bones)
# --------------------------------------------------------------------------
select_only(rig, body)
body.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=args.out, use_selection=True, object_types={"ARMATURE", "MESH"},
    axis_forward="Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, mesh_smooth_type="FACE")
print("EXPORTED", args.out)

# --------------------------------------------------------------------------
# 6. Preview renders
# --------------------------------------------------------------------------
if args.renders:
    import os
    os.makedirs(args.renders, exist_ok=True)
    bpy.ops.mesh.primitive_plane_add(size=8)
    bpy.context.object.data.materials.append(material("Floor", (0.6, 0.56, 0.5), 0.7))
    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.4, 0.42, 0.47, 1)
    bg.inputs[1].default_value = 0.7
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    scene.collection.objects.link(sun)
    rim = bpy.data.objects.new("Rim", bpy.data.lights.new("Rim", "AREA"))
    rim.data.energy = 40
    rim.data.size = 0.8
    rim.location = (-0.5, -0.6, 0.7)
    rim.rotation_euler = (Vector((0, 0, 0.2)) - rim.location).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(rim)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.view_settings.view_transform = "AgX"
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam

    def shoot(path, loc, target, lens=50, res=(900, 600)):
        cam.data.lens = lens
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.resolution_x, scene.render.resolution_y = res
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)

    rig.animation_data.action = bpy.data.actions["Idle"]
    scene.frame_set(0)
    if os.environ.get("DRAHO_REST"):  # debug: show the undeformed mesh
        body.modifiers["Armature"].show_render = False
    shoot(f"{args.renders}/still_threequarter.png", (0.85, 0.8, 0.45), (0, 0, 0.17))
    shoot(f"{args.renders}/head_profile.png", (0.42, 0.25, 0.29), (0, 0.25, 0.285), lens=85,
          res=(600, 600))
    shoot(f"{args.renders}/head_front.png", (0.12, 0.62, 0.32), (0, 0.24, 0.29), lens=85,
          res=(600, 600))
    for act, n in (("Walk", 27), ("Run", 12)):
        rig.animation_data.action = bpy.data.actions[act]
        for i, f in enumerate(range(0, n, n // 6)):
            scene.frame_set(f)
            shoot(f"{args.renders}/{act.lower()}_{i}.png", (1.3, 0.0, 0.22), (0, -0.02, 0.15),
                  res=(450, 300))
