#!/usr/bin/env python3
"""Measure a real plot in Flanders from open Digitaal Vlaanderen data.

Give a WGS84 point. The script downloads GRB buildings and parcels (WFS) and
summer + winter orthophotos (WMS), measures the open space of the parcel,
and draws overlays. All output goes into --out only.

Data: Digitaal Vlaanderen, open under the "Modellicentie Gratis Hergebruik".

Install:
    pip install pyproj shapely pillow

Example (fake point, central Ghent):
    python survey.py --lat 51.0500 --lon 3.7300 --out /tmp/survey

List orthophoto layer names (they change every year):
    curl -sS "https://geo.api.vlaanderen.be/OMW/wms?SERVICE=WMS&REQUEST=GetCapabilities&VERSION=1.3.0" \
        | grep -o '<Name>[^<]*</Name>'
"""
import argparse
import io
import json
import math
import os
import sys
import urllib.parse
import urllib.request

from PIL import Image, ImageDraw
from pyproj import Transformer
from shapely.geometry import Point, mapping, shape
from shapely.ops import unary_union

WFS_URL = "https://geo.api.vlaanderen.be/GRB/wfs"
SUMMER_URL = "https://geo.api.vlaanderen.be/OMZ/wms"
WINTER_URL = "https://geo.api.vlaanderen.be/OMW/wms"
MOSAIC_URL = "https://geo.api.vlaanderen.be/OMWRGBMRVL/wms"
MAX_PX = 2048  # MaxWidth/MaxHeight in the WMS GetCapabilities

TO_L72 = Transformer.from_crs("EPSG:4326", "EPSG:31370", always_xy=True)
TO_WGS = Transformer.from_crs("EPSG:31370", "EPSG:4326", always_xy=True)


def fetch(url, params):
    full = url + "?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(full, headers={"User-Agent": "flemish-site-survey/1.0"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read(), r.headers.get("Content-Type", "")


def wfs(typename, minx, miny, maxx, maxy):
    """Get GRB features in Lambert 72. The BBOX is in WGS84 (lat,lon order)."""
    lons, lats = TO_WGS.transform([minx, maxx, minx, maxx], [miny, miny, maxy, maxy])
    bbox = "%.7f,%.7f,%.7f,%.7f,urn:ogc:def:crs:EPSG::4326" % (min(lats), min(lons), max(lats), max(lons))
    data, ctype = fetch(WFS_URL, {
        "SERVICE": "WFS", "VERSION": "2.0.0", "REQUEST": "GetFeature",
        "TYPENAMES": typename, "SRSNAME": "EPSG:31370",
        "OUTPUTFORMAT": "application/json", "BBOX": bbox,
    })
    try:
        return json.loads(data)
    except ValueError:
        sys.exit("WFS %s did not return JSON (%s): %s" % (typename, ctype, data[:300]))


def wms(url, layer, minx, miny, size_m, res):
    """Get a square orthophoto in Lambert 72. Returns a PIL image or None."""
    px = int(round(size_m / res))
    if px > MAX_PX:
        print("  %s: %d px is over the WMS limit; using %d px (%.3f m/px)" % (layer, px, MAX_PX, size_m / MAX_PX))
        px = MAX_PX
    try:
        data, ctype = fetch(url, {
            "SERVICE": "WMS", "VERSION": "1.3.0", "REQUEST": "GetMap",
            "LAYERS": layer, "STYLES": "", "CRS": "EPSG:31370",
            "BBOX": "%.3f,%.3f,%.3f,%.3f" % (minx, miny, minx + size_m, miny + size_m),
            "WIDTH": px, "HEIGHT": px, "FORMAT": "image/png",
        })
    except Exception as e:  # network or HTTP error
        print("  WMS %s failed: %s" % (layer, e))
        return None
    if "image" not in ctype:
        print("  WMS %s returned %s: %s" % (layer, ctype, data[:200]))
        return None
    return Image.open(io.BytesIO(data)).convert("RGB")


class Frame:
    """Maps Lambert 72 metres to pixels of a square image."""

    def __init__(self, cx, cy, half, img):
        self.minx, self.maxy = cx - half, cy + half
        self.sx = img.width / (2 * half)
        self.sy = img.height / (2 * half)

    def px(self, x, y):
        return ((x - self.minx) * self.sx, (self.maxy - y) * self.sy)


def rings(geom):
    polys = getattr(geom, "geoms", [geom])
    for p in polys:
        if p.geom_type != "Polygon" or p.is_empty:
            continue
        yield list(p.exterior.coords)
        for i in p.interiors:
            yield list(i.coords)


def draw_overlay(img, frame, buildings, parcel, pt, res):
    img = img.copy()
    d = ImageDraw.Draw(img)
    w = max(2, int(0.1 / res))
    for b in buildings:
        for r in rings(b):
            d.line([frame.px(*c) for c in r], fill=(255, 40, 40), width=w)
    if parcel is not None:
        for r in rings(parcel):
            d.line([frame.px(*c) for c in r], fill=(255, 230, 0), width=w)
    x, y = frame.px(pt.x, pt.y)
    rad = max(6, int(0.6 / res))
    d.ellipse([x - rad, y - rad, x + rad, y + rad], outline=(0, 255, 255), width=w)
    scale_bar(d, img, frame.sx)
    return img


def scale_bar(d, img, px_per_m, metres=5):
    L = metres * px_per_m
    x0, y0 = 20, img.height - 30
    d.rectangle([x0 - 6, y0 - 22, x0 + L + 40, y0 + 14], fill=(0, 0, 0))
    d.line([x0, y0, x0 + L, y0], fill=(255, 255, 255), width=5)
    d.text((x0 + L + 6, y0 - 8), "%d m" % metres, fill=(255, 255, 255))


def long_axis(geom, pt):
    """Return (length, width, angle_deg, centre) of the min rotated rectangle.

    The angle is degrees from east, counter-clockwise, and points along the
    long side away from the point (towards the far end of the open area).
    """
    rect = geom.minimum_rotated_rectangle
    c = list(rect.exterior.coords)[:4]
    sides = []
    for i in range(2):
        (x0, y0), (x1, y1) = c[i], c[i + 1]
        sides.append((math.hypot(x1 - x0, y1 - y0), math.degrees(math.atan2(y1 - y0, x1 - x0))))
    sides.sort(reverse=True)
    (length, ang), (width, ang2) = sides
    cen = rect.centroid
    ux, uy = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    if (cen.x - pt.x) * ux + (cen.y - pt.y) * uy < 0:
        ang += 180
    return length, width, ang % 360, ang2 % 360, cen


def strip(args, layer_url, layer, open_geom, buildings, parcel, pt, ang, cen):
    """Download centred on the open area, rotate so the long axis is horizontal, crop."""
    minx, miny, maxx, maxy = open_geom.bounds
    half = math.hypot(maxx - minx, maxy - miny) / 2 + 4
    img = wms(layer_url, layer, cen.x - half, cen.y - half, 2 * half, args.res)
    if img is None:
        return None
    frame = Frame(cen.x, cen.y, half, img)
    img = draw_overlay(img, frame, buildings, parcel, pt, args.res)
    rot = 180 - ang
    if args.flip:
        rot += 180
    out = img.rotate(rot, resample=Image.BICUBIC, expand=False, center=(img.width / 2, img.height / 2))
    # Rotate the open polygon the same way (PIL rotates counter-clockwise on screen).
    ox, oy = img.width / 2, img.height / 2
    t = math.radians(rot)
    pts = []
    for r in rings(open_geom):
        for x, y in r:
            px, py = frame.px(x, y)
            dx, dy = px - ox, py - oy
            pts.append((ox + dx * math.cos(t) + dy * math.sin(t), oy - dx * math.sin(t) + dy * math.cos(t)))
    pad = 2 / args.res
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    box = (max(0, int(min(xs) - pad)), max(0, int(min(ys) - pad)),
           min(out.width, int(max(xs) + pad)), min(out.height, int(max(ys) + pad)))
    out = out.crop(box)
    d = ImageDraw.Draw(out)
    scale_bar(d, out, frame.sx)
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--lat", type=float, required=True)
    ap.add_argument("--lon", type=float, required=True)
    ap.add_argument("--out", required=True, help="output directory (keep it out of the repo)")
    ap.add_argument("--radius", type=float, default=25.0, help="half size of the area in metres")
    ap.add_argument("--res", type=float, default=0.05, help="photo resolution in m/px")
    ap.add_argument("--summer-layer", default="OMZRGB24VL")
    ap.add_argument("--winter-layer", default="OMWRGB25VL")
    ap.add_argument("--mosaic-layer", default="Ortho", help="layer of the current mosaic service")
    ap.add_argument("--min-part", type=float, default=1.0,
                    help="drop open-space pieces smaller than this (m2)")
    ap.add_argument("--flip", action="store_true", help="turn the strip 180 degrees")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    x, y = TO_L72.transform(args.lon, args.lat)
    pt = Point(x, y)
    R = args.radius
    lines = []

    def say(s=""):
        print(s)
        lines.append(s)

    say("Point converted to Lambert 72 (EPSG:31370); area %.0f x %.0f m" % (2 * R, 2 * R))
    gbg = wfs("GRB:GBG", x - R, y - R, x + R, y + R)
    adp = wfs("GRB:ADP", x - R, y - R, x + R, y + R)
    json.dump(gbg, open(os.path.join(args.out, "grb_gbg.json"), "w"))
    json.dump(adp, open(os.path.join(args.out, "grb_adp.json"), "w"))

    buildings, btypes = [], []
    for f in gbg.get("features", []):
        g = shape(f["geometry"]).buffer(0)
        buildings.append(g)
        btypes.append(f["properties"].get("LBLTYPE", "?"))
    parcels = [(shape(f["geometry"]).buffer(0), f["properties"].get("CAPAKEY", "?"))
               for f in adp.get("features", [])]
    say("Buildings in area: %d (%s)" % (len(buildings), ", ".join(sorted(set(btypes))) or "-"))
    say("Parcels in area: %d" % len(parcels))

    near = [p for p in parcels if p[0].distance(pt) <= 1.0]
    if not near:
        say("No parcel within 1 m of the point (public domain such as streets and squares has no parcel).")
        if parcels:
            p = min(parcels, key=lambda q: q[0].distance(pt))
            say("Nearest parcel is %.1f m away; using it." % p[0].distance(pt))
            near = [p]
    bunion = unary_union(buildings) if buildings else None
    if buildings:
        say("Distance point -> nearest building: %.2f m" % min(b.distance(pt) for b in buildings))

    parcel = unary_union([p[0] for p in near]) if near else None
    open_geom = None
    summary = {"radius_m": R, "res_m_per_px": args.res, "parcel_count": len(near)}
    if parcel is not None:
        open_geom = parcel.difference(bunion) if bunion is not None else parcel
        # Drop slivers left by tiny gaps between GRB parcel and building edges.
        parts = [g for g in getattr(open_geom, "geoms", [open_geom])
                 if g.geom_type == "Polygon" and g.area >= args.min_part]
        open_geom = unary_union(parts).simplify(0.02) if parts else open_geom.__class__()
        say("Parcel area: %.1f m2" % parcel.area)
        say("Open space (parcel minus buildings): %.1f m2" % open_geom.area)
        if open_geom.is_empty:
            say("Open space is empty (the parcel is fully built).")
            open_geom = None
    if open_geom is not None:
        length, width, ang, ang2, cen = long_axis(open_geom, pt)
        say("Min rotated rectangle: long side %.2f m at %.1f deg, short side %.2f m at %.1f deg"
            % (length, ang, width, ang2))
        say("  (angles: degrees from east, counter-clockwise; long angle points away from the point)")
        say("Open polygon relative to the point (dx east, dy north, metres):")
        rel = []
        for r in rings(open_geom):
            ring = [(round(cx - x, 2), round(cy - y, 2)) for cx, cy in r]
            rel.append(ring)
            say("  " + " ".join("(%.2f,%.2f)" % c for c in ring))
        summary.update({
            "parcel_area_m2": round(parcel.area, 2), "open_area_m2": round(open_geom.area, 2),
            "long_side_m": round(length, 2), "short_side_m": round(width, 2),
            "long_angle_deg": round(ang, 1), "short_angle_deg": round(ang2, 1),
            "open_rings_rel_m": rel,
        })
        json.dump({"type": "Feature", "properties": {}, "geometry": mapping(open_geom)},
                  open(os.path.join(args.out, "open_space_l72.json"), "w"))

    # Square photos centred on the point.
    sheets = []
    for name, url, layer in (("summer", SUMMER_URL, args.summer_layer),
                             ("winter", WINTER_URL, args.winter_layer),
                             ("mosaic", MOSAIC_URL, args.mosaic_layer)):
        img = wms(url, layer, x - R, y - R, 2 * R, args.res)
        if img is None:
            continue
        frame = Frame(x, y, R, img)
        img.save(os.path.join(args.out, "%s_raw.png" % name))
        draw_overlay(img, frame, buildings, parcel, pt, args.res).save(
            os.path.join(args.out, "%s_overlay.png" % name))
        say("Saved %s_overlay.png (%s, %dx%d px)" % (name, layer, img.width, img.height))

    # Strip view: long axis horizontal, far end left, house right.
    if open_geom is not None:
        for name, url, layer in (("summer", SUMMER_URL, args.summer_layer),
                                 ("winter", WINTER_URL, args.winter_layer)):
            s = strip(args, url, layer, open_geom, buildings, parcel, pt, ang, cen)
            if s is not None:
                s.save(os.path.join(args.out, "%s_strip.png" % name))
                sheets.append(s)
        if sheets:
            W = max(s.width for s in sheets)
            H = sum(s.height for s in sheets) + 10 * (len(sheets) - 1)
            sheet = Image.new("RGB", (W, H), (30, 30, 30))
            yy = 0
            for s in sheets:
                sheet.paste(s, (0, yy))
                yy += s.height + 10
            sheet.save(os.path.join(args.out, "strip_sheet.png"))
            say("Saved strip_sheet.png (%dx%d px; far end left, point side right%s)"
                % (W, H, ", flipped" if args.flip else ""))

    json.dump(summary, open(os.path.join(args.out, "summary.json"), "w"), indent=2)
    with open(os.path.join(args.out, "summary.txt"), "w") as f:
        f.write("\n".join(lines) + "\n")
    print("Output written to", os.path.abspath(args.out))


if __name__ == "__main__":
    main()
