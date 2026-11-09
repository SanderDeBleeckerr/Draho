"""Draho's animals in the house style (docs/ART_STYLE.md): faceted low-poly, palette colours, one material.

Each animal keeps the parts that its game script animates (the names match what the builders wire into
Configure), so the scripts do not change.

Usage (Blender 5.x; absolute paths on Windows):
    blender -b --factory-startup --python Tools/Blender/build_animals.py -- \
        --out Draho/Assets/_Draho/Art/Animals [--animal Fox] [--blend D:/tmp/animals]

Axes: Blender front is -Y, up +Z. The FBX has axis_forward -Z, up Y; Unity bakes the axis conversion on import
(Editor/AnimalModels.cs), so in Unity the animal faces +Z with up +Y, as the game scripts expect.
"""
import argparse
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draho_style as ds  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--animal", default="all", help="Mouse, Blackbird, Pigeon, Gull, Fox, Zlatenka, Hrozna, Hedgehog, Duck_Drake, Duck_Hen, or all")
ap.add_argument("--out", required=True, help="folder for the .fbx files")
ap.add_argument("--blend", help="optional folder for .blend files (for review)")
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


# ---------------------------------------------------------------------------
# A small modelling kit. Everything below is written in UNITY coordinates (x right, y up, z forward, metres),
# so the numbers can be compared directly with the pivots in ApartmentBuilder.cs.
# Unity's FBX import turns Blender (x, y, z) into (-x, z, -y).
# ---------------------------------------------------------------------------
def to_blender(p):
    return Vector((-p[0], -p[2], p[1]))


def to_unity(p):
    return Vector((-p[0], p[2], -p[1]))


class Kit:
    """One mesh under construction. Each face remembers its palette colour."""

    def __init__(self):
        self.bm = bmesh.new()
        self.colour = self.bm.faces.layers.int.new("palette")

    def _tag(self, faces, colour):
        for f in faces:
            c = colour(to_unity(f.calc_center_median())) if callable(colour) else colour
            f[self.colour] = ds.NAMES.index(c)

    def loft(self, rings, colour, segments=6):
        """Rings along z: (z, y, rx, ry) or (z, y, rx, ry, x). Both ends are closed."""
        loops = []
        for ring in rings:
            z, y, rx, ry = ring[:4]
            x = ring[4] if len(ring) > 4 else 0.0
            loops.append([self.bm.verts.new(to_blender((
                x + math.cos((k + 0.5) / segments * math.tau) * rx,
                y + math.sin((k + 0.5) / segments * math.tau) * ry, z))) for k in range(segments)])
        faces = []
        for a, b in zip(loops, loops[1:]):
            for k in range(segments):
                j = (k + 1) % segments
                faces.append(self.bm.faces.new((a[k], a[j], b[j], b[k])))
        for loop in (loops[0], loops[-1]):
            faces.append(self.bm.faces.new(loop))
        self._close(faces, colour)

    def limb(self, a, b, ra, rb, colour, sides=5, mid=None):
        """A closed tapered tube from a to b (optionally through a middle ring (t, radius))."""
        a, b = Vector(a), Vector(b)
        axis = (b - a).normalized()
        side = axis.cross(Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))).normalized()
        up = axis.cross(side)
        stations = [(0.0, ra)] + ([mid] if mid else []) + [(1.0, rb)]
        loops = []
        for t, r in stations:
            c = a.lerp(b, t)
            loops.append([self.bm.verts.new(to_blender(c + side * (math.cos(k / sides * math.tau) * r)
                                                       + up * (math.sin(k / sides * math.tau) * r)))
                          for k in range(sides)])
        faces = []
        for p, q in zip(loops, loops[1:]):
            for k in range(sides):
                j = (k + 1) % sides
                faces.append(self.bm.faces.new((p[k], p[j], q[j], q[k])))
        for loop in (loops[0], loops[-1]):
            faces.append(self.bm.faces.new(loop))
        self._close(faces, colour)

    def ball(self, centre, radius, colour, scale=(1, 1, 1), subdiv=1, euler=(0, 0, 0)):
        rot = Euler([math.radians(e) for e in euler], "XYZ").to_matrix()
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius)
        for v in res["verts"]:
            p = rot @ Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
            v.co = to_blender(Vector(centre) + p)
        self._close({f for v in res["verts"] for f in v.link_faces}, colour)

    def _close(self, faces, colour):
        faces = list(faces)
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)  # the conversion to Blender axes mirrors the shape
        # The recalculation can guess wrong on small parts: a closed part must have a positive signed volume.
        volume = 0.0
        for f in faces:
            ring = [v.co for v in f.verts]
            for i in range(1, len(ring) - 1):
                volume += ring[0].dot(ring[i].cross(ring[i + 1]))
        if volume < 0.0:
            bmesh.ops.reverse_faces(self.bm, faces=faces)
        self._tag(faces, colour)

    def finish(self, name, parent):
        bmesh.ops.triangulate(self.bm, faces=[f for f in self.bm.faces if len(f.verts) > 4])
        layer = self.colour
        names = {}
        self.bm.faces.ensure_lookup_table()
        for f in self.bm.faces:
            names[f.index] = ds.NAMES[f[layer]]
        o = mesh_obj(name, self.bm, parent)
        o.data.attributes.remove(o.data.attributes["palette"])
        ds.paint(o, lambda p: names[p.index])
        ds.facet(o)
        return o


def pivot(name, unity_pos, parent):
    return empty(name, loc=to_blender(unity_pos), parent=parent)


# ---------------------------------------------------------------------------
# Birds: one body, three species. Sizes are for a pigeon (32 cm); s scales it. Pivots as the game expects:
# Root > Body > Head, WingL, WingR. Wings hang from the shoulder and swing outward when the bird flies.
# ---------------------------------------------------------------------------
def bird(name, s, c):
    root = empty(name)
    body_p = empty("Body", parent=root)

    def S(*v):
        return tuple(x * s for x in v)

    k = Kit()

    def plumage(p):
        if p.z > 0.052 * s:
            return c["neck"]
        return c["belly"] if p.y < 0.075 * s else c["body"]
    k.loft([(z * s, y * s, rx * s, ry * s) for z, y, rx, ry in (
        (-0.125, 0.125, 0.012, 0.010), (-0.085, 0.116, 0.038, 0.033), (-0.030, 0.105, 0.058, 0.053),
        (0.025, 0.105, 0.062, 0.060), (0.070, 0.122, 0.050, 0.053), (0.092, 0.150, 0.032, 0.036),
        (0.098, 0.180, 0.018, 0.020))], plumage, segments=8)
    k.loft([(z * s, y * s, rx * s, ry * s) for z, y, rx, ry in (
        (-0.105, 0.124, 0.026, 0.010), (-0.170, 0.112, 0.038, 0.006), (-0.235, 0.100, 0.034, 0.004))], c["tail"])
    for side in (-1, 1):
        k.limb(S(side * 0.022, 0.058, 0.012), S(side * 0.022, 0.008, 0.018), 0.0065 * s, 0.0045 * s, c["feet"], sides=4)
        k.ball(S(side * 0.022, 0.006, 0.028), 0.014 * s, c["feet"], scale=(0.8, 0.35, 1.5))
    k.finish(f"{name}_Body", body_p)

    head = pivot("Head", S(0, 0.15, 0.06), body_p)
    k = Kit()
    k.ball(S(0, 0.03, 0.032), 0.037 * s, c["head"], scale=(0.92, 0.96, 1.06), subdiv=2)
    k.limb(S(0, 0.024, 0.062), S(0, 0.013, 0.100), 0.012 * s, 0.002 * s, c["beak"], sides=4)
    for side in (-1, 1):
        if c.get("eye_ring"):
            k.ball(S(side * 0.029, 0.039, 0.045), 0.0095 * s, c["eye_ring"], scale=(0.5, 1, 1))
        k.ball(S(side * 0.0335, 0.039, 0.045), 0.0058 * s, c["eye"], scale=(0.55, 1, 1), euler=(36, 20, 0))
    if c.get("croissant"):
        for side in (-1, 1):  # one curved croissant, held across the beak: two tapered halves meeting at the beak
            k.limb(S(side * -0.008, 0.012, 0.100), S(side * 0.056, -0.012, 0.086), 0.019 * s, 0.005 * s,
                   lambda p: "oak_light" if abs(p.x) > 0.034 * s else "mustard", sides=6, mid=(0.5, 0.016 * s))
    k.finish(f"{name}_Head", head)

    for wing_name, side in (("WingL", -1), ("WingR", 1)):
        w = pivot(wing_name, S(side * 0.055, 0.14, 0), body_p)
        k = Kit()

        def feathers(p):
            z = p.z / s
            if c.get("tip") and z < -0.115:
                return c["tip"]
            if c.get("bar") and (-0.085 < z < -0.060 or -0.040 < z < -0.020):
                return c["bar"]
            return c["wing"]
        k.loft([(z * s, y * s, rx * s, ry * s, side * 0.009 * s) for z, y, rx, ry in (
            (0.045, -0.026, 0.004, 0.006), (0.018, -0.040, 0.008, 0.032), (-0.050, -0.050, 0.008, 0.037),
            (-0.112, -0.052, 0.006, 0.021), (-0.180, -0.052, 0.003, 0.004))], feathers, segments=6)
        k.finish(f"{name}_{wing_name}_Mesh", w)
    return root


def blackbird():
    return bird("Blackbird", 0.75, dict(body="charcoal", belly="cat_fur_light", neck="charcoal", tail="black", feet="walnut",
                                        head="charcoal", beak="a_beak", eye="black", eye_ring="a_beak", wing="metal_dark"))


def pigeon():
    return bird("Pigeon", 1.0, dict(body="metal", belly="grey_light", neck="teal_dark", tail="slate", feet="red",
                                    head="slate", beak="charcoal", eye="a_beak", wing="metal", bar="charcoal", tip="slate"))


def gull():
    return bird("Gull", 1.86, dict(body="white", belly="white", neck="white", tail="charcoal", feet="rose",
                                   head="white", beak="soft_yellow", eye="charcoal", wing="metal", tip="charcoal", croissant=True))


# ---------------------------------------------------------------------------
# Fox: Root > Fox_Body, Head, Leg_0..3 (front left, front right, back left, back right), Tail
# ---------------------------------------------------------------------------
def fox():
    root = empty("Fox")
    k = Kit()
    k.loft([(-0.30, 0.292, 0.014, 0.014), (-0.25, 0.292, 0.060, 0.068), (-0.12, 0.288, 0.068, 0.080),
            (0.03, 0.290, 0.070, 0.095), (0.14, 0.295, 0.075, 0.104), (0.22, 0.330, 0.054, 0.078),
            (0.30, 0.385, 0.038, 0.048)],
           lambda p: "linen" if (p.z > 0.09 and p.y < 0.285) or p.y < 0.215 else "a_fox_rust", segments=8)
    k.finish("Fox_Body", root)

    head = pivot("Head", (0, 0.39, 0.31), root)
    k = Kit()
    k.loft([(-0.065, 0.005, 0.020, 0.020), (-0.040, 0.010, 0.052, 0.046), (0.000, 0.010, 0.060, 0.052),
            (0.045, -0.006, 0.040, 0.034), (0.095, -0.020, 0.018, 0.016), (0.138, -0.026, 0.006, 0.006)],
           lambda p: "linen" if p.y < -0.018 or (abs(p.x) > 0.042 and p.y < 0.0) else "a_fox_rust", segments=8)
    k.ball((0, -0.024, 0.141), 0.012, "charcoal")
    for side in (-1, 1):
        k.ball((side * 0.034, 0.020, 0.048), 0.0095, "charcoal", scale=(0.6, 1, 1))
        k.limb((side * 0.035, 0.040, -0.015), (side * 0.046, 0.128, -0.025), 0.031, 0.002,
               lambda p: "charcoal" if p.y > 0.095 else "a_fox_rust", sides=4, mid=(0.55, 0.018))
    k.finish("Fox_Head", head)

    spots = [(-1, 0.17), (1, 0.17), (-1, -0.2), (1, -0.2)]
    for i, (side, z) in enumerate(spots):
        leg = pivot(f"Leg_{i}", (side * 0.045, 0.24, z), root)
        k = Kit()
        k.limb((0, 0.03, 0), (0, -0.095, -0.006), 0.036, 0.021, "a_fox_rust", sides=5)
        k.limb((0, -0.09, -0.006), (0, -0.226, 0.004), 0.021, 0.014, "metal_dark", sides=5)
        k.ball((0, -0.229, 0.018), 0.021, "metal_dark", scale=(1, 0.52, 1.5))
        k.finish(f"Fox_Leg_{i}_Mesh", leg)

    tail = pivot("Tail", (0, 0.3, -0.27), root)
    k = Kit()
    k.loft([(0.02, 0.0, 0.020, 0.020), (-0.06, -0.02, 0.050, 0.050), (-0.20, -0.06, 0.070, 0.068),
            (-0.34, -0.10, 0.054, 0.052), (-0.42, -0.125, 0.022, 0.022), (-0.455, -0.135, 0.004, 0.004)],
           lambda p: "linen" if p.z < -0.345 else "a_fox_rust", segments=7)
    k.finish("Fox_Tail", tail)
    return root


# ---------------------------------------------------------------------------
# Zlatenka, the leopard tortoise: Root > Zlatenka_Shell (shell, legs, tail), Head
# ---------------------------------------------------------------------------
def tortoise():
    root = empty("Zlatenka")
    rnd = random.Random(3)
    spots = []                     # leopard markings: dark patches scattered over the dome
    for _ in range(26):
        a, up = rnd.uniform(0, math.tau), rnd.uniform(0.12, 0.98)
        spots.append(Vector((math.cos(a) * math.sqrt(1 - up * up) * 0.16, 0.10 + up * 0.09,
                             math.sin(a) * math.sqrt(1 - up * up) * 0.195)))

    def shell(p):
        if p.y < 0.078:
            return "walnut"        # the rim above the legs
        return "walnut_dark" if any((p - s).length < 0.026 for s in spots) else "mustard"
    k = Kit()
    k.ball((0, 0.10, 0), 0.5, shell, scale=(0.33, 0.19, 0.40), subdiv=3)
    skin = "oak_light"
    for side in (-1, 1):
        for z, lean in ((0.105, 0.03), (-0.105, -0.03)):
            k.limb((side * 0.105, 0.08, z), (side * 0.15, 0.026, z + lean), 0.040, 0.026, skin, sides=6)
            k.ball((side * 0.152, 0.016, z + lean * 1.3), 0.036, skin, scale=(1, 0.42, 1.15))
    k.limb((0, 0.06, -0.17), (0, 0.04, -0.235), 0.02, 0.003, skin, sides=4)
    k.finish("Zlatenka_Shell", root)

    head = pivot("Head", (0, 0.07, 0.19), root)
    k = Kit()
    k.limb((0, -0.012, -0.05), (0, 0.01, 0.05), 0.033, 0.026, skin, sides=6)
    k.ball((0, 0.018, 0.076), 0.5, skin, scale=(0.066, 0.056, 0.092), subdiv=2)
    for side in (-1, 1):
        k.ball((side * 0.028, 0.028, 0.088), 0.0078, "charcoal", scale=(0.6, 1, 1))
    k.finish("Zlatenka_Head", head)
    return root


# ---------------------------------------------------------------------------
# Hrozna, the red Cardigan corgi: Root > Body (mesh with head and legs) > Tail
# ---------------------------------------------------------------------------
def hrozna():
    root = empty("Hrozna")
    body = empty("Body", parent=root)
    k = Kit()
    k.loft([(-0.31, 0.215, 0.022, 0.020), (-0.26, 0.210, 0.108, 0.098), (-0.10, 0.200, 0.126, 0.115),
            (0.10, 0.200, 0.126, 0.120), (0.22, 0.212, 0.102, 0.112), (0.29, 0.272, 0.070, 0.082),
            (0.35, 0.330, 0.032, 0.034)],
           lambda p: "linen" if (p.z > 0.13 and p.y < 0.27) or p.y < 0.125 else "a_corgi_tan", segments=8)

    def face(p):
        return "linen" if p.y < 0.312 or abs(p.x) < 0.02 else "a_corgi_tan"
    k.loft([(0.235, 0.345, 0.030, 0.030), (0.270, 0.348, 0.095, 0.090), (0.360, 0.348, 0.102, 0.095),
            (0.415, 0.314, 0.066, 0.056), (0.462, 0.298, 0.040, 0.034), (0.490, 0.296, 0.010, 0.010)], face, segments=8)
    k.ball((0, 0.304, 0.490), 0.022, "charcoal")
    for side in (-1, 1):
        k.ball((side * 0.054, 0.364, 0.395), 0.018, "charcoal", scale=(0.65, 1, 1))
        k.limb((side * 0.070, 0.405, 0.305), (side * 0.102, 0.535, 0.298), 0.066, 0.008, "a_corgi_tan", sides=5,
               mid=(0.6, 0.044))
        for z in (0.19, -0.19):
            k.limb((side * 0.086, 0.135, z), (side * 0.086, 0.018, z), 0.047, 0.038, "linen", sides=6)
            k.ball((side * 0.086, 0.017, z + 0.018), 0.043, "linen", scale=(1, 0.42, 1.3))
    k.finish("Hrozna_Body", body)

    tail = pivot("Tail", (0, 0.22, -0.3), body)
    k = Kit()
    k.loft([(0.03, 0.0, 0.024, 0.024), (-0.09, -0.012, 0.054, 0.054), (-0.25, -0.042, 0.062, 0.060),
            (-0.38, -0.066, 0.030, 0.030), (-0.445, -0.076, 0.005, 0.005)],
           lambda p: "linen" if p.z < -0.355 else "a_corgi_tan", segments=6)
    k.finish("Hrozna_Tail", tail)
    return root


# ---------------------------------------------------------------------------
# Hedgehog: Root > Body (skin and spines) > Snout (face and legs: hidden when it curls up)
# ---------------------------------------------------------------------------
def hedgehog():
    root = empty("Hedgehog")
    body = empty("Body", parent=root)
    k = Kit()
    k.ball((0, 0.06, 0), 0.5, "walnut", scale=(0.15, 0.104, 0.215), subdiv=2)
    radii = Vector((0.075, 0.052, 0.1075))
    rnd = random.Random(101)
    golden = math.pi * (3 - math.sqrt(5))
    count = 0
    for i in range(330):           # an even spiral over the sphere; only the back gets spines
        y = 1 - (i + 0.5) / 330 * 2
        r = math.sqrt(1 - y * y)
        a = i * golden
        n = Vector((math.cos(a) * r, y, math.sin(a) * r))
        if n.y < -0.05 or n.z > 0.72:
            continue
        base = Vector((0, 0.06, 0)) + Vector((n.x * radii.x, n.y * radii.y, n.z * radii.z)) * 0.93
        lean = (n + Vector((0, 0.1, -0.95))).normalized()   # spines lie back over the body
        length = rnd.uniform(0.038, 0.05)
        tip = base + lean * length
        k.limb(base, tip, 0.0085, 0.0008, lambda p: "linen" if (p - base).length > length * 0.58 else "walnut_dark",
               sides=3, mid=(0.62, 0.0042))
        count += 1
    print(f"CHECK hedgehog spines {count}")
    k.finish("Hedgehog_Back", body)

    snout = empty("Snout", parent=body)
    k = Kit()
    k.loft([(0.055, 0.047, 0.046, 0.040), (0.100, 0.047, 0.042, 0.036), (0.142, 0.040, 0.022, 0.020),
            (0.176, 0.035, 0.006, 0.006)], "oak_light", segments=7)
    k.ball((0, 0.035, 0.179), 0.0078, "charcoal")
    for side in (-1, 1):
        k.ball((side * 0.025, 0.060, 0.130), 0.0066, "charcoal", scale=(0.7, 1, 1))
        k.ball((side * 0.033, 0.078, 0.098), 0.0125, "oak", scale=(1, 1, 0.45))
        k.ball((side * 0.037, 0.020, 0.085), 0.021, "oak", scale=(0.85, 1.1, 1.25))
        k.ball((side * 0.048, 0.016, -0.065), 0.018, "oak", scale=(0.85, 0.9, 1.25))
    k.finish("Hedgehog_Snout", snout)
    return root


# ---------------------------------------------------------------------------
# Ducks: Root (on the water) > Body > Head
# ---------------------------------------------------------------------------
def duck(name, drake):
    root = empty(name)
    body = empty("Body", parent=root)
    rnd = random.Random(8)
    mottle = {}

    def plumage(p):
        if drake:
            if p.z < -0.135:
                return "charcoal"
            if p.y > 0.128 and p.z > 0.1:
                return "white" if p.y < 0.148 else "a_drake_green"
            return "walnut" if p.z > 0.095 else "stone"
        key = (round(p.x, 3), round(p.y, 3), round(p.z, 3))
        return mottle.setdefault(key, rnd.choice(["oak", "oak", "walnut", "oak_light"]))
    k = Kit()
    k.loft([(-0.255, 0.128, 0.008, 0.008), (-0.195, 0.102, 0.050, 0.034), (-0.10, 0.072, 0.090, 0.066),
            (0.03, 0.066, 0.100, 0.074), (0.14, 0.080, 0.076, 0.064), (0.195, 0.108, 0.030, 0.032)], plumage, segments=8)
    k.limb((0, 0.095, 0.145), (0, 0.178, 0.150), 0.044, 0.034, plumage, sides=7)
    for side in (-1, 1):
        k.loft([(0.075, 0.092, 0.012, 0.008, side * 0.083), (0.02, 0.086, 0.016, 0.040, side * 0.094),
                (-0.09, 0.094, 0.014, 0.040, side * 0.084), (-0.19, 0.112, 0.008, 0.006, side * 0.046)],
               ("metal" if drake else "walnut"), segments=5)
    k.finish(f"{name}_Body", body)

    head = pivot("Head", (0, 0.17, 0.15), body)
    k = Kit()
    k.ball((0, 0.012, 0.006), 0.5, "a_drake_green" if drake else "oak", scale=(0.104, 0.104, 0.122), subdiv=2)
    k.loft([(0.046, -0.010, 0.026, 0.013), (0.098, -0.016, 0.026, 0.007), (0.130, -0.018, 0.019, 0.004)], "a_beak", segments=5)
    for side in (-1, 1):
        k.ball((side * 0.046, 0.024, 0.032), 0.0088, "charcoal", scale=(0.6, 1, 1))
    k.finish(f"{name}_Head", head)
    return root


BUILDERS = {
    "Mouse": mouse, "Blackbird": blackbird, "Pigeon": pigeon, "Gull": gull, "Fox": fox, "Zlatenka": tortoise,
    "Hrozna": hrozna, "Hedgehog": hedgehog,
    "Duck_Drake": lambda: duck("Duck_Drake", True), "Duck_Hen": lambda: duck("Duck_Hen", False),
}


def export(name, out_path, blend_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root = BUILDERS[name]()
    bpy.context.view_layer.update()
    meshes = [o for o in root.children_recursive if o.type == "MESH"]
    tris = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
    pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 3) for i in range(3)]
    print(f"CHECK {name}: {len(meshes)} meshes, {tris} triangles, width {size[0]} length {size[1]} height {size[2]} m, "
          f"lowest {round(min(p.z for p in pts), 4)}")

    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in [root] + list(root.children_recursive):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    # FBX in Blender's own axes (-Y forward, Z up), WITHOUT "apply transform": that option converts the meshes but
    # not the pivots (empties). Unity converts the whole hierarchy on import (bakeAxisConversion, Editor/AnimalModels.cs).
    bpy.ops.export_scene.fbx(
        filepath=os.path.abspath(out_path), use_selection=True, object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True, use_tspace=False,
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False, axis_forward="-Y", axis_up="Z",
        bake_anim=False, use_custom_props=False, path_mode="STRIP")
    print("EXPORTED", os.path.abspath(out_path), os.path.getsize(os.path.abspath(out_path)), "bytes")
    if blend_path:
        os.makedirs(os.path.dirname(os.path.abspath(blend_path)), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend_path))


for animal in (BUILDERS if args.animal == "all" else [args.animal]):
    export(animal, os.path.join(args.out, f"{animal}.fbx"),
           os.path.join(args.blend, f"{animal}.blend") if args.blend else None)
