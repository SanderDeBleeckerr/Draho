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
    # The base is a real hiding box: hollow inside, a round entrance in the front (booleans; the cutters' faces
    # keep their dark palette colour, so the inside reads as shade).
    base = ds.box("Base", (0, 0, 0.15), (S, S, 0.3), "navy", bevel=0.025)
    cavity = ds.box("Cavity", (0, 0.01, 0.16), (S - 0.05, S - 0.07, 0.24), "black", bevel=0)
    hole = ds.cylinder("Hole", (0, 0, 0), 0.085, 0.2, "black", segments=20, bevel=0)
    hole.rotation_euler.x = math.radians(90)
    hole.location = (0, -S / 2 + 0.08, 0.14)
    bpy.context.view_layer.update()
    for cutter in (cavity, hole):
        mod = base.modifiers.new(cutter.name, "BOOLEAN")
        mod.operation, mod.solver, mod.object = "DIFFERENCE", "EXACT", cutter
        with bpy.context.temp_override(object=base, active_object=base, selected_objects=[base]):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cutter)
    parts = [base]
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
    # Mostly mid and dark greys (all-light stones on a dark ground read as a field of white bumps).
    greys = ("grey_mid", "grey_stone", "grey_dark", "warm_grey", "stone", "grey_mid", "grey_stone", "grey_light")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    nx, ny = int(W / STEP), int(D / STEP)
    for j in range(ny):
        for i in range(nx):
            cx = -W / 2 + STEP * (i + 0.5 + (0.5 if j % 2 else 0) * 0.5) + rnd.uniform(-0.018, 0.018)
            cy = -D / 2 + STEP * (j + 0.5) + rnd.uniform(-0.018, 0.018)
            if abs(cx) > W / 2 - 0.04 or abs(cy) > D / 2 - 0.04 or rnd.random() < 0.06:
                continue                                                     # a few gaps show the ground
            s = rnd.choice((0.6, 0.75, 0.9, 1.0, 1.0, 1.1, 1.25))              # small, medium and a few big stones
            rx, ry = s * rnd.uniform(0.04, 0.052), s * rnd.uniform(0.034, 0.046)
            h = s * rnd.uniform(0.018, 0.03)
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


@prop
def island():
    """2.0 x 0.95 x 0.9 m kitchen island (owner: white handle-less drawers, dark-grey top, the hob). Drawers on the
    front (Blender -Y, facing the back counter): three columns of three, 3 mm shadow gaps between the fronts, a
    recessed dark plinth, a 3 cm worktop with a small overhang and a black induction hob with its ring marks."""
    W, D, H = 2.0, 0.95, 0.9
    PLINTH, TOP = 0.1, 0.03
    body_h = H - TOP
    parts = [ds.box("Carcass", (0, 0.02, PLINTH + (body_h - PLINTH) / 2), (W - 0.02, D - 0.06, body_h - PLINTH), "grey_light", bevel=0.004),
             ds.box("Plinth", (0, 0.06, PLINTH / 2), (W - 0.1, D - 0.2, PLINTH), "grey_deep", bevel=0.003),
             ds.box("Worktop", (0, 0, H - TOP / 2), (W + 0.03, D + 0.03, TOP), "grey_dark", bevel=0.006)]
    gap, cols = 0.003, 3
    fw = (W - 0.02 - gap * (cols + 1)) / cols
    heights = (0.16, 0.26, 0.33)                                            # top to bottom
    for c in range(cols):
        x = -W / 2 + 0.01 + gap + fw / 2 + c * (fw + gap)
        z = H - TOP - gap
        for h in heights:
            fh = h - gap
            parts.append(ds.box("Drawer", (x, -D / 2 + 0.012, z - fh / 2), (fw, 0.02, fh), "white", bevel=0.003))
            z -= h
    for s in (-1, 1):                                                        # end panels
        parts.append(ds.box("EndPanel", (s * (W / 2 - 0.009), 0.0, (body_h - 0.003) / 2), (0.018, D - 0.02, body_h - 0.003), "white", bevel=0.003))
    parts.append(ds.box("BackPanel", (0, D / 2 - 0.02, PLINTH + (body_h - PLINTH) / 2), (W - 0.04, 0.018, body_h - PLINTH), "white", bevel=0.003))
    hob = ds.box("Hob", (0.25, 0.0, H + 0.002), (0.58, 0.5, 0.006), "black", bevel=0.002)
    parts.append(hob)
    for (hx, hy, r) in ((0.08, -0.1, 0.1), (0.42, -0.1, 0.08), (0.08, 0.12, 0.08), (0.42, 0.12, 0.1)):
        ring = ds.cylinder("HobRing", (hx, hy, H + 0.0052), r, 0.0008, "grey_dark", segments=24, bevel=0)
        inner = ds.cylinder("HobRingIn", (hx, hy, H + 0.0056), r - 0.006, 0.0008, "black", segments=24, bevel=0)
        parts += [ring, inner]
    return parts


def _cut(target, cutter):
    """Boolean difference (exact), then remove the cutter. Its faces keep the cutter's palette colour."""
    bpy.context.view_layer.update()
    mod = target.modifiers.new(cutter.name, "BOOLEAN")
    mod.operation, mod.solver, mod.object = "DIFFERENCE", "EXACT", cutter
    with bpy.context.temp_override(object=target, active_object=target, selected_objects=[target]):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter)


@prop
def back_counter():
    """2.1 x 0.6 x 0.9 m counter against the south wall, matching the island: four columns of white handle-less
    fronts (two doors under the sink), a recessed plinth, the dark worktop with a stainless sink cut into it, a
    mixer tap, and a coffee machine with a cup at the left end. Front faces Blender -Y (the room)."""
    W, D, H = 2.1, 0.6, 0.9
    PLINTH, TOP = 0.1, 0.03
    body_h = H - TOP
    parts = [ds.box("Carcass", (0, 0.02, PLINTH + (body_h - PLINTH) / 2), (W - 0.02, D - 0.06, body_h - PLINTH), "grey_light", bevel=0.004),
             ds.box("Plinth", (0, 0.05, PLINTH / 2), (W - 0.04, D - 0.16, PLINTH), "grey_deep", bevel=0.003)]
    top = ds.box("Worktop", (0, 0, H - TOP / 2), (W + 0.02, D + 0.02, TOP), "grey_dark", bevel=0.006)
    cols, gap = 4, 0.003
    fw = (W - 0.02 - gap * (cols + 1)) / cols
    sink_x = -W / 2 + 0.01 + gap + fw / 2 + 2 * (fw + gap)
    for c in range(cols):
        x = -W / 2 + 0.01 + gap + fw / 2 + c * (fw + gap)
        z = H - TOP - gap
        if c == 2:                                                   # two doors under the sink
            fh = body_h - PLINTH - 2 * gap
            for s in (-1, 1):
                parts.append(ds.box("Door", (x + s * (fw / 4 + gap / 4), -D / 2 + 0.012, PLINTH + gap + fh / 2),
                                    (fw / 2 - gap / 2, 0.02, fh), "white", bevel=0.003))
            continue
        for h in (0.16, 0.26, 0.33):
            fh = h - gap
            parts.append(ds.box("Drawer", (x, -D / 2 + 0.012, z - fh / 2), (fw, 0.02, fh), "white", bevel=0.003))
            z -= h
    # The sink: a basin cut into the worktop (the cutter's faces stay "metal").
    basin = ds.box("Basin", (sink_x, -0.02, H - 0.09), (0.46, 0.36, 0.2), "metal", bevel=0)
    _cut(top, basin)
    parts.append(top)
    parts.append(ds.cylinder("Drain", (sink_x, -0.02, H - 0.188), 0.03, 0.002, "metal_dark", segments=12, bevel=0))
    # Mixer tap behind the sink: a column, an arched spout, a lever.
    tx, ty = sink_x, D / 2 - 0.09
    parts.append(ds.cylinder("TapBase", (tx, ty, H), 0.025, 0.03, "metal", segments=12, bevel=0.002))
    parts.append(ds.cylinder("TapColumn", (tx, ty, H + 0.03), 0.014, 0.26, "metal", segments=10, bevel=0))
    spout = ds.cylinder("TapSpout", (0, 0, 0), 0.011, 0.2, "metal", segments=10, bevel=0)
    spout.rotation_euler.x = math.radians(90 + 15)                  # forward and a little down, over the basin
    spout.location = (tx, ty, H + 0.28)
    parts.append(spout)
    parts.append(ds.box("TapLever", (tx + 0.035, ty, H + 0.2), (0.06, 0.012, 0.012), "metal", bevel=0.002))
    # Coffee machine at the left end, a white cup under its spout.
    cx = -W / 2 + 0.26
    parts.append(ds.box("CoffeeBody", (cx, 0.08, H + 0.19), (0.24, 0.32, 0.38), "metal_dark", bevel=0.015))
    parts.append(ds.box("CoffeeHead", (cx, -0.09, H + 0.31), (0.14, 0.06, 0.06), "black", bevel=0.01))
    parts.append(ds.box("DripTray", (cx, -0.08, H + 0.012), (0.18, 0.1, 0.024), "metal", bevel=0.004))
    parts.append(ds.cylinder("Cup", (cx, -0.09, H + 0.024), 0.035, 0.07, "white", segments=12, bevel=0.003))
    parts.append(ds.box("Tank", (cx + 0.125, 0.12, H + 0.17), (0.02, 0.2, 0.3), "grey_light", bevel=0.004))
    return parts


@prop
def fridge():
    """0.6 x 0.6 x 2.1 m integrated fridge column matching the kitchen: white handle-less doors (a tall fridge door,
    a cupboard door above it) with 3 mm shadow gaps, a dark grip channel along the opening edge, a recessed plinth.
    Front faces Blender -Y (the room)."""
    W, D, H, PLINTH = 0.6, 0.6, 2.1, 0.1
    gap = 0.003
    parts = [ds.box("Carcass", (0, 0.02, PLINTH + (H - PLINTH) / 2), (W - 0.01, D - 0.04, H - PLINTH), "grey_light", bevel=0.004),
             ds.box("Plinth", (0, 0.05, PLINTH / 2), (W - 0.04, D - 0.12, PLINTH), "grey_deep", bevel=0.003)]
    fridge_h, top_h = 1.42, H - PLINTH - 1.42
    z = PLINTH + gap
    for name, h in (("FridgeDoor", fridge_h), ("TopDoor", top_h)):
        fh = h - 2 * gap
        parts.append(ds.box(name, (0, -D / 2 + 0.01, z + fh / 2), (W - 2 * gap, 0.02, fh), "white", bevel=0.003))
        # Grip channel: a dark recess along the opening edge of each door.
        parts.append(ds.box("Grip", (W / 2 - 0.025, -D / 2 - 0.0005, z + fh / 2), (0.02, 0.004, fh - 0.04), "grey_deep", bevel=0))
        z += h
    for s in (-1, 1):                                                        # side panels
        parts.append(ds.box("Side", (s * (W / 2 - 0.009), 0.0, (H) / 2), (0.018, D - 0.02, H - 0.004), "white", bevel=0.003))
    return parts


@prop
def plant_stand():
    """0.4 x 0.4 x 0.7 m snake plant in a terracotta pot by the sliding door. Organic, so faceted (house rule): eleven
    flat-shaded blades, each a tapering three-sided spike flattened to a blade and leaning out a little, in two greens
    with a few lighter ones; the pot with a rim and the soil."""
    import random
    rnd = random.Random(5)
    parts = [ds.cylinder("Pot", (0, 0, 0), 0.13, 0.26, "terracotta", segments=10, radius_top=0.16, bevel=0.006, smooth=False),
             ds.cylinder("Rim", (0, 0, 0.24), 0.17, 0.035, "terracotta", segments=10, bevel=0.004, smooth=False),
             ds.cylinder("Soil", (0, 0, 0.255), 0.15, 0.012, "soil", segments=10, bevel=0, smooth=False)]
    for i in range(14):
        a = i * 2.39996 + rnd.uniform(-0.2, 0.2)                            # golden-angle spread
        r = 0.02 + 0.08 * ((i % 4) / 3)
        h = rnd.uniform(0.36, 0.5) * (1.0 if i % 4 else 0.8)
        colour = "leaf_light" if i % 5 == 0 else rnd.choice(("leaf", "leaf_dark", "leaf"))
        # Snake-plant blades are broad (5-7 cm) and stiff: a wide, flat spike with a sharp tip.
        blade = ds.cylinder("Blade", (0, 0, 0), 0.055, h, colour, segments=4, radius_top=0.0, bevel=0, smooth=False)
        blade.scale = (1.0, 0.22, 1.0)                                      # flat blade
        blade.rotation_euler = (math.radians(rnd.uniform(4, 14)) * math.cos(a), math.radians(rnd.uniform(4, 14)) * math.sin(a), a)
        blade.location = (math.cos(a) * r * 0.6, math.sin(a) * r * 0.6, 0.255)
        parts.append(blade)
    return parts


@prop
def front_door_leaf():
    """The flat's front door leaf, 0.74 x 2.05 x 0.04 m, set into the kitchen's east wall by ApartmentBuilder
    (BuildHomecoming). Walnut with two moulded panels on the room side (Blender -Y), a lever handle on a round rose
    and a lock cylinder near the opening edge (Blender -X), a peephole at eye height. Origin: bottom middle."""
    W, H, T = 0.74, 2.05, 0.04
    parts = [ds.box("Leaf", (0, 0, H / 2), (W, T, H), "walnut", bevel=0.004)]
    m = 0.012                                                                # moulding: 12 mm square strips
    for (zc, ph) in ((H * 0.68, 0.86), (H * 0.24, 0.66)):                    # upper and lower panel
        pw = W - 0.24
        for s in (-1, 1):
            parts.append(ds.box("Moulding", (s * (pw / 2 - m / 2), -T / 2 - m / 2, zc), (m, m, ph), "walnut_dark", bevel=0.002))
            parts.append(ds.box("Moulding", (0, -T / 2 - m / 2, zc + s * (ph / 2 - m / 2)), (pw, m, m), "walnut_dark", bevel=0.002))
    hx = -W / 2 + 0.08
    rose = ds.cylinder("Rose", (0, 0, 0), 0.026, 0.01, "metal", segments=16, bevel=0.002)
    rose.rotation_euler.x = math.radians(90)
    rose.location = (hx, -T / 2, 1.05)
    lever = ds.box("Lever", (hx + 0.06, -T / 2 - 0.05, 1.05), (0.13, 0.018, 0.018), "metal", bevel=0.004)
    neck = ds.box("LeverNeck", (hx, -T / 2 - 0.025, 1.05), (0.018, 0.05, 0.018), "metal", bevel=0.003)
    lock = ds.cylinder("Lock", (0, 0, 0), 0.016, 0.012, "metal_dark", segments=12, bevel=0.002)
    lock.rotation_euler.x = math.radians(90)
    lock.location = (hx, -T / 2, 0.92)
    peep = ds.cylinder("Peephole", (0, 0, 0), 0.01, 0.01, "metal", segments=10, bevel=0)
    peep.rotation_euler.x = math.radians(90)
    peep.location = (0, -T / 2, 1.55)
    return parts + [rose, lever, neck, lock, peep]


@prop
def lamp_kitchen_lamp():
    """Pendant over the kitchen island (the layout lamp "kitchen_lamp" hangs its light at 2.3 m): a ceiling rose, a
    cord and a dark metal dome shade with a pale inside, open at the bottom (the bulb is added in Unity, emissive).
    Built at real height: origin on the floor under the lamp, the ceiling at 2.6 m."""
    C = 2.6
    parts = [ds.cylinder("Rose", (0, 0, C - 0.03), 0.05, 0.03, "metal_dark", segments=12, bevel=0.004),
             ds.cylinder("Cord", (0, 0, 2.38), 0.004, C - 0.03 - 2.38, "black", segments=6, bevel=0),
             ds.cylinder("ShadeTop", (0, 0, 2.34), 0.04, 0.04, "metal_dark", segments=12, bevel=0.003),
             ds.cylinder("Shade", (0, 0, 2.18), 0.19, 0.16, "metal_dark", segments=20, radius_top=0.05, bevel=0.004),
             ds.cylinder("ShadeInside", (0, 0, 2.185), 0.18, 0.15, "cream", segments=20, radius_top=0.045, bevel=0)]
    return parts


@prop
def lamp_living_lamp():
    """Living-room ceiling lamp (layout "living_lamp", light at 2.3 m): a ceiling rose, a cord and a linen drum
    shade with a diffuser disc at the bottom (the bulb is added in Unity). Origin on the floor under the lamp."""
    C = 2.6
    return [ds.cylinder("Rose", (0, 0, C - 0.03), 0.05, 0.03, "white", segments=12, bevel=0.004),
            ds.cylinder("Cord", (0, 0, 2.42), 0.004, C - 0.03 - 2.42, "white", segments=6, bevel=0),
            ds.cylinder("Drum", (0, 0, 2.18), 0.24, 0.24, "linen", segments=24, bevel=0.003),
            ds.cylinder("Diffuser", (0, 0, 2.19), 0.235, 0.004, "pale_yellow", segments=24, bevel=0)]


@prop
def lamp_dining_lamp():
    """Pendant over the dining bar (layout "dining_lamp", light at 1.95 m: about 0.9 m over the bar top, low and
    warm so the corner reads at night). A ceiling rose, a long cord and a painted cone shade with a brass rim and a
    pale inside (the bulb is added in Unity, emissive). Origin on the floor under the lamp, the ceiling at 2.6 m."""
    C = 2.6
    return [ds.cylinder("Rose", (0, 0, C - 0.03), 0.045, 0.03, "teal_dark", segments=12, bevel=0.004),
            ds.cylinder("Cord", (0, 0, 2.06), 0.004, C - 0.03 - 2.06, "black", segments=6, bevel=0),
            ds.cylinder("Cap", (0, 0, 2.02), 0.03, 0.045, "mustard", segments=12, bevel=0.003),
            ds.cylinder("Shade", (0, 0, 1.86), 0.16, 0.17, "teal", segments=20, radius_top=0.035, bevel=0.003),
            ds.cylinder("ShadeInside", (0, 0, 1.865), 0.15, 0.16, "cream", segments=20, radius_top=0.03, bevel=0),
            ds.cylinder("Rim", (0, 0, 1.855), 0.165, 0.012, "mustard", segments=20, bevel=0.002)]


def _shaft_window(W, H=1.3):
    """An upper-floor window in the court's light well, W x H: a light frame (6 cm) with a mullion and a transom,
    dark blue-grey panes set back 3 cm, a handle on the opening sash. Origin bottom middle; front Blender -Y."""
    F, D = 0.06, 0.06
    parts = []
    for s in (-1, 1):
        parts.append(ds.box("Jamb", (s * (W / 2 - F / 2), 0, H / 2), (F, D, H), "white", bevel=0.006))
        parts.append(ds.box("Rail", (0, 0, H / 2 + s * (H / 2 - F / 2)), (W, D, F), "white", bevel=0.006))
    parts.append(ds.box("Mullion", (0, 0, (F + H * 0.78) / 2), (0.05, D * 0.9, H * 0.78 - F), "white", bevel=0.004))
    parts.append(ds.box("Transom", (0, 0, H * 0.78), (W - 2 * F, D * 0.9, 0.05), "white", bevel=0.004))
    for (x0, x1, z0, z1) in ((-W / 2 + F, -0.025, F, H * 0.78 - 0.025), (0.025, W / 2 - F, F, H * 0.78 - 0.025),
                             (-W / 2 + F, W / 2 - F, H * 0.78 + 0.025, H - F)):
        parts.append(ds.box("Pane", ((x0 + x1) / 2, 0.03, (z0 + z1) / 2), (x1 - x0, 0.008, z1 - z0), "deep_blue_grey", bevel=0))
    parts.append(ds.box("Handle", (0.06, -D / 2 - 0.01, H * 0.42), (0.02, 0.02, 0.1), "metal", bevel=0.003))
    return parts


@prop
def shaft_window_wide():
    """North wall of the light well: 1.3 m wide."""
    return _shaft_window(1.3)


@prop
def shaft_window_narrow():
    """West wall of the light well: 0.9 m wide."""
    return _shaft_window(0.9)


@prop
def pipe_segment():
    """One metre of zinc drainpipe (radius 5 cm, the layout's default) with the socket collar at its top joint.
    ApartmentBuilder stacks these along every climbable pipe and scales them to the pipe's radius. Origin: bottom."""
    return [ds.cylinder("Pipe", (0, 0, 0), 0.05, 1.0, "metal", segments=12, bevel=0, smooth=True),
            ds.cylinder("Collar", (0, 0, 0.94), 0.057, 0.06, "metal", segments=12, bevel=0.004, smooth=True)]


@prop
def pipe_bracket():
    """A drainpipe bracket: a clip ring round the pipe, an arm to the wall (Blender +Y = towards the wall) and a
    fixing plate with two screws. Origin: the pipe's axis at the ring's height."""
    return [ds.cylinder("Ring", (0, 0, -0.015), 0.059, 0.03, "metal_dark", segments=12, bevel=0.003, smooth=True),
            ds.box("Arm", (0, 0.075, 0), (0.025, 0.05, 0.02), "metal_dark", bevel=0.003),
            ds.box("Plate", (0, 0.1, 0), (0.07, 0.008, 0.05), "metal_dark", bevel=0.002),
            ds.cylinder("Screw", (-0.022, 0.096, 0), 0.006, 0.004, "metal", segments=6, bevel=0),
            ds.cylinder("Screw", (0.022, 0.096, 0), 0.006, 0.004, "metal", segments=6, bevel=0)]


@prop
def pipe_shoe():
    """The outlet at the foot of a drainpipe: a 30 cm bend kicking out away from the wall (Blender -Y) over a small
    drain grate. Origin: the pipe's axis on the ground."""
    shoe = ds.cylinder("Shoe", (0, 0, 0), 0.05, 0.2, "metal", segments=12, bevel=0, smooth=True)
    shoe.rotation_euler.x = math.radians(-40)         # the top stays on the pipe, the mouth points out from the wall
    shoe.location = (0, -0.1, 0.06)
    grate = ds.box("Grate", (0, -0.14, 0.004), (0.16, 0.16, 0.008), "grey_deep", bevel=0.002)
    return [ds.cylinder("Joint", (0, 0, 0.22), 0.057, 0.06, "metal", segments=12, bevel=0.004, smooth=True), shoe, grate]


@prop
def picnic_table():
    """1.8 x 1.5 x 0.75 m garden picnic table with fixed benches (owner's pin), weathered grey wood: five top planks
    with 1 cm gaps, two planks per bench at 0.43 m, crossed A-frame legs at each end with a cross bar under the
    benches, a centre brace and bolt heads. The collider follows the mesh: bench -> table jumps, free underneath."""
    import random
    rnd = random.Random(8)
    W, D, H = 1.8, 1.5, 0.75
    top_d, n_top = 0.75, 5
    pw = top_d / n_top
    woods = ("warm_grey", "grey_stone", "stone")
    parts = []
    for i in range(n_top):
        y = -top_d / 2 + pw * (i + 0.5)
        parts.append(ds.box("TopPlank", (0, y, H - 0.02), (W, pw - 0.01, 0.04), rnd.choice(woods), bevel=0.005))
    bench_y = D / 2 - 0.13
    for s in (-1, 1):
        for k in (-1, 1):
            parts.append(ds.box("BenchPlank", (0, s * bench_y + k * 0.065, 0.43), (W, 0.12, 0.035), rnd.choice(woods), bevel=0.005))
    for e in (-1, 1):
        x = e * (W / 2 - 0.22)
        for s in (-1, 1):                                                   # the A: two legs from the top to the ground
            leg = ds.box("Leg", (0, 0, 0), (0.07, 0.07, 0.9), "grey_stone", bevel=0.006)
            leg.rotation_euler.x = math.radians(s * 34)
            leg.location = (x, s * 0.26, 0.37)
            parts.append(leg)
        parts.append(ds.box("CrossBar", (x, 0, 0.395), (0.07, D - 0.06, 0.05), "grey_stone", bevel=0.005))  # carries the benches
        parts.append(ds.box("TopBar", (x, 0, H - 0.07), (0.07, top_d - 0.04, 0.05), "grey_stone", bevel=0.005))
        for s in (-1, 1):
            parts.append(ds.cylinder("Bolt", (x - 0.036, s * 0.2, 0.395), 0.012, 0.006, "metal_dark", segments=6, bevel=0))
    brace = ds.box("Brace", (0, 0, 0.42), (W - 0.5, 0.05, 0.05), "grey_stone", bevel=0.005)
    parts.append(brace)
    return parts


@prop
def garden_lantern():
    """The wall lantern by the sliding door (chapter 9), in black iron. Origin: the point on the house wall where it
    hangs; Blender -Y is away from the wall. A wall plate, an arm with a diagonal brace, a hook, and a lantern frame
    round the glowing glass (the glass stays in Unity: NightLamp switches its glow): four corner posts, a base plate,
    a pyramid roof with a finial."""
    iron = "black"
    c = -0.17                                                    # the lantern's centre, 17 cm out from the wall
    parts = [ds.box("Plate", (0, 0.01, 0.02), (0.1, 0.02, 0.2), iron, bevel=0.004),
             ds.box("Arm", (0, (c + 0.0) / 2, 0.06), (0.018, abs(c), 0.018), iron, bevel=0.003)]
    brace = ds.box("Brace", (0, 0, 0), (0.012, 0.14, 0.012), iron, bevel=0.002)
    brace.rotation_euler.x = math.radians(-40)
    brace.location = (0, -0.05, 0.0)
    parts.append(brace)
    parts.append(ds.cylinder("Hook", (0, c, 0.03), 0.006, 0.035, iron, segments=6, bevel=0))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(ds.box("Post", (sx * 0.052, c + sy * 0.052, -0.05), (0.012, 0.012, 0.17), iron, bevel=0.002))
    parts.append(ds.box("Base", (0, c, -0.135), (0.12, 0.12, 0.015), iron, bevel=0.003))
    parts.append(ds.box("Collar", (0, c, 0.035), (0.12, 0.12, 0.012), iron, bevel=0.003))
    roof = ds.cylinder("Roof", (0, c, 0.04), 0.09, 0.07, iron, segments=4, radius_top=0.012, bevel=0, smooth=False)
    roof.rotation_euler.z = math.radians(45)
    roof.location = (0, c, 0.04)
    parts.append(roof)
    parts.append(ds.cylinder("Finial", (0, c, 0.11), 0.012, 0.03, iron, segments=6, radius_top=0.0, bevel=0, smooth=False))
    for sx in (-1, 1):                                           # thin glazing bars on the front and back
        parts.append(ds.box("Bar", (0, c + sx * 0.052, -0.05), (0.006, 0.006, 0.16), iron, bevel=0))
    return parts


@prop
def street_lamp_base():
    """The foot of the street and quay lamp-posts: a fluted cast-iron plinth with mouldings (old dark green paint).
    Origin: the ground under the post. The post itself stays a Unity cylinder (its height differs per lamp)."""
    iron = "metal_dark"
    parts = [ds.cylinder("Plinth", (0, 0, 0), 0.17, 0.08, iron, segments=12, radius_top=0.16, bevel=0.01, smooth=False),
             ds.cylinder("Bell", (0, 0, 0.08), 0.14, 0.42, iron, segments=12, radius_top=0.085, bevel=0.0, smooth=False),
             ds.cylinder("Ring", (0, 0, 0.5), 0.1, 0.04, iron, segments=12, bevel=0.008, smooth=False),
             ds.cylinder("Neck", (0, 0, 0.54), 0.075, 0.26, iron, segments=12, radius_top=0.06, bevel=0.0, smooth=False),
             ds.cylinder("Ring2", (0, 0, 0.8), 0.08, 0.03, iron, segments=12, bevel=0.006, smooth=False)]
    for k in range(6):                                            # flutes down the bell
        a = k / 6 * math.tau
        f = ds.box("Flute", (math.cos(a) * 0.118, math.sin(a) * 0.118, 0.29), (0.02, 0.02, 0.34), iron, bevel=0.004)
        f.rotation_euler.z = a
        f.location = (math.cos(a) * 0.118, math.sin(a) * 0.118, 0.29)
        parts.append(f)
    return parts


@prop
def street_lamp_head():
    """The lantern on top of the lamp-posts. Origin: the top of the post. A collar, four corner posts round the glass
    (the glass stays in Unity: NightLamp switches its glow; it is 0.28 x 0.36 x 0.28 m, its centre 0.22 m up), a
    flared pyramid roof with a finial, and a little crossbar for a ladder."""
    iron = "metal_dark"
    parts = [ds.cylinder("Collar", (0, 0, -0.06), 0.06, 0.08, iron, segments=10, radius_top=0.09, bevel=0, smooth=False),
             ds.box("Base", (0, 0, 0.035), (0.34, 0.34, 0.03), iron, bevel=0.006),
             ds.box("Top", (0, 0, 0.405), (0.34, 0.34, 0.02), iron, bevel=0.005)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(ds.box("Post", (sx * 0.15, sy * 0.15, 0.22), (0.022, 0.022, 0.36), iron, bevel=0.003))
    roof = ds.cylinder("Roof", (0, 0, 0.415), 0.27, 0.14, iron, segments=4, radius_top=0.03, bevel=0, smooth=False)
    roof.rotation_euler.z = math.radians(45)
    roof.location = (0, 0, 0.415)
    parts.append(roof)
    parts.append(ds.cylinder("Finial", (0, 0, 0.555), 0.025, 0.09, iron, segments=6, radius_top=0.0, bevel=0, smooth=False))
    parts.append(ds.box("LadderBar", (0, 0, -0.16), (0.5, 0.025, 0.025), iron, bevel=0.004))
    return parts


@prop
def wheelie_bin():
    """A 240 l wheelie bin: a slightly tapered body (wider at the top), a lid with an overhang and a hinge, a handle
    bar and two wheels at the back. Front (-Y) faces the street. 0.58 x 0.74 x 1.07 m."""
    body, lid = "grey_mid", "grey_dark"
    parts = [ds.cylinder("Body", (0, 0, 0.04), 0.36, 0.95, body, segments=4, radius_top=0.42, bevel=0.0, smooth=False)]
    parts[0].rotation_euler.z = math.radians(45)
    parts[0].scale = (0.92, 1.18, 1.0)
    parts += [ds.box("Lid", (0, -0.015, 1.02), (0.62, 0.79, 0.05), lid, bevel=0.012),
              ds.box("LidLip", (0, -0.4, 0.985), (0.6, 0.03, 0.06), lid, bevel=0.008),
              ds.box("Hinge", (0, 0.39, 0.99), (0.56, 0.05, 0.05), lid, bevel=0.01),
              ds.box("Handle", (0, 0.43, 0.93), (0.5, 0.035, 0.035), lid, bevel=0.008),
              ds.box("Axle", (0, 0.31, 0.11), (0.6, 0.06, 0.06), "black", bevel=0.01)]
    for sx in (-1, 1):
        w = ds.cylinder("Wheel", (sx * 0.31, 0.31, 0.11), 0.11, 0.06, "black", segments=12, bevel=0.01, smooth=False)
        w.rotation_euler.y = math.radians(90)
        w.location = (sx * 0.28, 0.31, 0.11)
        parts.append(w)
    return parts


@prop
def compost_bin():
    """A black plastic compost bin: an octagonal cone (narrower at the top), ribs, a domed lid with a knob and a
    little hatch at the foot. 0.7 m across, 0.88 m high."""
    c = "black"
    parts = [ds.cylinder("Body", (0, 0, 0), 0.36, 0.82, c, segments=8, radius_top=0.29, bevel=0.0, smooth=False),
             ds.cylinder("Lid", (0, 0, 0.82), 0.31, 0.05, c, segments=8, radius_top=0.24, bevel=0.0, smooth=False),
             ds.cylinder("Knob", (0, 0, 0.87), 0.05, 0.03, c, segments=8, bevel=0.005, smooth=False),
             ds.box("Hatch", (0, -0.345, 0.12), (0.26, 0.03, 0.18), "charcoal", bevel=0.01)]
    for k in range(8):
        a = (k + 0.5) / 8 * math.tau
        r = ds.box("Rib", (0, 0, 0), (0.03, 0.03, 0.8), "charcoal", bevel=0.005)
        r.rotation_euler = (0.0, math.radians(-4.5), a)
        r.location = (math.cos(a) * 0.315, math.sin(a) * 0.315, 0.41)  # half sunk into the wall
        parts.append(r)
    return parts


def _tube(name, points, radius, colour, sides=6):
    """A faceted tube along a polyline (closed at both ends), for bent iron and dough."""
    import bmesh
    from mathutils import Vector
    pts = [Vector(q) for q in points]
    bm = bmesh.new()
    rings = []
    for i, q in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        a = t.cross(Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))).normalized()
        b = t.cross(a)
        rings.append([bm.verts.new(q + (a * math.cos(k / sides * math.tau) + b * math.sin(k / sides * math.tau)) * radius)
                      for k in range(sides)])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = ds._obj(name, bm, (0, 0, 0))
    ds.facet(o)
    ds.paint(o, colour)
    return o


def _loaf(name, c, length, width, colour, seed):
    return ds.blob(name, c, width / 2, (1.0, length / width, 0.72), colour, subdiv=1, seed=seed)


@prop
def bakery_shopfront():
    """A painted timber shopfront for the bakery (chapter 6), 6.4 m wide, against the house wall. Origin: the foot
    of the wall at the middle of the shopfront; Blender -Y is out of the wall (the street). From -X: a recessed door
    with a glazed upper half, a pilaster, then the shop window (4.55 m, two glazing bars and a transom) over a
    stallriser with a sill, a fascia with a cornice along the top. Behind the glass (the glass itself stays in Unity):
    a dark interior panel and two display shelves of loaves, baguettes and buns."""
    paint, trim = "teal_dark", "cream"
    W2 = 3.2
    p = []
    for x in (-W2, -1.6, W2):                                       # pilasters with capitals and plinths
        p.append(ds.box("Pilaster", (x, -0.15, 1.35), (0.25, 0.3, 2.7), paint, bevel=0.02))
        p.append(ds.box("Capital", (x, -0.17, 2.66), (0.31, 0.34, 0.08), trim, bevel=0.01))
        p.append(ds.box("Plinth", (x, -0.16, 0.08), (0.29, 0.32, 0.16), "grey_dark", bevel=0.01))
    p += [ds.box("Fascia", (0, -0.16, 2.93), (2 * W2 + 0.3, 0.32, 0.46), paint, bevel=0.02),
          ds.box("FasciaBorder", (0, -0.323, 2.93), (2 * W2 - 0.1, 0.012, 0.34), trim, bevel=0.0),
          ds.box("FasciaPanel", (0, -0.331, 2.93), (2 * W2 - 0.25, 0.012, 0.26), paint, bevel=0.0),
          ds.box("Cornice", (0, -0.18, 3.19), (2 * W2 + 0.45, 0.4, 0.07), trim, bevel=0.012)]
    wx0, wx1 = -1.475, 3.075
    wc, ww = (wx0 + wx1) / 2, wx1 - wx0
    p += [ds.box("Stallriser", (wc, -0.14, 0.31), (ww, 0.28, 0.62), paint, bevel=0.012),
          ds.box("StallPanel", (wc, -0.283, 0.31), (ww - 0.24, 0.012, 0.4), trim, bevel=0.0),
          ds.box("Sill", (wc, -0.165, 0.645), (ww + 0.06, 0.33, 0.05), trim, bevel=0.01),
          ds.box("TopRail", (wc, -0.26, 2.66), (ww, 0.06, 0.08), paint, bevel=0.006),
          ds.box("Transom", (wc, -0.27, 2.27), (ww, 0.05, 0.05), paint, bevel=0.006),
          ds.box("Interior", (wc, -0.012, 1.64), (ww, 0.012, 1.96), "charcoal", bevel=0.0)]
    for x in (wx0 + ww / 3, wx0 + 2 * ww / 3):
        p.append(ds.box("GlazingBar", (x, -0.27, 1.45), (0.05, 0.05, 1.62), paint, bevel=0.006))
    # display: a low shelf on the sill line and a higher one, loaves and baguettes on them
    p.append(ds.box("ShelfLow", (wc, -0.13, 0.69), (ww - 0.05, 0.22, 0.03), "oak", bevel=0.004))
    p.append(ds.box("ShelfHigh", (wc, -0.11, 1.32), (ww - 0.05, 0.18, 0.025), "oak", bevel=0.004))
    import random
    rnd = random.Random(6)
    x = wx0 + 0.25
    k = 0
    while x < wx1 - 0.25:                                           # round and long loaves on the low shelf
        long_ = k % 3 == 1
        p.append(_loaf("Loaf", (x, -0.13, 0.74), 0.34 if long_ else 0.2, 0.16 if long_ else 0.19,
                       "a_corgi_tan" if k % 2 else "oak_light", seed=k))
        x += 0.36 + rnd.random() * 0.08
        k += 1
    x = wx0 + 0.2
    while x < wx1 - 0.7:                                            # buns on the high shelf, baguettes at the end
        p.append(_loaf("Bun", (x, -0.11, 1.36), 0.11, 0.1, "a_corgi_tan", seed=k))  # resting on the shelf
        x += 0.17
        k += 1
    for i in range(4):
        b = ds.cylinder("Baguette", (wx1 - 0.45 + i * 0.07, -0.1, 1.33), 0.022, 0.55, "oak_light", segments=6, bevel=0, smooth=False)
        b.rotation_euler.y = math.radians(-14 + i * 6)
        p.append(b)
    # the door: recessed, a frame, a glazed upper half (dark glass), a kick panel, a brass knob; a transom above
    dx0, dx1 = -W2 + 0.125, -1.6 - 0.125
    dc, dw = (dx0 + dx1) / 2, dx1 - dx0
    p += [ds.box("DoorTransom", (dc, -0.06, 2.45), (dw, 0.12, 0.42), "slate", bevel=0.0),
          ds.box("DoorHead", (dc, -0.2, 2.24), (dw, 0.2, 0.06), paint, bevel=0.006),
          ds.box("DoorLeaf", (dc, -0.05, 1.1), (dw - 0.12, 0.05, 2.18), paint, bevel=0.01),
          ds.box("DoorGlass", (dc, -0.077, 1.5), (dw - 0.36, 0.008, 1.05), "slate", bevel=0.0),
          ds.box("DoorPanel", (dc, -0.077, 0.45), (dw - 0.36, 0.008, 0.5), trim, bevel=0.0),
          ds.box("Step", (dc, -0.17, 0.04), (dw + 0.1, 0.34, 0.08), "stone", bevel=0.01)]
    knob = ds.cylinder("Knob", (0, 0, 0), 0.025, 0.05, "mustard", segments=8, bevel=0.005, smooth=False)
    knob.rotation_euler.x = math.radians(90)
    knob.location = (dc + dw / 2 - 0.16, -0.075, 1.02)
    p.append(knob)
    return p


@prop
def bread_crate():
    """A slatted wooden bread crate (0.5 x 0.7 x 0.4 m) with loaves in it. Front -Y."""
    wood = "oak_light"
    p = [ds.box("Bottom", (0, 0, 0.02), (0.5, 0.7, 0.03), wood, bevel=0.004)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.append(ds.box("Post", (sx * 0.23, sy * 0.33, 0.2), (0.04, 0.04, 0.4), "oak", bevel=0.005))
    for z in (0.08, 0.2, 0.33):
        for sy in (-1, 1):
            p.append(ds.box("SlatLong", (0, sy * 0.34, z), (0.46, 0.015, 0.07), wood, bevel=0.003))
        for sx in (-1, 1):
            p.append(ds.box("SlatShort", (sx * 0.24, 0, z - 0.004), (0.015, 0.66, 0.062), wood, bevel=0.003))  # not flush with the long slats
    for i, (x, y) in enumerate(((-0.11, -0.18), (0.1, -0.15), (-0.08, 0.12), (0.11, 0.17))):
        p.append(_loaf("Loaf", (x, y, 0.3), 0.3, 0.17, "a_corgi_tan" if i % 2 else "oak_light", seed=20 + i))
    return p


@prop
def bakery_sign():
    """The bakery's hanging sign: a wall plate, a scrolled iron bracket and a gilt pretzel hanging from two links.
    Origin: on the wall where the plate is fixed (Blender -Y out of the wall); the pretzel hangs 0.6 m out."""
    iron, gold = "black", "mustard"
    p = [ds.box("Plate", (0, -0.01, 0), (0.12, 0.02, 0.3), iron, bevel=0.004),
         ds.box("Arm", (0, -0.45, 0.1), (0.03, 0.9, 0.03), iron, bevel=0.004),
         _tube("Scroll", [(0, -0.02, -0.12), (0, -0.12, -0.08), (0, -0.25, 0.0), (0, -0.4, 0.08), (0, -0.55, 0.1)], 0.012, iron)]
    for y in (-0.48, -0.72):
        p.append(ds.box("Link", (0, y, 0.0), (0.012, 0.012, 0.18), iron, bevel=0.0))
    # the pretzel, in the plane of the bracket (seen from up and down the street): a loop and two crossed arms
    cy, cz, r = -0.6, -0.38, 0.27

    def at(u, v):
        return (0.0, cy + u, cz + v)
    loop = [at(r * math.cos(math.radians(a)), r * math.sin(math.radians(a))) for a in range(-70, 251, 20)]
    p.append(_tube("PretzelA", loop + [at(0.04, -0.06), at(0.25, 0.07)], 0.04, gold, sides=8))
    p.append(_tube("PretzelB", [at(r * math.cos(math.radians(-70)), r * math.sin(math.radians(-70))),
                                at(-0.04, -0.06), at(-0.25, 0.07)], 0.04, gold, sides=8))
    return p


def _maple_leaf(name, centre, size, normal_tilt, yaw, colour, two_sided=True):
    """A small maple leaf: a five-lobed flat outline (fan from the middle), made twice with opposite windings and
    1 mm apart, so it shows from both sides with the one-sided palette material. size = tip-to-stem length (m)."""
    import bmesh
    from mathutils import Vector, Matrix
    lobes = [(90, 1.0), (35, 0.82), (145, 0.82), (-15, 0.5), (195, 0.5)]
    pts = []
    order = sorted(lobes, key=lambda l: l[0])
    for i, (a, r) in enumerate(order):
        a2 = order[(i + 1) % len(order)][0] + (360 if i == len(order) - 1 else 0)
        pts.append((a, r))
        pts.append(((a + a2) / 2, 0.34))                      # the notch between two lobes
    bm = bmesh.new()
    rot = Matrix.Rotation(math.radians(yaw), 3, "Z") @ Matrix.Rotation(math.radians(normal_tilt), 3, "X")
    c = Vector(centre)
    for side, off in (((1, 0.0005), (-1, -0.0005)) if two_sided else ((1, 0.0),)):
        mid = bm.verts.new(c + rot @ Vector((0, 0, off)))
        ring = [bm.verts.new(c + rot @ Vector((math.cos(math.radians(a)) * r * size * 0.5,
                                                math.sin(math.radians(a)) * r * size * 0.5, off))) for a, r in pts]
        for k in range(len(ring)):
            f = (mid, ring[k], ring[(k + 1) % len(ring)])
            bm.faces.new(f if side > 0 else f[::-1])
    o = ds._obj(name, bm, (0, 0, 0))
    ds.facet(o)
    ds.paint(o, colour)
    return o


def _sapling(seed, turning):
    """A young maple shoot in the overgrown lawn: a thin, slightly bending trunk, two or three twigs, and clusters
    of five-lobed leaves at the twig ends and the top (some turning yellow and orange in the 'turning' variant).
    Origin at its foot on the ground; about 1.2 m high."""
    import random
    rnd = random.Random(seed)
    bark = "walnut"
    h = 1.2
    lean = (rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06))
    trunk = [(lean[0] * (t / 6) ** 1.5, lean[1] * (t / 6) ** 1.5, h * t / 6) for t in range(7)]
    parts = [_tube("Trunk", trunk, 0.011, bark, sides=5)]
    tips = [trunk[-1]]
    for k in range(3):
        t = 0.42 + 0.17 * k
        base = (lean[0] * t ** 1.5, lean[1] * t ** 1.5, h * t)
        a = math.radians(rnd.uniform(0, 360) + k * 125)
        L = rnd.uniform(0.22, 0.32)
        mid = (base[0] + math.cos(a) * L * 0.5, base[1] + math.sin(a) * L * 0.5, base[2] + L * 0.25)
        tip = (base[0] + math.cos(a) * L, base[1] + math.sin(a) * L, base[2] + L * 0.42)
        parts.append(_tube("Twig", [base, mid, tip], 0.006, bark, sides=4))
        tips.append(tip)
    greens = ["leaf", "leaf_dark", "leaf_light"]
    autumn = ["mustard", "terracotta", "leaf_light"]
    n = 0
    # Clusters of 8 at the twig ends and the top, 15-22 cm leaves (a young maple's are big for its size), and a few
    # single leaves along the upper trunk, so it reads as a leafy shoot from cat height, not a bare stick.
    spots = [(tip, 8, 0.12) for tip in tips]
    for k in range(6):
        t = 0.5 + 0.08 * k
        spots.append(((lean[0] * t ** 1.5, lean[1] * t ** 1.5, h * t), 1, 0.06))
    for (tx, ty, tz), count, spread in spots:
        for j in range(count):
            a = rnd.uniform(0, 360)
            r = rnd.uniform(0.03, spread)
            c = (tx + math.cos(math.radians(a)) * r, ty + math.sin(math.radians(a)) * r, tz + rnd.uniform(-0.06, 0.05))
            colour = rnd.choice(autumn) if turning and rnd.random() < 0.55 else rnd.choice(greens)
            parts.append(_maple_leaf("Leaf", c, rnd.uniform(0.15, 0.22), rnd.uniform(15, 55), a + 90, colour))
            n += 1
    return parts


@prop
def sapling_green():
    return _sapling(seed=11, turning=False)


@prop
def sapling_turning():
    return _sapling(seed=23, turning=True)


def _blade(name, base, height, width, lean_dir, lean, colour, segs=3):
    """A grass blade or leaf strip: tapering to a point and arching over as it rises, made double-sided (two
    windings 1 mm apart) for the one-sided palette material. lean_dir in radians; lean = how far the tip falls (m)."""
    import bmesh
    from mathutils import Vector
    d = Vector((math.cos(lean_dir), math.sin(lean_dir), 0))
    side = Vector((-d.y, d.x, 0))
    bm = bmesh.new()
    for s_ in (1, -1):
        off = Vector((0, 0, 0)) + d * 0.0005 * s_
        rows = []
        for k in range(segs + 1):
            t = k / segs
            c = Vector(base) + d * (lean * t * t) + Vector((0, 0, height * math.sin(t * math.pi / 2) * (1 - 0.25 * t * t))) + off
            w = width * (1 - t) * 0.5
            rows.append((bm.verts.new(c - side * w), bm.verts.new(c + side * w)) if k < segs else (bm.verts.new(c),))
        for k in range(segs):
            a, b = rows[k], rows[k + 1]
            if len(b) == 2:
                f = (a[0], a[1], b[1], b[0])
            else:
                f = (a[0], a[1], b[0])
            bm.faces.new(f if s_ > 0 else f[::-1])
    o = ds._obj(name, bm, (0, 0, 0))
    ds.facet(o)
    ds.paint(o, colour)
    return o


@prop
def grass_tuft_tall():
    """Long grass for the margins the mower misses (garden review round 4): ten arching blades 25-40 cm in two greens
    and a straw tone, and two seed stalks with buff heads. About 150 tris; origin on the ground."""
    import random
    rnd = random.Random(31)
    parts = []
    for i in range(10):
        a = i * 2 * math.pi / 10 + rnd.uniform(-0.3, 0.3)
        base = (math.cos(a) * 0.035, math.sin(a) * 0.035, 0)
        parts.append(_blade("Blade", base, rnd.uniform(0.24, 0.38), rnd.uniform(0.018, 0.026), a, rnd.uniform(0.12, 0.24),
                            rnd.choice(("grass", "leaf", "leaf_dark", "leaf_light")), segs=3))
    for j in range(2):
        a = rnd.uniform(0, 2 * math.pi)
        h = rnd.uniform(0.42, 0.52)
        tip = (math.cos(a) * 0.05, math.sin(a) * 0.05, h)
        parts.append(_tube("Stalk", [(0, 0, 0), (tip[0] * 0.5, tip[1] * 0.5, h * 0.55), tip], 0.003, "leaf_light", sides=3))
        parts.append(ds.blob("SeedHead", (tip[0], tip[1], h + 0.03), 0.012, (1.0, 1.0, 3.0), "oak_light", subdiv=1, seed=j))
    return parts


@prop
def dandelion():
    """A dandelion in the lawn: a flat rosette of six toothed leaves, two flower stems with yellow heads and, now and
    then, a white seed clock. About 130 tris; origin on the ground."""
    import random
    rnd = random.Random(5)
    parts = []
    for i in range(6):
        a = i * 2 * math.pi / 6 + rnd.uniform(-0.2, 0.2)
        parts.append(_blade("Leaf", (0, 0, 0.004), 0.025, 0.045, a, rnd.uniform(0.11, 0.15), "leaf", segs=3))
    for j, (h, head) in enumerate(((0.16, "mustard"), (0.2, "white"))):
        a = j * 2.4 + 0.5
        tip = (math.cos(a) * 0.03, math.sin(a) * 0.03, h)
        parts.append(_tube("Stem", [(0, 0, 0), (tip[0] * 0.5, tip[1] * 0.5, h * 0.5), tip], 0.0035, "leaf_light", sides=3))
        if head == "mustard":
            parts.append(ds.blob("Flower", (tip[0], tip[1], h + 0.008), 0.022, (1.0, 1.0, 0.45), "mustard", subdiv=1, seed=j))
        else:
            parts.append(ds.blob("Clock", (tip[0], tip[1], h + 0.02), 0.024, (1.0, 1.0, 1.0), "white", subdiv=1, seed=j))
    return parts


def _stepping_stone(seed, size=(0.46, 0.36), tone="grey_mid"):
    """A natural stepping stone (garden review round 7): an irregular, chipped outline (14 points round a rounded
    rectangle), 4 cm thick, a flat top in one face (a fan from a raised middle showed pinwheel facets) inside a ring
    of edge faces, the top edge bevelled; one grey per stone, a few edge faces of moss. Origin at the middle of its
    base; about 70 tris."""
    import bmesh
    import random
    rnd = random.Random(seed)
    a, b = size[0] / 2, size[1] / 2
    n = 14
    ring = []
    for i in range(n):
        t = i / n * math.tau
        cx, cy = math.cos(t), math.sin(t)
        r = 1.0 / ((abs(cx) ** 3 + abs(cy) ** 3) ** (1 / 3))
        r *= rnd.uniform(0.86, 1.0)
        ring.append((cx * r * a, cy * r * b))
    bm = bmesh.new()
    T, bev = 0.04, 0.008
    bottom = [bm.verts.new((x, y, 0.0)) for x, y in ring]
    lip = [bm.verts.new((x, y, T - bev)) for x, y in ring]
    top = [bm.verts.new((x * 0.95, y * 0.95, T)) for x, y in ring]
    inner = [bm.verts.new((x * 0.8, y * 0.8, T)) for x, y in ring]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bottom[i], bottom[j], lip[j], lip[i]))
        bm.faces.new((lip[i], lip[j], top[j], top[i]))
        bm.faces.new((top[i], top[j], inner[j], inner[i]))
    bm.faces.new(inner)
    bm.faces.new(bottom[::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = ds._obj("SteppingStone", bm, (0, 0, 0))
    ds.facet(o)
    darker = {"stone": "grey_mid", "grey_mid": "grey_dark", "grey_dark": "grey_deep"}[tone]
    def colour(poly):
        c = poly.center
        if c.z < T - bev - 0.001:
            return darker                                   # the sides
        if len(poly.vertices) == 4 and c.z > T - 0.001 and rnd.random() < 0.3:
            return "leaf_dark"                              # moss on the top edge
        return tone
    ds.paint(o, colour)
    return [o]


@prop
def stepping_stone_a():
    return _stepping_stone(41, tone="grey_mid")


@prop
def stepping_stone_b():
    return _stepping_stone(42, (0.42, 0.38), tone="stone")


@prop
def stepping_stone_c():
    return _stepping_stone(43, (0.5, 0.34), tone="grey_dark")


def _leaf_litter(seed, count, area):
    """Fallen maple leaves lying in the grass (garden review round 9): count leaves scattered over area (('disc', r) or
    ('strip', length, width)), almost flat, overlapping a little, in autumn palette colours; single-sided (seen from
    above), about 10 tris a leaf. Origin on the ground at the middle."""
    import random
    rnd = random.Random(seed)
    colours = ["mustard", "mustard", "terracotta", "walnut", "oak_light", "oak", "leaf_light"]
    parts = []
    for i in range(count):
        if area[0] == "disc":
            r = area[1] * math.sqrt(rnd.random()) * (0.6 + 0.4 * rnd.random())
            a = rnd.uniform(0, math.tau)
            x, y = math.cos(a) * r, math.sin(a) * r
        else:
            x, y = rnd.uniform(-area[1] / 2, area[1] / 2), rnd.uniform(-area[2] / 2, area[2] / 2)
        z = 0.004 + 0.0012 * i % 0.012
        parts.append(_maple_leaf("Leaf", (x, y, z), rnd.uniform(0.07, 0.11), rnd.uniform(-18, 18),
                                 rnd.uniform(0, 360), rnd.choice(colours), two_sided=False))
    return parts


@prop
def leaf_litter_dense():
    return _leaf_litter(61, 40, ("disc", 0.9))


@prop
def leaf_litter_sparse():
    return _leaf_litter(62, 14, ("disc", 0.8))


@prop
def leaf_litter_strip():
    return _leaf_litter(63, 24, ("strip", 2.0, 0.35))


@prop
def garden_tarp():
    """A blue tarp left on the lawn (garden review round 10), 1.2 x 0.9 m: a faceted cloth grid crumpled in soft folds
    (one ridge along a diagonal, the rest settling into the grass), one corner folded back over itself (its underside
    a lighter teal), metal eyelets at the corners. Origin on the ground at the middle; about 250 tris."""
    import bmesh
    import random
    rnd = random.Random(9)
    W, D, nx, ny = 1.2, 0.9, 12, 9
    bm = bmesh.new()
    verts = {}
    for j in range(ny + 1):
        for i in range(nx + 1):
            x, y = -W / 2 + W * i / nx, -D / 2 + D * j / ny
            # folds: a ridge along the diagonal, two softer waves, a little noise; clamp to the ground
            d = (x / W - y / D) * 1.4
            z = 0.05 * math.exp(-(d * d) * 9) + 0.018 * math.sin(x * 9 + 1) * math.sin(y * 7) + rnd.uniform(0, 0.008)
            z = max(0.003, z)
            # the folded corner (x > 0.3, y > 0.2): flipped back over the tarp, lying on top of it
            if x - 0.3 + (y - 0.2) * 0.6 > 0:
                f = x - 0.3 + (y - 0.2) * 0.6
                k = 2 * f / 1.36                                 # an exact mirror across the fold line (normal (1, 0.6))
                x, y, z = x - k, y - k * 0.6, 0.012 + 0.03 * min(f, 0.5) + 0.004
            verts[i, j] = bm.verts.new((x, y, z))
    folded = set()
    for j in range(ny):
        for i in range(nx):
            f = bm.faces.new((verts[i, j], verts[i + 1, j], verts[i + 1, j + 1], verts[i, j + 1]))
            x0, y0 = -W / 2 + W * (i + 0.5) / nx, -D / 2 + D * (j + 0.5) / ny
            if x0 - 0.3 + (y0 - 0.2) * 0.6 > 0:
                folded.add(f.index)
    bm.normal_update()
    for f in bm.faces:                                     # every face up (the folded part is mirrored)
        if f.normal.z < 0:
            f.normal_flip()
    bm.faces.index_update()
    o = ds._obj("Tarp", bm, (0, 0, 0))
    ds.facet(o)
    ds.paint(o, lambda poly: "teal" if poly.index in folded else "navy")
    parts = [o]
    for (cx, cy) in ((-W / 2 + 0.04, -D / 2 + 0.04), (-W / 2 + 0.04, D / 2 - 0.04), (W / 2 - 0.04, -D / 2 + 0.04)):
        parts.append(ds.cylinder("Eyelet", (cx, cy, 0.004), 0.012, 0.006, "metal", segments=6, bevel=0, smooth=False))
    return parts


def _tree(h, radius, crown, seed, greens=("leaf", "leaf_dark", "leaf_light")):
    """A climbable garden tree, faceted (organic): a tapering bark trunk of height h with a root flare, four or five
    branches out of the fork, and a crown of irregular low-poly leaf clumps (three greens, lighter on top). Origin at
    the foot of the trunk. ApartmentBuilder keeps its cylinder as the invisible climbing collider."""
    import random
    rnd = random.Random(seed)
    parts = [ds.cylinder("Trunk", (0, 0, 0), radius * 1.15, h + 0.15, "walnut_dark", segments=7, radius_top=radius * 0.75, bevel=0, smooth=False),
             ds.cylinder("Flare", (0, 0, 0), radius * 1.7, 0.18, "walnut_dark", segments=7, radius_top=radius * 1.1, bevel=0, smooth=False)]
    for i in range(5):
        a = i * 2 * math.pi / 5 + rnd.uniform(-0.3, 0.3)
        length = rnd.uniform(1.0, 1.5) * crown / 1.6
        br = ds.cylinder("Branch", (0, 0, 0), radius * 0.5, length, "walnut_dark", segments=5, radius_top=radius * 0.2, bevel=0, smooth=False)
        br.rotation_euler = (math.radians(rnd.uniform(35, 55)), 0, a)
        br.location = (0, 0, h - 0.25)
        parts.append(br)
    for i in range(16):
        a = rnd.uniform(0, 2 * math.pi)
        r = crown * math.sqrt(rnd.random()) * 0.85
        z = h + 0.7 + rnd.uniform(0, 1.3) - r * 0.35
        size = rnd.uniform(0.55, 0.85) * crown / 1.6
        colour = greens[2] if z > h + 1.6 else rnd.choice(greens[:2])
        parts.append(ds.blob("Leaves", (math.cos(a) * r, math.sin(a) * r, z), size, (1.0, 1.0, 0.72), colour, subdiv=2, seed=seed * 31 + i))
    return parts


@prop
def tree_maple():
    """The garden maple (layout climbable "maple": trunk 2.4 m, radius 0.14)."""
    return _tree(2.4, 0.14, 1.6, 5)


@prop
def tree_bank_alder():
    """The alder on the river bank (layout climbable "bank_alder": trunk 3.0 m, radius 0.16), darker greens."""
    return _tree(3.0, 0.16, 1.4, 9, greens=("leaf_dark", "olive", "leaf"))


@prop
def grass_tuft():
    """A tuft of six faceted grass blades, 8-16 cm, leaning out from the middle (two greens and a light one).
    ApartmentBuilder scatters copies along the lawn's edges and lightly over it (one material: few draw calls)."""
    import random
    rnd = random.Random(4)
    parts = []
    for i in range(6):
        a = i * 2 * math.pi / 6 + rnd.uniform(-0.4, 0.4)
        h = rnd.uniform(0.08, 0.16)
        blade = ds.cylinder("Blade", (0, 0, 0), 0.014, h, rnd.choice(("grass", "leaf", "leaf_light")), segments=3,
                            radius_top=0.0, bevel=0, smooth=False)
        blade.scale = (1.0, 0.3, 1.0)
        blade.rotation_euler = (math.radians(rnd.uniform(12, 30)), 0, a + math.pi / 2)
        blade.location = (math.cos(a) * 0.015, math.sin(a) * 0.015, 0)
        parts.append(blade)
    return parts


@prop
def wild_bramble():
    """A bramble bush in the neighbour's wild garden, built for radius 0.5 m (ApartmentBuilder scales it per bush):
    six faceted dark clumps of different sizes and a few blackberries. Origin on the ground in the middle."""
    import random
    rnd = random.Random(12)
    parts = []
    for i in range(6):
        a = i * 1.05 + rnd.uniform(-0.3, 0.3)
        r = 0.22 * math.sqrt(rnd.random()) if i else 0
        size = rnd.uniform(0.26, 0.38)
        parts.append(ds.blob("Clump", (math.cos(a) * r, math.sin(a) * r, size * 0.75), size, (1.0, 1.0, 0.8),
                             rnd.choice(("leaf_dark", "olive", "leaf_dark")), subdiv=2, seed=40 + i))
    bpy.context.view_layer.update()
    clumps = [(o.location.copy(), o.dimensions.x / 2) for o in parts]
    for i in range(9):                                            # blackberries on the clumps' surface
        centre, r = clumps[i % len(clumps)]
        a = rnd.uniform(0, 2 * math.pi)
        e = rnd.uniform(-0.2, 0.5)                                # mostly the upper half
        pos = centre + Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e) * 0.8)) * r * 0.95
        parts.append(ds.blob("Berry", tuple(pos), 0.025, (1, 1, 1), "black" if i % 3 else "red", subdiv=1, seed=70 + i))
    return parts


@prop
def wild_wheelbarrow():
    """An old wheelbarrow lying upside down over the hedgehog's nest (the nest, the leaves and the hoglets stay in
    Unity): a tapered rust-red tray turned over, a rolled rim, two wooden handles with grips at the back (Blender
    +Y), the wheel with its tyre at the front (Blender -Y). Origin on the ground in the middle."""
    parts = [ds.box("TrayBottom", (0, 0, 0.33), (0.56, 0.78, 0.025), "red", bevel=0.006)]
    for s in (-1, 1):
        side = ds.box("TraySide", (0, 0, 0), (0.025, 0.86, 0.26), "red", bevel=0.004)
        side.rotation_euler.y = math.radians(-s * 14)
        side.location = (s * 0.31, 0, 0.2)
        parts.append(side)
        end = ds.box("TrayEnd", (0, 0, 0), (0.64, 0.025, 0.26), "brick_dark", bevel=0.004)
        end.rotation_euler.x = math.radians(s * 16)
        end.location = (0, s * 0.43, 0.2)
        parts.append(end)
        parts.append(ds.box("Rim", (s * 0.345, 0, 0.075), (0.03, 0.9, 0.03), "brick_dark", bevel=0.006))
        parts.append(ds.box("Handle", (s * 0.22, 0.6, 0.12), (0.04, 0.75, 0.04), "walnut", bevel=0.006))
        parts.append(ds.cylinder("Grip", (0, 0, 0), 0.025, 0.13, "black", segments=8, bevel=0.003))
        parts[-1].rotation_euler.x = math.radians(90)
        parts[-1].location = (s * 0.22, 0.98, 0.12)
        parts.append(ds.box("Fork", (s * 0.06, -0.48, 0.2), (0.025, 0.2, 0.025), "metal_dark", bevel=0.003))
    for s in (-1, 1):
        parts.append(ds.box("RimEnd", (0, s * 0.44, 0.075), (0.7, 0.03, 0.03), "brick_dark", bevel=0.006))
    tyre = ds.cylinder("Tyre", (0, 0, 0), 0.18, 0.08, "black", segments=16, bevel=0.01)
    tyre.rotation_euler.y = math.radians(90)
    tyre.location = (-0.04, -0.58, 0.2)
    hub = ds.cylinder("Hub", (0, 0, 0), 0.06, 0.1, "metal", segments=10, bevel=0.004)
    hub.rotation_euler.y = math.radians(90)
    hub.location = (-0.05, -0.58, 0.2)
    return parts + [tyre, hub]


@prop
def wild_chair():
    """A white plastic garden chair (monobloc) fallen on its back in the wild garden: modelled standing (seat 0.43 m,
    splayed legs, arm rests, a high back), then tipped back so the backrest lies in the grass."""
    parts = [ds.box("Seat", (0, 0, 0.43), (0.46, 0.44, 0.03), "white", bevel=0.01)]
    back = ds.box("Back", (0, 0, 0), (0.44, 0.03, 0.42), "white", bevel=0.01)
    back.rotation_euler.x = math.radians(-12)
    back.location = (0, 0.24, 0.66)
    parts.append(back)
    for sx in (-1, 1):
        for sy in (-1, 1):
            leg = ds.cylinder("Leg", (0, 0, 0), 0.022, 0.44, "white", segments=8, radius_top=0.016, bevel=0)
            leg.rotation_euler = (math.radians(-sy * 8), math.radians(sx * 8), 0)
            leg.location = (sx * 0.2, sy * 0.19, 0)
            parts.append(leg)
        parts.append(ds.box("Arm", (sx * 0.24, 0.02, 0.6), (0.04, 0.44, 0.03), "white", bevel=0.008))
        parts.append(ds.box("ArmPost", (sx * 0.24, -0.18, 0.51), (0.035, 0.035, 0.17), "white", bevel=0.006))
    # Tip it back about the back legs' feet: the backrest ends up in the grass.
    from mathutils import Matrix
    bpy.context.view_layer.update()
    pivot = Vector((0, 0.24, 0))
    rot = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(-78), 4, "X") @ Matrix.Translation(-pivot)
    for o in parts:
        o.matrix_world = rot @ o.matrix_world
    bpy.context.view_layer.update()
    low = min((o.matrix_world @ Vector(c)).z for o in parts for c in o.bound_box)
    for o in parts:
        o.location.z -= low
    return parts


@prop
def wild_barrel():
    """A rusty steel drum (0.58 m across, 0.9 m high) in the wild garden: rust-red with darker patches, two rolling
    hoops, a rim and a lid with its bung. Origin on the ground."""
    parts = [ds.cylinder("Drum", (0, 0, 0), 0.29, 0.9, "terracotta", segments=16, bevel=0.01, smooth=True)]
    for z in (0.3, 0.6):
        parts.append(ds.cylinder("Hoop", (0, 0, z - 0.015), 0.302, 0.03, "brick_dark", segments=16, bevel=0.004, smooth=True))
    parts.append(ds.cylinder("Rim", (0, 0, 0.88), 0.297, 0.025, "brick_dark", segments=16, bevel=0.004, smooth=True))
    parts.append(ds.cylinder("Bung", (0.15, 0.05, 0.9), 0.03, 0.015, "metal_dark", segments=8, bevel=0.002))
    parts.append(ds.box("RustPatch", (0.0, -0.29, 0.5), (0.18, 0.006, 0.22), "brick_dark", bevel=0))
    return parts


def _human(trousers, coat, shoes, coat_len, bag=False):
    """A human as the cat sees one: legs, shoes and the hem of a coat (the top is out of view at 1.4 m). Trouser
    legs taper to the ankle with a slight bend at the knee, shoes with a sole and a rounded toe (front: Blender -Y),
    a coat hem flaring a little. Origin between the feet; smooth shading (cloth, not faceted)."""
    parts = []
    for s in (-1, 1):
        x = s * 0.1
        parts.append(ds.cylinder("Shin", (x, 0, 0.08), 0.055, 0.45, trousers, segments=10, radius_top=0.062, bevel=0, smooth=True))
        thigh = ds.cylinder("Thigh", (0, 0, 0), 0.064, 0.5, trousers, segments=10, radius_top=0.078, bevel=0, smooth=True)
        thigh.rotation_euler.x = math.radians(-4)
        thigh.location = (x, 0.01, 0.52)
        parts.append(thigh)
        parts.append(ds.box("Sole", (x, -0.04, 0.012), (0.1, 0.28, 0.024), "black", bevel=0.008))
        parts.append(ds.box("Shoe", (x, -0.04, 0.055), (0.095, 0.26, 0.065), shoes, bevel=0.025))
    hem = ds.cylinder("Coat", (0, 0, 1.4 - coat_len), 0.25, coat_len, coat, segments=14, radius_top=0.21, bevel=0, smooth=True)
    hem.scale = (1.0, 0.72, 1.0)
    parts.append(hem)
    if bag:
        # Set down on the floor beside the feet (no hand in view to hold it).
        parts.append(ds.box("Bag", (0.27, 0.02, 0.16), (0.12, 0.28, 0.32), "oak_light", bevel=0.01))
        for s in (-1, 1):
            parts.append(ds.box("BagHandle", (0.27, s * 0.06, 0.36), (0.012, 0.012, 0.08), "oak", bevel=0.002))
        parts.append(ds.blob("Bread", (0.27, 0.08, 0.36), 0.04, (1.0, 1.0, 2.2), "oak_light", subdiv=1, seed=3))
    return parts


@prop
def human_a():
    """The first human at the door: jeans, a long brown coat, black shoes."""
    return _human("navy", "oak", "black", 0.62)


@prop
def human_b():
    """The second human: dark trousers, a short green coat, brown shoes and a paper shopping bag."""
    return _human("grey_deep", "leaf_dark", "walnut_dark", 0.5, bag=True)


@prop
def firewood():
    """1.4 x 0.45 x 0.9 m log store against the house wall: two side planks, a back board, base slats and a sloping
    roof board. The logs run front to back with their cut ends to the garden (front, Blender -Y) and every log
    stays inside the frame (the old stack's logs went through its side boards)."""
    import random
    rnd = random.Random(11)
    W, D, H, T = 1.4, 0.45, 0.9, 0.03
    parts = []
    for s in (-1, 1):
        parts.append(ds.box("Side", (s * (W / 2 - T / 2), 0, H / 2), (T, D, H), "oak", bevel=0.006))
    parts.append(ds.box("Back", (0, D / 2 - 0.01, H / 2), (W - 2 * T, 0.02, H), "oak", bevel=0.004))
    for i in range(3):
        parts.append(ds.box("Slat", (0, -D / 2 + 0.07 + i * 0.15, 0.03), (W - 2 * T, 0.08, 0.025), "walnut", bevel=0.004))
    roof = ds.box("Roof", (0, 0, 0), (W + 0.06, D + 0.08, 0.025), "walnut_dark", bevel=0.005)
    roof.rotation_euler.x = math.radians(-8)          # sloping down to the front
    roof.location = (0, 0.0, H + 0.02)
    parts.append(roof)
    inner = W - 2 * T - 0.02                           # room between the side planks
    y0 = 0.043                                         # on the slats
    row = 0
    while True:
        r_row = 0.055
        z = y0 + r_row + row * 2 * r_row * 0.92
        if z + r_row > H - 0.08:
            break
        x = -inner / 2 + (r_row if row % 2 == 0 else r_row * 1.6)
        while x + r_row <= inner / 2:
            r = r_row * rnd.uniform(0.78, 1.0)
            length = rnd.uniform(D - 0.1, D - 0.05)    # always shorter than the store is deep
            log = ds.cylinder("Log", (0, 0, -length / 2), r, length, "walnut", segments=8, bevel=0)
            # Pale cut ends, dark bark.
            ds.paint(log, lambda p: "linen" if abs(p.normal.z) > 0.9 else rnd.choice(("walnut", "walnut_dark", "oak")))
            log.rotation_euler.x = math.radians(90)    # along the depth: the cut ends face the garden
            # The pivot is the log's base and the turn lays it towards the front (-Y): shift it back by half its
            # length, so the log sits between the back board and the front edge of the store.
            log.location = (x, length / 2 - 0.03 + rnd.uniform(-0.012, 0.012), z)
            parts.append(log)
            x += 2 * r_row * 1.02
        row += 1
    return parts


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
