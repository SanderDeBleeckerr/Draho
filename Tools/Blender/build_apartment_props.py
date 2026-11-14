"""Apartment furniture in the Draho house style (docs/ART_STYLE.md): bevelled hard surfaces, palette UVs, one material.

Each piece replaces a Kenney model or a code-built box in ApartmentBuilder: Unity loads Art/Furniture/<id>.fbx for a layout
prop with that id, gives it the shared DrahoPalette material and a collider shaped from the mesh.

Sizes are real (metres) and match apartment_layout.json. The front of each piece faces Blender -Y (Unity +Z after
the FBX axis conversion), the origin is on the floor in the middle of the footprint.

Run it in a live Blender (the Blender MCP: exec this file) or headless:
    blender -b --factory-startup --python Tools/Blender/build_apartment_props.py -- --out Draho/Assets/_Draho/Art/Furniture
    [--only sofa] [--renders <dir>]
"""
import argparse
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draho_style as ds  # noqa: E402

PROPS = {}


def prop(fn):
    PROPS[fn.__name__] = fn
    return fn


def collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


def join(name, parts):
    """One mesh, origin on the floor at the footprint's middle, transforms applied."""
    for o in parts:
        o.select_set(False)
    with bpy.context.temp_override(active_object=parts[0], selected_editable_objects=parts, object=parts[0]):
        bpy.ops.object.join()
    o = parts[0]
    o.name = o.data.name = name
    with bpy.context.temp_override(active_object=o, selected_editable_objects=[o], object=o):
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    exp = collection("COL_Export")
    for c in o.users_collection:
        c.objects.unlink(o)
    exp.objects.link(o)
    return o


# ---------------------------------------------------------------------------
# The pieces
# ---------------------------------------------------------------------------
@prop
def sofa():
    """2.2 x 0.9 x 0.85 m. Seat top 0.45 m (the cat lands on the cushions), arms 0.68 m, back 0.85 m."""
    W, D, ARM, SEAT_TOP = 2.2, 0.9, 0.2, 0.45
    parts = [
        ds.box("Base", (0, 0.02, 0.21), (W - 2 * ARM + 0.02, D - 0.06, 0.26), "teal_dark", bevel=0.03),
        ds.box("Back", (0, D / 2 - 0.09, 0.44), (W - 2 * ARM + 0.02, 0.16, 0.68), "teal_dark", bevel=0.05),
    ]
    for s in (-1, 1):  # padded arms with rounded tops
        parts.append(ds.box("Arm", (s * (W / 2 - ARM / 2), 0, 0.34), (ARM, D, 0.68 - 0.0), "teal_dark", bevel=0.07))
    cw = (W - 2 * ARM) / 2 - 0.01
    for s in (-1, 1):  # two seat cushions with a small gap
        parts.append(ds.box("SeatCushion", (s * (cw / 2 + 0.005), -0.06, SEAT_TOP - 0.065), (cw, D - 0.22, 0.13),
                            "teal", bevel=0.05))
    for s in (-1, 1):  # two back cushions, leaning back 12 degrees
        o = ds.box("BackCushion", (0, 0, 0), (cw, 0.18, 0.38), "teal", bevel=0.06)
        o.rotation_euler = (math.radians(-12), 0, 0)
        o.location = (s * (cw / 2 + 0.005), D / 2 - 0.25, SEAT_TOP + 0.19)
        parts.append(o)
    for sx in (-1, 1):  # short tapered walnut feet
        for sy in (-1, 1):
            parts.append(ds.cylinder("Leg", (sx * (W / 2 - 0.08), sy * (D / 2 - 0.08), 0), 0.03, 0.08, "walnut_dark",
                                     segments=8, radius_top=0.024, bevel=0.004))
    return parts


@prop
def coffee_table():
    """1.0 x 1.0 x 0.45 m walnut table: a thick bevelled top, square legs with a slight taper, a lower shelf
    (the cat can hide under it: the collider follows the shape)."""
    S, H, TOP = 1.0, 0.45, 0.045
    parts = [ds.box("Top", (0, 0, H - TOP / 2), (S, S, TOP), "walnut", bevel=0.012),
             ds.box("Shelf", (0, 0, 0.12), (S - 0.16, S - 0.16, 0.025), "walnut", bevel=0.006)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            leg = ds.cylinder("Leg", (sx * (S / 2 - 0.06), sy * (S / 2 - 0.06), 0), 0.03, H - TOP, "walnut_dark",
                              segments=4, radius_top=0.036, bevel=0.004)
            leg.rotation_euler.z = math.radians(45)  # 4 segments turned 45 degrees: a square leg
            parts.append(leg)
    for s in (-1, 1):  # rails under the top
        parts.append(ds.box("Rail", (0, s * (S / 2 - 0.06), H - TOP - 0.03), (S - 0.16, 0.025, 0.06), "walnut_dark", bevel=0.005))
        parts.append(ds.box("Rail", (s * (S / 2 - 0.06), 0, H - TOP - 0.03), (0.025, S - 0.16, 0.06), "walnut_dark", bevel=0.005))
    return parts


@prop
def cat_tree():
    """0.45 x 0.45 x 0.75 m: a carpeted base box with a round entrance, a sisal post, a platform with a raised rim."""
    S, H = 0.45, 0.75
    parts = [ds.box("Base", (0, 0, 0.15), (S, S, 0.3), "navy", bevel=0.025),
             ds.cylinder("Hole", (0, -S / 2 + 0.004, 0.06), 0.09, 0.006, "black", segments=16, bevel=0)]
    parts[-1].rotation_euler.x = math.radians(90)
    parts[-1].location = (0, -S / 2 - 0.002, 0.14)
    parts.append(ds.cylinder("Post", (0, 0, 0.3), 0.055, H - 0.34, "mustard", segments=12, bevel=0.004))
    for i in range(6):  # sisal rope rings
        parts.append(ds.cylinder("Rope", (0, 0, 0.33 + i * 0.065), 0.06, 0.012, "oak_light", segments=12, bevel=0.003))
    parts.append(ds.box("Platform", (0, 0, H - 0.025), (S, S, 0.05), "navy", bevel=0.02))
    for s in (-1, 1):
        parts.append(ds.box("Rim", (0, s * (S / 2 - 0.02), H + 0.025), (S, 0.04, 0.05), "navy", bevel=0.015))
    parts.append(ds.cylinder("ToyString", (S / 2 - 0.04, -S / 2 + 0.04, H - 0.32), 0.003, 0.3, "linen", segments=4, bevel=0))
    parts.append(ds.blob("Toy", (S / 2 - 0.04, -S / 2 + 0.04, H - 0.34), 0.03, (1, 1, 0.9), "red", subdiv=1, seed=3))
    return parts


@prop
def radiator():
    """0.9 x 0.12 x 0.6 m panel radiator: vertical ribs, a top grille, two brackets and a valve. The layout hangs it
    0.15 m above the floor (its "y"), so the origin is the bottom of the radiator and the brackets reach down."""
    W, D, H, Z0 = 0.9, 0.12, 0.72, -0.06
    parts = [ds.box("Panel", (0, 0, Z0 + H / 2 - 0.06), (W, D * 0.55, H - 0.12), "white", bevel=0.01)]
    n = 14
    for i in range(n):  # ribs on the room side
        x = -W / 2 + 0.04 + i * (W - 0.08) / (n - 1)
        parts.append(ds.box("Rib", (x, -D * 0.3, Z0 + H / 2 - 0.06), (0.035, 0.02, H - 0.14), "white", bevel=0.008))
    parts.append(ds.box("Grille", (0, 0, Z0 + H - 0.115), (W - 0.02, D * 0.8, 0.012), "grey_light", bevel=0.003))
    for s in (-1, 1):
        parts.append(ds.box("Bracket", (s * (W / 2 - 0.12), D * 0.25, 0.08), (0.04, 0.05, 0.16), "grey_mid", bevel=0.004))
    parts.append(ds.cylinder("Valve", (W / 2 + 0.03, 0, Z0 - 0.02), 0.02, 0.08, "metal", segments=10, bevel=0.003))
    return parts


@prop
def poster():
    """1.0 x 0.03 x 0.7 m framed print on the wall (hung at the layout's height): a walnut frame, an off-white mat
    and a calm blue-hour picture in palette blocks (roofs, a lit window, the moon)."""
    W, D, H = 1.0, 0.03, 0.7
    F = 0.035
    parts = []
    for s in (-1, 1):
        parts.append(ds.box("Frame", (s * (W / 2 - F / 2), 0, H / 2), (F, D, H), "walnut_dark", bevel=0.006))
        parts.append(ds.box("Frame", (0, 0, H / 2 + s * (H / 2 - F / 2)), (W, D, F), "walnut_dark", bevel=0.006))
    parts.append(ds.box("Mat", (0, 0.004, H / 2), (W - 2 * F, 0.008, H - 2 * F), "off_white", bevel=0))
    iw, ih, y = W - 2 * F - 0.12, H - 2 * F - 0.12, -0.001
    parts.append(ds.box("Sky", (0, y, H / 2 + ih * 0.15), (iw, 0.006, ih * 0.7), "muted_blue", bevel=0))
    parts.append(ds.box("Town", (0, y - 0.002, H / 2 - ih * 0.3), (iw, 0.006, ih * 0.4), "deep_blue_grey", bevel=0))
    for i, (x, w, h) in enumerate(((-0.28, 0.16, 0.2), (-0.08, 0.14, 0.26), (0.12, 0.18, 0.18), (0.3, 0.1, 0.24))):
        parts.append(ds.box("House", (x, y - 0.004, H / 2 - ih / 2 + h / 2), (w, 0.006, h), "slate", bevel=0))
    parts.append(ds.box("Window", (-0.06, y - 0.006, H / 2 - ih / 2 + 0.16), (0.03, 0.006, 0.04), "pale_yellow", bevel=0))
    moon = ds.cylinder("Moon", (0.22, y - 0.004, H / 2 + ih * 0.28), 0.035, 0.006, "pale_yellow", segments=16, bevel=0)
    moon.rotation_euler.x = math.radians(90)
    moon.location = (0.22, y - 0.004, H / 2 + ih * 0.28)
    parts.append(moon)
    return parts


@prop
def rug():
    """2.4 x 1.7 m, 1 cm thick wool rug: a dusty rose field, a linen border and a navy inner line, fringes on the
    short sides. Walk-over (no collider in Unity: the layout height is 0.01)."""
    W, D, T = 2.4, 1.7, 0.012
    # The layers step up 3 mm each: 0.6 mm apart they z-fought on a phone's depth buffer (the rug flashed as the
    # camera moved). Each layer is a slab on top of the one below, not overlapping it.
    parts = [ds.box("Border", (0, 0, T / 2), (W, D, T), "linen", bevel=0.004),
             ds.box("Line", (0, 0, T + 0.0015), (W - 0.2, D - 0.2, 0.003), "navy", bevel=0),
             ds.box("Field", (0, 0, T + 0.0045), (W - 0.28, D - 0.28, 0.003), "rose", bevel=0)]
    for s in (-1, 1):
        for i in range(24):
            z = -D / 2 + 0.06 + i * (D - 0.12) / 23
            parts.append(ds.box("Fringe", (s * (W / 2 + 0.025), z, 0.002), (0.05, 0.008, 0.003), "linen", bevel=0))
    return parts


@prop
def dining_bar():
    """1.4 x 1.0 m footprint, 1.05 m high: a walnut high table against the wall (back, Blender +Y) and two bar
    stools in front (Blender -Y) with round seats at 0.72 m, a low back and a foot ring. The cat can hop stool ->
    table: the collider follows the shape."""
    W, H, TD = 1.4, 1.05, 0.5
    ty = 0.5 - TD / 2                                                        # the table's centre line (y)
    parts = [ds.box("Top", (0, ty, H - 0.02), (W, TD, 0.04), "walnut", bevel=0.01)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(ds.box("Leg", (sx * (W / 2 - 0.05), ty + sy * (TD / 2 - 0.05), (H - 0.04) / 2),
                                (0.045, 0.045, H - 0.04), "walnut_dark", bevel=0.005))
    parts.append(ds.box("FootBar", (0, ty - TD / 2 + 0.05, 0.25), (W - 0.1, 0.03, 0.03), "metal_dark", bevel=0.004))
    parts.append(ds.box("Apron", (0, ty + TD / 2 - 0.05, H - 0.09), (W - 0.1, 0.025, 0.1), "walnut_dark", bevel=0.004))
    SEAT = 0.72
    for sx in (-0.35, 0.35):
        sy = -0.22
        parts.append(ds.cylinder("Seat", (sx, sy, SEAT - 0.04), 0.18, 0.05, "teal_dark", segments=16, bevel=0.012))
        for k in range(4):                                                   # four straight legs under the seat
            a = math.radians(45 + 90 * k)
            parts.append(ds.cylinder("StoolLeg", (sx + math.cos(a) * 0.12, sy + math.sin(a) * 0.12, 0), 0.014, SEAT - 0.04,
                                     "metal_dark", segments=6, radius_top=0.012, bevel=0))
        parts.append(ds.cylinder("FootRing", (sx, sy, 0.26), 0.135, 0.015, "metal_dark", segments=16, radius_top=0.135, bevel=0))
        parts.append(ds.box("StoolBack", (sx, sy - 0.15, SEAT + 0.14), (0.3, 0.03, 0.12), "walnut", bevel=0.008))
        for s in (-1, 1):
            parts.append(ds.cylinder("BackPost", (sx + s * 0.12, sy - 0.15, SEAT), 0.01, 0.1, "metal_dark", segments=6, bevel=0))
    return parts


@prop
def bedroom_curtain():
    """0.8 m wide, 2.3 m long linen curtain seen through the bedroom window: drawn to one side (pleats bunched on
    the right, a dark gap on the left), a slim rail on top. Front faces Blender -Y."""
    import bmesh
    W, H, GAP = 0.8, 2.3, 0.18
    bm = bmesh.new()
    cols, rows = 40, 6
    x0, x1 = -W / 2 + GAP, W / 2
    for j in range(rows + 1):
        z = H * j / rows
        for i in range(cols + 1):
            u = i / cols
            x = x0 + (x1 - x0) * u
            bunch = 0.6 + 0.8 * u                                           # deeper pleats where it is bunched
            # Six soft folds: more and steeper folds read as a ribbed wooden panel behind the glass.
            y = 0.03 * bunch * math.sin(u * math.pi * 6) * (0.85 + 0.15 * z / H)
            bm.verts.new((x, y, z))
    bm.verts.ensure_lookup_table()
    for j in range(rows):
        for i in range(cols):
            a = j * (cols + 1) + i
            bm.faces.new((bm.verts[a], bm.verts[a + 1], bm.verts[a + cols + 2], bm.verts[a + cols + 1]))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)
    me = bpy.data.meshes.new("Curtain")
    bm.to_mesh(me)
    bm.free()
    cloth = bpy.data.objects.new("Curtain", me)
    bpy.context.scene.collection.objects.link(cloth)
    for p in me.polygons:
        p.use_smooth = True
    ds.paint(cloth, "off_white")
    rail = ds.cylinder("Rail", (-W / 2, 0.02, H + 0.02), 0.008, W, "metal", segments=6, bevel=0)
    rail.rotation_euler.y = math.radians(90)
    rail.location = (-W / 2, 0.02, H + 0.02)
    return [cloth, rail]


@prop
def court_pebbles():
    """The inner court's round stones with real height: a 3.0 x 1.8 m bed of about 430 half-buried, flattened
    pebbles (11 cm apart, 2-3.5 cm high) in warm greys. Low-poly domes (8 sides, 2 rings); walk-over."""
    import random
    import bmesh
    rnd = random.Random(42)
    W, D, STEP = 3.0, 1.8, 0.11
    greys = ("grey_light", "warm_grey", "stone", "grey_mid", "mortar", "grey_stone")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    nx, ny = int(W / STEP), int(D / STEP)
    for j in range(ny):
        for i in range(nx):
            cx = -W / 2 + STEP * (i + 0.5 + (0.5 if j % 2 else 0) * 0.5) + rnd.uniform(-0.018, 0.018)
            cy = -D / 2 + STEP * (j + 0.5) + rnd.uniform(-0.018, 0.018)
            if abs(cx) > W / 2 - 0.04 or abs(cy) > D / 2 - 0.04:
                continue
            rx, ry = rnd.uniform(0.042, 0.056), rnd.uniform(0.036, 0.05)
            h = rnd.uniform(0.02, 0.035)
            rot = rnd.uniform(0, math.pi)
            colour = ds.cell_uv(rnd.choice(greys))
            ring = []
            for r_frac, z_frac in ((1.0, 0.0), (0.72, 0.75)):
                ring.append([])
                for k in range(8):
                    a = rot + k * math.pi / 4
                    x, y = math.cos(a) * rx * r_frac, math.sin(a) * ry * r_frac
                    ring[-1].append(bm.verts.new((cx + x, cy + y, h * z_frac)))
            top = bm.verts.new((cx, cy, h))
            faces = []
            for k in range(8):
                k1 = (k + 1) % 8
                faces.append(bm.faces.new((ring[0][k], ring[0][k1], ring[1][k1], ring[1][k])))
                faces.append(bm.faces.new((ring[1][k], ring[1][k1], top)))
            for f in faces:
                f.smooth = True
                for loop in f.loops:
                    loop[uv].uv = colour
    me = bpy.data.meshes.new("Pebbles")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("Pebbles", me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(ds.palette_material())
    return [o]


# ---------------------------------------------------------------------------
def build(names=None):
    built = {}
    for name, fn in PROPS.items():
        if names and name not in names:
            continue
        old = bpy.data.objects.get(name)
        if old:
            bpy.data.objects.remove(old)
        built[name] = join(name, fn())
    return built


def export(obj, out_dir):
    """Same FBX settings as the animals (docs/ART_STYLE.md §7b): Blender's own axes, Unity bakes the conversion."""
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, f"{obj.name}.fbx")
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, axis_forward="-Y",
                             axis_up="Z", apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, bake_space_transform=False)
    return path


if __name__ == "__main__" and "--" in sys.argv:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--only", nargs="*")
    args = ap.parse_args(sys.argv[sys.argv.index("--") + 1:])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for o, path in ((o, export(o, args.out)) for o in build(args.only).values()):
        print(f"EXPORTED {path}: {sum(len(p.vertices) - 2 for p in o.data.polygons)} triangles")
