"""Draho house style for Blender scripts: the shared palette, mesh styling and a look-dev render setup.

See docs/ART_STYLE.md. Every stylised asset gets its colours from ONE palette texture (DrahoPalette.png):
each face's UVs point at the centre of one colour cell, so a whole room can share one material
(few draw calls on a phone) and a colour change is one edit here.

Import it from another script in Tools/Blender/:
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import draho_style as ds
"""
import json
import math
import os

import bmesh
import bpy
from mathutils import Vector

# ---------------------------------------------------------------------------
# The palette (sRGB hex). Keep the order: a colour's cell index is its position.
# Add new colours at the END only, or existing models point at the wrong cell.
# ---------------------------------------------------------------------------
PALETTE = [
    # Blue-hour direction (docs/ART_STYLE.md): muted, natural colours; warm yellow only for light and accents.
    # cat
    ("cat_fur", "#292B32"), ("cat_fur_light", "#3C3F49"), ("cat_eye", "#D8A94F"), ("cat_nose", "#5A4A50"),
    ("cat_inner_ear", "#9E7F82"),
    # wood (natural brown)
    ("oak_light", "#A8977F"), ("oak", "#8C7B67"), ("walnut", "#6B5545"), ("walnut_dark", "#4A3D35"),
    # walls and stone (warm grey, off-white)
    ("plaster", "#DED9CF"), ("plaster_warm", "#CFC6B8"), ("plaster_shade", "#B7B0A5"),
    ("brick", "#8E6E62"), ("brick_dark", "#6A534B"), ("mortar", "#B9B2A6"),
    ("terracotta", "#A47F69"), ("slate", "#4E5868"), ("stone", "#A7A29B"),
    # fabric (muted blues, a dusty rose, soft yellow)
    ("teal", "#5F7A80"), ("teal_dark", "#46596A"), ("mustard", "#C9AE72"), ("rose", "#A08484"),
    ("linen", "#CFC7B6"), ("navy", "#38465C"),
    # plants and garden (muted olive)
    ("leaf_light", "#A3A77E"), ("leaf", "#858B69"), ("leaf_dark", "#5C6450"), ("grass", "#7E8562"),
    ("soil", "#5A4A3E"), ("path", "#B3AA98"),
    # metal and accents
    ("metal", "#8E949C"), ("metal_dark", "#3D424B"), ("red", "#B0645B"), ("cream", "#F3DFA7"),
    ("black", "#292B32"), ("white", "#ECE8DF"),
    # town
    ("roof_tile", "#7E5A50"), ("roof_slate", "#4B5466"), ("water", "#5A7088"), ("sky", "#687D9B"),
    # the brief's reference colours, by their own names
    ("deep_blue_grey", "#38465C"), ("muted_blue", "#687D9B"), ("soft_yellow", "#E8C77D"),
    ("pale_yellow", "#F3DFA7"), ("olive", "#858B69"), ("warm_grey", "#A7A29B"), ("charcoal", "#292B32"),
    ("off_white", "#DED9CF"),
]
NAMES = [n for n, _ in PALETTE]
GRID = 8            # cells per row
CELL = 32           # pixels per cell -> 256 x 256 texture, room for 64 colours
SIZE = GRID * CELL

# Look-dev light. Unity's lights should match these (see docs/ART_STYLE.md, section Light).
SUN_COLOR = "#F2B868"   # the last low sun of the evening
SKY_COLOR = "#687D9B"   # muted blue: the blue-hour ambient


def srgb(hex_):
    h = hex_.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def linear(hex_):
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb(hex_))


def cell_uv(name):
    """UV of the centre of a colour's cell (row 0 at the top of the image, as in Unity's view of the PNG)."""
    i = NAMES.index(name)
    col, row = i % GRID, i // GRID
    return Vector(((col + 0.5) / GRID, 1 - (row + 0.5) / GRID))


# ---------------------------------------------------------------------------
# Palette texture and material
# ---------------------------------------------------------------------------
def palette_image():
    img = bpy.data.images.get("DrahoPalette")
    if img:
        return img
    img = bpy.data.images.new("DrahoPalette", SIZE, SIZE, alpha=False)
    px = [0.0] * (SIZE * SIZE * 4)
    for i, (_, hex_) in enumerate(PALETTE):
        r, g, b = srgb(hex_)
        col, row = i % GRID, i // GRID
        for y in range(SIZE - (row + 1) * CELL, SIZE - row * CELL):   # Blender rows start at the bottom
            for x in range(col * CELL, (col + 1) * CELL):
                k = (y * SIZE + x) * 4
                px[k:k + 4] = (r, g, b, 1.0)
    img.pixels.foreach_set(px)
    return img


def save_palette(png_path):
    """Write DrahoPalette.png and DrahoPalette.json (name -> hex and UV) for Unity."""
    os.makedirs(os.path.dirname(os.path.abspath(png_path)), exist_ok=True)
    img = palette_image()
    img.filepath_raw = os.path.abspath(png_path)
    img.file_format = "PNG"
    img.save()
    data = {"grid": GRID, "cell": CELL, "size": SIZE,
            "colors": [{"name": n, "hex": h, "u": round(cell_uv(n).x, 5), "v": round(cell_uv(n).y, 5)}
                       for n, h in PALETTE]}
    with open(os.path.splitext(png_path)[0] + ".json", "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)


def palette_material(roughness=0.75):
    m = bpy.data.materials.get("DrahoPalette")
    if m:
        return m
    m = bpy.data.materials.new("DrahoPalette")
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = palette_image()
    tex.interpolation = "Closest"   # Unity: Filter Mode = Point, no mipmaps
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    return m


def flat_material(name, hex_, roughness=0.6, emission=0.0, alpha=1.0):
    """For the few things that are not on the palette (glass, light bulbs)."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*linear(hex_), 1)
    b.inputs["Roughness"].default_value = roughness
    if emission:
        b.inputs["Emission Color"].default_value = (*linear(hex_), 1)
        b.inputs["Emission Strength"].default_value = emission
    if alpha < 1:
        b.inputs["Alpha"].default_value = alpha
        b.inputs["Transmission Weight"].default_value = 0.9
    return m


# ---------------------------------------------------------------------------
# Mesh styling
# ---------------------------------------------------------------------------
def paint(obj, colour):
    """Put the whole mesh on one palette colour. `colour` is a palette name, or a function face -> name."""
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for p in me.polygons:
        c = cell_uv(colour(p) if callable(colour) else colour)
        for li in p.loop_indices:
            uv[li].uv = c
        p.material_index = 0
    me.materials.clear()
    me.materials.append(palette_material())


def paint_by_material(obj, mapping, default="plaster"):
    """Imported models: map each material name (lower case, substring match) to a palette name."""
    names = [(m.name.lower() if m else "") for m in obj.data.materials]

    def pick(p):
        n = names[p.material_index] if p.material_index < len(names) else ""
        for key, colour in mapping.items():
            if key in n:
                return colour
        return default
    paint(obj, pick)


def soften(obj, bevel=0.012, segments=2, angle=35):
    """Hard-surface style: small bevels that catch the light, weighted normals, smooth shading.
    Organic shapes (cat, leaves, rocks) stay faceted: call facet() instead."""
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = bevel
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(angle)
    mod.harden_normals = True
    obj.data.shade_smooth()
    wn = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.keep_sharp = True
    apply_modifiers(obj)


def facet(obj):
    obj.data.shade_flat()


def apply_modifiers(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    obj.modifiers.clear()
    old = obj.data
    obj.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)


# ---------------------------------------------------------------------------
# Quick primitives (bmesh, no operator context needed). Sizes in metres, z up.
# ---------------------------------------------------------------------------
def _obj(name, bm, loc, collection=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    o.location = loc
    (collection or bpy.context.scene.collection).objects.link(o)
    return o


def box(name, center, size, colour, bevel=0.012):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    o = _obj(name, bm, center)
    if bevel:
        soften(o, bevel=min(bevel, min(size) * 0.3))
    paint(o, colour)
    return o


def cylinder(name, base_center, radius, height, colour, segments=12, radius_top=None, bevel=0.01, smooth=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=radius,
                          radius2=radius if radius_top is None else radius_top, depth=height)
    bmesh.ops.translate(bm, vec=Vector((0, 0, height / 2)), verts=bm.verts)
    o = _obj(name, bm, base_center)
    if smooth:
        soften(o, bevel=bevel, angle=50)   # 50 deg: the sides stay one smooth surface, the rims get a bevel
    else:
        facet(o)
    paint(o, colour)
    return o


def blob(name, center, radius, scale, colour, subdiv=1, seed=0):
    """A faceted low-poly lump (leaves, bushes, stones)."""
    import random
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    for v in bm.verts:
        v.co *= 1 + rnd.uniform(-0.12, 0.12)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    o = _obj(name, bm, center)
    facet(o)
    paint(o, colour)
    return o


# ---------------------------------------------------------------------------
# Look-dev render
# ---------------------------------------------------------------------------
def use_gpu(scene):
    """Cycles on the GPU when there is one (OptiX, then CUDA, HIP), else the CPU."""
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ("OPTIX", "CUDA", "HIP", "METAL", "ONEAPI"):
        try:
            prefs.compute_device_type = kind
        except TypeError:
            continue
        prefs.get_devices()
        gpus = [d for d in prefs.devices if d.type == kind]
        if gpus:
            for d in prefs.devices:
                d.use = d.type == kind
            scene.cycles.device = "GPU"
            print(f"RENDER on {kind}: {', '.join(d.name for d in gpus)}")
            return kind
    scene.cycles.device = "CPU"
    print("RENDER on CPU")
    return "CPU"


def lookdev_world(scene, sky_strength=0.7):
    world = bpy.data.worlds.new("DrahoSky")
    scene.world = world
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (*linear(SKY_COLOR), 1)
    bg.inputs["Strength"].default_value = sky_strength
    vs = scene.view_settings
    for transform in ("Khronos PBR Neutral", "AgX", "Standard"):   # PBR Neutral is closest to Unity's Neutral tonemap
        try:
            vs.view_transform = transform
            break
        except TypeError:
            continue
    return world


def sun(direction, strength=3.5, angle_deg=4, colour=SUN_COLOR):
    light = bpy.data.lights.new("Sun", "SUN")
    light.energy = strength
    light.color = linear(colour)
    light.angle = math.radians(angle_deg)
    o = bpy.data.objects.new("Sun", light)
    o.rotation_euler = Vector(direction).normalized().to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(o)
    return o


def camera(loc, target, lens=35):
    scene = bpy.context.scene
    cam = scene.camera
    if cam is None:
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
        scene.collection.objects.link(cam)
        scene.camera = cam
    cam.location = loc
    cam.data.lens = lens
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return cam


def render(path, res=(1600, 900), samples=128):
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.filepath = os.path.abspath(path)   # Blender reads relative paths from the .blend folder, not the cwd
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
