"""Draho concept frame: one corner of the apartment at blue hour, in the house style (docs/ART_STYLE.md),
with Drahomil.

It is the target picture for the art direction: Unity scenes should come close to it. It also writes the
shared palette texture for Unity.

Usage (Blender 5.x; use absolute paths on Windows):
    blender -b --factory-startup --python Tools/Blender/style_lookdev.py -- \
        --cat Draho/Assets/_Draho/Art/Cat/Drahomil.fbx --out docs/images/style \
        [--palette Draho/Assets/_Draho/Art/Palette/DrahoPalette.png] [--samples 128] [--blend lookdev.blend]
"""
import argparse
import math
import os
import random
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draho_style as ds  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--cat")
ap.add_argument("--out", required=True)
ap.add_argument("--palette")
ap.add_argument("--samples", type=int, default=128)
ap.add_argument("--blend", help="also save the scene as a .blend file")
args = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
rnd = random.Random(7)

# The room: x 0..4.2 (left wall at x = 0), y 0..4 (window wall at y = 4), z up. Front and right stay open
# for the camera.
W, D, H = 4.2, 4.0, 2.7
T = 0.14  # wall thickness

# --- floor: oak planks, 16 cm wide, staggered joints ------------------------------------------------
y = 0.0
row = 0
while y < D:
    w = min(0.16, D - y)
    x = -rnd.uniform(0, 1.2)
    while x < W:
        length = rnd.uniform(0.9, 1.6)
        x0, x1 = max(x, 0.0), min(x + length, W)
        if x1 - x0 > 0.05:
            ds.box(f"Plank{row}_{x0:.2f}", ((x0 + x1) / 2, y + w / 2, -0.015), (x1 - x0 - 0.004, w - 0.004, 0.03),
                   rnd.choice(["oak", "oak", "oak_light"]), bevel=0.004)
        x += length
    y += w
    row += 1
ds.box("Subfloor", (W / 2, D / 2, -0.06), (W + 0.4, D + 0.4, 0.06), "walnut_dark", bevel=0)

# --- walls with a window in the back wall -------------------------------------------------------------
win_x0, win_x1, sill, top = 1.5, 3.1, 0.75, 2.25
ds.box("WallLeft", (-T / 2, D / 2, H / 2), (T, D + T, H), "plaster")
ds.box("WallBack_L", (win_x0 / 2, D + T / 2, H / 2), (win_x0, T, H), "plaster")
ds.box("WallBack_R", ((win_x1 + W) / 2, D + T / 2, H / 2), (W - win_x1, T, H), "plaster")
ds.box("WallBack_Low", ((win_x0 + win_x1) / 2, D + T / 2, sill / 2), (win_x1 - win_x0, T, sill), "plaster")
ds.box("WallBack_High", ((win_x0 + win_x1) / 2, D + T / 2, (top + H) / 2), (win_x1 - win_x0, T, H - top), "plaster")
ds.box("Ceiling", (W / 2, D / 2, H + 0.05), (W + 0.3, D + 0.3, 0.1), "plaster", bevel=0)
for name, cx, cy, sx, sy in (("SkirtLeft", 0.01, D / 2, 0.02, D), ("SkirtBack", W / 2, D - 0.01, W, 0.02)):
    ds.box(name, (cx, cy, 0.05), (sx, sy, 0.1), "walnut", bevel=0.006)
# window frame, mullion, sill
fw = 0.06
mid = (win_x0 + win_x1) / 2
ds.box("Sill", (mid, D - 0.03, sill - 0.02), (win_x1 - win_x0 + 0.16, 0.2, 0.04), "white")
for name, c, s in (("FrameL", (win_x0 + fw / 2, D + 0.02, (sill + top) / 2), (fw, 0.08, top - sill)),
                   ("FrameR", (win_x1 - fw / 2, D + 0.02, (sill + top) / 2), (fw, 0.08, top - sill)),
                   ("FrameT", (mid, D + 0.02, top - fw / 2), (win_x1 - win_x0, 0.08, fw)),
                   ("FrameB", (mid, D + 0.02, sill + fw / 2), (win_x1 - win_x0, 0.08, fw)),
                   ("Mullion", (mid, D + 0.02, (sill + top) / 2), (0.05, 0.06, top - sill)),
                   ("Transom", (mid, D + 0.02, 1.85), (win_x1 - win_x0, 0.06, 0.04))):
    ds.box(name, c, s, "white", bevel=0.008)
glass = ds.box("Glass", (mid, D + 0.02, (sill + top) / 2), (win_x1 - win_x0 - 0.1, 0.01, top - sill - 0.1), "white", bevel=0)
glass.data.materials.clear()
glass.data.materials.append(ds.flat_material("Glass", "#DDE6F0", roughness=0.05, alpha=0.12))
glass.visible_shadow = False
# a curtain on the left of the window (it moves in the wind in the game)
for i in range(5):
    ds.box(f"Curtain{i}", (win_x0 - 0.12 + i * 0.07, D - 0.08 + (i % 2) * 0.03, 1.25), (0.08, 0.05, 2.3), "linen", bevel=0.02)

# --- outside: the garden at dusk, and the neighbours' house with lit windows -----------------------------
ds.box("GardenGround", (W / 2, D + 3, -0.05), (W + 8, 6, 0.1), "grass", bevel=0)
ds.box("GardenWall", (W / 2, D + 4.5, 0.9), (W + 8, 0.25, 1.8), "stone")
for i in range(7):
    ds.blob(f"Hedge{i}", (-0.8 + i * 0.95, D + 4.1, 1.1 + rnd.uniform(-0.1, 0.2)), 0.55, (1.2, 0.8, 1.0),
            rnd.choice(["leaf", "leaf_dark", "olive"]), seed=i)
hx, hy = W / 2 + 0.4, D + 16.0   # far enough that the sky shows above it
ds.box("NeighbourHouse", (hx, hy, 2.5), (9.0, 4.0, 5.0), "warm_grey", bevel=0.05)
ds.box("NeighbourRoof", (hx, hy, 5.6), (9.4, 4.4, 1.2), "roof_slate", bevel=0.05)
lit = ds.flat_material("WindowLit", "#F3DFA7", roughness=0.5, emission=6.0)
for i, (wx, wz, on) in enumerate(((-2.8, 2.2, True), (-0.4, 2.2, False), (2.0, 2.2, True), (-2.8, 3.9, False),
                                  (-0.4, 3.9, True), (2.0, 3.9, True))):
    win = ds.box(f"NWin{i}", (hx + wx, hy - 2.02, wz), (1.0, 0.06, 1.3), "deep_blue_grey", bevel=0)
    if on:
        win.data.materials.clear()
        win.data.materials.append(lit)

# --- rug ---------------------------------------------------------------------------------------------
ds.cylinder("RugBorder", (2.0, 1.75, 0.0), 1.15, 0.012, "deep_blue_grey", segments=40, bevel=0.004).scale.y = 0.72
# light under the cat: the black silhouette reads against it
ds.cylinder("Rug", (2.0, 1.75, 0.003), 1.0, 0.012, "warm_grey", segments=40, bevel=0.004).scale.y = 0.72

# --- sofa against the left wall (seat faces +x) ------------------------------------------------------
sx, sy0, sy1 = 0.05, 0.9, 2.9  # back of the sofa at x = sx
ds.box("SofaBase", (sx + 0.45, (sy0 + sy1) / 2, 0.27), (0.9, sy1 - sy0, 0.3), "teal_dark", bevel=0.03)
for i in range(2):
    a = sy0 + 0.17 + i * (sy1 - sy0 - 0.34) / 2
    b = a + (sy1 - sy0 - 0.34) / 2
    ds.box(f"Seat{i}", (sx + 0.5, (a + b) / 2, 0.5), (0.78, b - a - 0.02, 0.16), "teal", bevel=0.05)
    ds.box(f"Back{i}", (sx + 0.17, (a + b) / 2, 0.78), (0.24, b - a - 0.02, 0.5), "teal", bevel=0.07)
for name, yy in (("ArmL", sy0 + 0.085), ("ArmR", sy1 - 0.085)):
    ds.box(name, (sx + 0.45, yy, 0.45), (0.9, 0.17, 0.5), "teal", bevel=0.06)
for xx in (sx + 0.08, sx + 0.82):
    for yy in (sy0 + 0.08, sy1 - 0.08):
        ds.cylinder("SofaLeg", (xx, yy, 0.0), 0.025, 0.12, "walnut", segments=8, radius_top=0.035)
ds.box("Pillow", (sx + 0.36, sy1 - 0.42, 0.72), (0.14, 0.38, 0.38), "mustard", bevel=0.06).rotation_euler = (0.2, -0.25, 0.1)
ds.box("FloorCushion", (1.0, 0.55, 0.06), (0.55, 0.55, 0.12), "rose", bevel=0.05).rotation_euler.z = 0.3

# --- coffee table ------------------------------------------------------------------------------------
tx, ty = 1.85, 1.9
ds.box("TableTop", (tx, ty, 0.42), (0.6, 1.0, 0.045), "oak", bevel=0.012)
ds.box("TableShelf", (tx, ty, 0.12), (0.5, 0.9, 0.025), "oak", bevel=0.008)
for dx in (-0.25, 0.25):
    for dy in (-0.45, 0.45):
        ds.box("TableLeg", (tx + dx, ty + dy, 0.2), (0.045, 0.045, 0.4), "walnut", bevel=0.008)
ds.cylinder("Mug", (tx + 0.1, ty + 0.25, 0.443), 0.04, 0.09, "red", segments=10)
for i, c in enumerate(("navy", "mustard")):
    ds.box(f"TableBook{i}", (tx - 0.05, ty - 0.2, 0.455 + i * 0.03), (0.22, 0.3, 0.028), c, bevel=0.006).rotation_euler.z = 0.2 * i

# --- pendant lamp over the table: the warm light of the room ------------------------------------------
ds.cylinder("LampCord", (tx, ty, 1.75), 0.006, H - 1.75, "charcoal", segments=6, smooth=False)
ds.cylinder("LampShade", (tx, ty, 1.55), 0.24, 0.22, "charcoal", segments=16, radius_top=0.06, bevel=0.01)
bulb = ds.blob("Bulb", (tx, ty, 1.57), 0.05, (1, 1, 1), "pale_yellow", subdiv=2)
bulb.data.materials.clear()
bulb.data.materials.append(ds.flat_material("Bulb", "#FFE6B0", emission=25.0))
bulb.visible_shadow = False
lamp = bpy.data.lights.new("PendantLight", "POINT")
lamp.energy = 55
lamp.color = ds.linear("#FFC978")
lamp.shadow_soft_size = 0.06
lo = bpy.data.objects.new("PendantLight", lamp)
lo.location = (tx, ty, 1.5)
scene.collection.objects.link(lo)

# --- bookcase against the back wall, left of the window ----------------------------------------------
bx0, bx1, bh, bd = 0.25, 1.25, 1.8, 0.32
by = D - bd / 2 - 0.02
ds.box("CaseSideL", (bx0 + 0.02, by, bh / 2), (0.04, bd, bh), "walnut", bevel=0.006)
ds.box("CaseSideR", (bx1 - 0.02, by, bh / 2), (0.04, bd, bh), "walnut", bevel=0.006)
ds.box("CaseBack", ((bx0 + bx1) / 2, D - 0.03, bh / 2), (bx1 - bx0, 0.02, bh), "walnut_dark", bevel=0)
for k in range(5):
    z = 0.04 + k * (bh - 0.08) / 4
    ds.box(f"Shelf{k}", ((bx0 + bx1) / 2, by, z), (bx1 - bx0 - 0.08, bd, 0.03), "walnut", bevel=0.006)
    if k == 4:
        continue
    x = bx0 + 0.06
    while x < bx1 - 0.12:
        th = rnd.uniform(0.025, 0.05)
        hh = rnd.uniform(0.2, 0.3)
        lean = 0.12 if rnd.random() < 0.08 else 0.0
        b = ds.box("Book", (x + th / 2, by + 0.02, z + 0.015 + hh / 2), (th, bd - 0.09, hh),
                   rnd.choice(["teal", "mustard", "rose", "navy", "linen", "red", "leaf_dark", "warm_grey"]), bevel=0.004)
        b.rotation_euler.y = lean
        x += th + 0.003 + lean * 0.2
        if rnd.random() < 0.06:
            x += 0.1
# a trailing plant on top of the bookcase
ds.cylinder("TopPot", (bx1 - 0.2, by, bh + 0.02), 0.08, 0.14, "terracotta", segments=10, radius_top=0.1)
for i in range(7):
    ds.blob(f"Trail{i}", (bx1 - 0.2 + rnd.uniform(-0.12, 0.12), by - 0.1 - i * 0.015, bh + 0.15 - i * 0.12), 0.06,
            (1.1, 0.8, 0.5), rnd.choice(["leaf", "olive", "leaf_light"]), seed=40 + i)

# --- plant on the floor under the window ---------------------------------------------------------------
px, py = 3.45, D - 0.45
ds.cylinder("Pot", (px, py, 0.0), 0.16, 0.34, "terracotta", segments=12, radius_top=0.2, bevel=0.012)
ds.cylinder("PotRim", (px, py, 0.31), 0.215, 0.05, "terracotta", segments=12, bevel=0.01)
ds.cylinder("Soil", (px, py, 0.33), 0.19, 0.02, "soil", segments=12, smooth=False)
for i in range(22):
    a = i * 2.4 + rnd.uniform(-0.2, 0.2)   # golden-angle spiral: leaves all round, none on top of each other
    r = rnd.uniform(0.1, 0.28)
    z = 0.5 + i / 22 * 0.7 + (0.28 - r) * 0.5
    leaf = ds.blob(f"Leaf{i}", (px + math.cos(a) * r, py + math.sin(a) * r, z), 0.16,
                   (1.0, 0.55, 0.22), rnd.choice(["leaf", "leaf_light", "leaf_dark", "olive"]), subdiv=1, seed=10 + i)
    leaf.rotation_euler = (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.6, 0.6), a)
ds.cylinder("Stem", (px, py, 0.33), 0.018, 0.75, "leaf_dark", segments=6, smooth=False)

# --- the cat's corner: bowl, and a toy ball ------------------------------------------------------------
ds.cylinder("Bowl", (3.4, 1.0, 0.0), 0.09, 0.05, "red", segments=14, radius_top=0.11, bevel=0.01)
ds.cylinder("Food", (3.4, 1.0, 0.035), 0.085, 0.02, "walnut", segments=14, smooth=False)
ds.box("Mat", (3.4, 1.05, 0.004), (0.42, 0.3, 0.008), "linen", bevel=0.003)
ds.blob("ToyBall", (2.9, 2.75, 0.05), 0.05, (1, 1, 1), "soft_yellow", subdiv=1, seed=3)

# --- Drahomil -------------------------------------------------------------------------------------------
CAT_AT, CAT_YAW = (2.45, 1.25, 0.0), 12   # on the rug, looking at the window (the cat faces +Y at yaw 0)
if args.cat:
    before = set(bpy.data.objects)
    # The same axes as the export in convert_lowpoly_cat.py, so the cat comes back facing +Y, feet at z = 0.
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(args.cat), axis_forward="Z", axis_up="Y")
    new = [o for o in bpy.data.objects if o not in before]
    cat = next((o for o in new if o.type == "ARMATURE"), None)
    # The clips key the rig object's own transform, so place the cat with a parent empty.
    holder = bpy.data.objects.new("Drahomil", None)
    scene.collection.objects.link(holder)
    for o in new:
        if o.parent is None:
            o.parent = holder
    holder.location = CAT_AT
    holder.rotation_euler.z = math.radians(CAT_YAW)
    for o in new:
        if o.type == "MESH":
            for m in o.data.materials:
                if m and m.name.startswith("Fur"):
                    b = m.node_tree.nodes.get("Principled BSDF")
                    b.inputs["Base Color"].default_value = (*ds.linear("#292B32"), 1)   # charcoal, not pure black
                    b.inputs["Roughness"].default_value = 0.8
                    b.inputs["Specular IOR Level"].default_value = 0.25  # no lamp highlight stripe on the back
                    b.inputs["Sheen Weight"].default_value = 0.35       # soft highlights on the ears, back and tail
                    b.inputs["Sheen Tint"].default_value = (*ds.linear("#9AA6BE"), 1)
    idle = next((a for a in bpy.data.actions if a.name.split("|")[-1] == "Idle"), None)  # "Rig|Rig|Idle"
    if cat and idle:
        if not cat.animation_data:
            cat.animation_data_create()
        cat.animation_data.action = idle
    scene.frame_set(0)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    for o in new:
        if o.type == "MESH":
            ev = o.evaluated_get(dg)
            pts = [ev.matrix_world @ v.co for v in ev.data.vertices]
            lo_ = [round(min(p[i] for p in pts), 2) for i in range(3)]
            hi_ = [round(max(p[i] for p in pts), 2) for i in range(3)]
            print("CAT", o.name, "visible", o.visible_get(), "min", lo_, "max", hi_)

# --- blue-hour light -------------------------------------------------------------------------------------
# 1. cool blue ambient from the sky, 2. the last warm, low sun through the window, 3. the warm pendant lamp,
# 4. a cool fill from the open side of the room (the twilight that the missing walls would let in).
ds.lookdev_world(scene, sky_strength=1.0)
ds.sun((-0.25, -0.9, -0.4), strength=2.6, angle_deg=3)
fill = bpy.data.lights.new("TwilightFill", "AREA")
fill.energy = 170
fill.size = 3.0
fill.color = ds.linear("#8EA2C4")
fo = bpy.data.objects.new("TwilightFill", fill)
fo.location = (3.6, -0.8, 2.0)
fo.rotation_euler = (Vector((2.0, 2.0, 0.3)) - fo.location).to_track_quat("-Z", "Y").to_euler()
scene.collection.objects.link(fo)
# 5. a soft rim on the cat from the window side: the edges of ears, back and tail separate it from dark
#    backgrounds (in Unity: a fresnel rim in the cat material, see docs/ART_STYLE.md). No glow.
rim = bpy.data.lights.new("CatRim", "AREA")
rim.energy = 6
rim.size = 0.6
rim.color = ds.linear("#F3DFA7")
ro = bpy.data.objects.new("CatRim", rim)
ro.location = (CAT_AT[0] - 0.5, CAT_AT[1] + 1.4, 0.7)
ro.rotation_euler = (Vector((CAT_AT[0], CAT_AT[1], 0.2)) - ro.location).to_track_quat("-Z", "Y").to_euler()
scene.collection.objects.link(ro)
ds.use_gpu(scene)
scene.cycles.max_bounces = 6

if args.palette:
    ds.save_palette(args.palette)
    print("PALETTE", os.path.abspath(args.palette))

os.makedirs(args.out, exist_ok=True)
# 1. the room from the orbit camera's usual distance (the same view as the first style frame)
ds.camera((4.3, -0.9, 1.45), (2.2, 1.9, 0.45), lens=26)
ds.render(os.path.join(args.out, "style_frame_game.png"), samples=args.samples)
# 2. low behind the cat, looking where it looks (the reference panels)
ds.camera((2.6, 0.1, 0.42), (2.25, 3.2, 0.42), lens=26)
ds.render(os.path.join(args.out, "style_frame_cat.png"), samples=args.samples)
# 3. readability: the black cat's side, with the dark sofa base right behind it
ds.camera((3.7, 1.0, 0.32), (2.45, 1.3, 0.18), lens=40)
ds.render(os.path.join(args.out, "style_frame_readability.png"), samples=args.samples)

if args.blend:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
