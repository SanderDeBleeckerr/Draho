"""Draho's animals in the house style (docs/ART_STYLE.md): faceted low-poly, palette colours, one material.

Each animal keeps the parts that its game script animates (the names match what the builders wire into
Configure), so the scripts do not change:
    mouse:  Mouse (root, on the ground, facing -Y) > Body > [mesh] and TailPivot > Tail

Usage (Blender 5.x; absolute paths on Windows):
    blender -b --factory-startup --python Tools/Blender/build_animals.py -- \
        --animal mouse --out Draho/Assets/_Draho/Art/Animals/Mouse.fbx [--blend D:/tmp/mouse.blend]

Axes: Blender front is -Y, up +Z. The FBX has axis_forward -Z, up Y; Unity bakes the axis conversion on import
(Editor/AnimalModels.cs), so in Unity the animal faces +Z with up +Y, as the game scripts expect.
"""
import argparse
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draho_style as ds  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--animal", required=True, choices=["mouse"])
ap.add_argument("--out", required=True)
ap.add_argument("--blend")
args = ap.parse_args(argv)


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------
def empty(name, loc=(0, 0, 0), parent=None):
    o = bpy.data.objects.new(name, None)
    o.empty_display_size = 0.02
    o.location = loc
    bpy.context.scene.collection.objects.link(o)
    if parent:
        o.parent = parent
    return o


def mesh_obj(name, bm, parent=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if parent:
        o.parent = parent
    return o


def loft(bm, rings, segments=8, width=1.0, height=1.0, cap=True):
    """A tube through rings [(y, z, radius)], elliptic cross-section, closed with fans at both ends.
    Returns the vertex rings. Faces get the palette colour later (by the 'colour' face layer)."""
    vrings = []
    for (y, z, r) in rings:
        ring = []
        for k in range(segments):
            a = (k + 0.5) / segments * math.tau
            ring.append(bm.verts.new((math.cos(a) * r * width, y, z + math.sin(a) * r * height)))
        vrings.append(ring)
    for i in range(len(vrings) - 1):
        a, b = vrings[i], vrings[i + 1]
        for k in range(segments):
            bm.faces.new((a[k], a[(k + 1) % segments], b[(k + 1) % segments], b[k]))
    if cap:
        for ring, rev in ((vrings[0], True), (vrings[-1], False)):
            c = sum((v.co for v in ring), Vector()) / len(ring)
            tip = bm.verts.new(c)
            for k in range(segments):
                f = (ring[k], ring[(k + 1) % segments], tip)
                bm.faces.new(f[::-1] if rev else f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return vrings


def blob(bm, center, radius, scale=(1, 1, 1), subdiv=1):
    res = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    for v in res["verts"]:
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])) + Vector(center)
    return set(f for v in res["verts"] for f in v.link_faces)


def paint_faces(obj, colours):
    """colours: list of (face_index_set or None for 'the rest', palette name)."""
    lookup = {}
    rest = "warm_grey"
    for faces, name in colours:
        if faces is None:
            rest = name
        else:
            for i in faces:
                lookup[i] = name
    ds.paint(obj, lambda p: lookup.get(p.index, rest))
    ds.facet(obj)


# ---------------------------------------------------------------------------
# the mouse (real size: body 9 cm, tail 11 cm; the game's CharacterController is 7.5 cm tall)
# ---------------------------------------------------------------------------
def mouse():
    root = empty("Mouse")
    body_pivot = empty("Body", parent=root)

    bm = bmesh.new()
    # rump (+Y, back) to snout (-Y, front): one pear-shaped loft, so the head flows out of the body
    loft(bm, [
        (0.050, 0.026, 0.006),
        (0.042, 0.029, 0.022),
        (0.022, 0.031, 0.029),
        (-0.004, 0.032, 0.027),
        (-0.028, 0.035, 0.021),   # neck
        (-0.046, 0.037, 0.019),   # head
        (-0.066, 0.033, 0.011),
        (-0.080, 0.030, 0.004),   # snout
    ], segments=8, width=0.95, height=0.9)
    bm.faces.index_update()
    # belly a shade lighter: the lowest faces of the loft
    belly = {f.index for f in bm.faces if f.calc_center_median().z < 0.016}
    ears = set()
    for side in (-1, 1):
        # big round mouse ears: flat faceted discs, standing up and turned a little outwards
        faces = blob(bm, (0, 0, 0), 0.0155, scale=(1.0, 0.25, 0.95), subdiv=1)
        verts = {v for f in faces for v in f.verts}
        bmesh.ops.rotate(bm, verts=list(verts), cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-28 * side), 3, "Z"))
        bmesh.ops.translate(bm, verts=list(verts), vec=(side * 0.018, -0.038, 0.059))
        bm.faces.index_update()
        ears |= {f.index for f in faces}
    eyes = set()
    for side in (-1, 1):
        fs = blob(bm, (side * 0.0125, -0.062, 0.043), 0.0042, subdiv=1)
        bm.faces.index_update()
        eyes |= {f.index for f in fs}
    fs = blob(bm, (0, -0.0815, 0.030), 0.0042, subdiv=1)
    bm.faces.index_update()
    nose = {f.index for f in fs}
    feet = set()
    for (x, y, z) in ((-0.013, -0.024, 0.0125), (0.013, -0.024, 0.0125), (-0.017, 0.028, 0.0075), (0.017, 0.028, 0.0075)):
        fs = blob(bm, (x, y, z), 0.008, scale=(0.8, 1.4, 0.95 + (z - 0.0075) * 80), subdiv=1)  # tucked into the belly
        bm.faces.index_update()
        feet |= {f.index for f in fs}
    bm.faces.index_update()
    body = mesh_obj("Mouse_Body", bm, parent=body_pivot)
    paint_faces(body, [(belly, "grey_light"), (ears, "cat_inner_ear"), (eyes, "charcoal"),
                       (nose, "rose"), (feet, "cat_inner_ear"), (None, "grey_stone")])

    # tail: pivot at the base (the script swings it round its up axis), curving back and down to the ground
    tail_pivot = empty("TailPivot", loc=(0, 0.040, 0.018), parent=body_pivot)
    bm = bmesh.new()
    rings = []
    for i in range(7):
        t = i / 6
        rings.append((0.004 + t * 0.105, -0.010 * math.sin(t * math.pi * 0.9) - t * 0.012 + 0.0, 0.0036 * (1 - t) + 0.0012))
    loft(bm, rings, segments=4)
    tail = mesh_obj("Tail", bm, parent=tail_pivot)
    paint_faces(tail, [(None, "cat_inner_ear")])
    return root


builders = {"mouse": mouse}
bpy.ops.wm.read_factory_settings(use_empty=True)
root = builders[args.animal]()
bpy.context.view_layer.update()

meshes = [o for o in root.children_recursive if o.type == "MESH"]
tris = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 3) for i in range(3)]
print(f"CHECK {args.animal}: {len(meshes)} meshes, {tris} triangles, size x {size[0]} y {size[1]} z {size[2]} m, "
      f"lowest z {round(min(p.z for p in pts), 4)}")

os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for o in [root] + list(root.children_recursive):
    o.select_set(True)
bpy.context.view_layer.objects.active = root
# FBX for Unity, -Z forward, Y up, WITHOUT "apply transform": that option converts the meshes but not the pivots
# (empties), which stood the mouse on its nose. Unity converts the axes of the whole hierarchy on import instead
# (ModelImporter.bakeAxisConversion, set in Editor/AnimalModels.cs).
bpy.ops.export_scene.fbx(
    filepath=os.path.abspath(args.out), use_selection=True, object_types={"MESH", "EMPTY"},
    use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True, use_tspace=False,
    apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False, axis_forward="-Y", axis_up="Z",
    bake_anim=False, use_custom_props=False, path_mode="STRIP")
print("EXPORTED", os.path.abspath(args.out), os.path.getsize(os.path.abspath(args.out)), "bytes")
if args.blend:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
