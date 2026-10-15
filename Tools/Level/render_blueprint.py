"""Draw a top-down blueprint from a level layout JSON.

Usage:
    pip install matplotlib
    python Tools/Level/render_blueprint.py \
        Draho/Assets/_Draho/Levels/Apartment/apartment_layout.json docs/images/apartment_blueprint.png

The JSON is the single source of truth: edit sizes there, then run this again.
"""
import json
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.patches as mp
import matplotlib.pyplot as plt

FACING_ARROW = {"north": (0, 1), "south": (0, -1), "east": (1, 0), "west": (-1, 0)}


def wall_segment(wall, a, b, room_x0, room_x1, room_z0, room_z1):
    """Return ((x0, z0), (x1, z1)) for an opening on a named outer wall."""
    if wall == "west":
        return (room_x0, a), (room_x0, b)
    if wall == "east":
        return (room_x1, a), (room_x1, b)
    if wall == "north":
        return (a, room_z1), (b, room_z1)
    return (a, room_z0), (b, room_z0)


def main(src, dst):
    lay = json.load(open(src))
    rooms = lay["rooms"]
    inside = [r for r in rooms if not r.get("outdoor")]
    x0 = min(r["x0"] for r in inside)
    x1 = max(r["x1"] for r in inside)
    z0 = min(r["z0"] for r in inside)
    z1 = max(r["z1"] for r in inside)
    t = lay["wall"]["thickness"]

    span = max(r["x1"] for r in rooms) - min(min(r["x0"] for r in rooms), *(b["x0"] for b in lay.get("buildings", [])))
    depth = max([r["z1"] for r in rooms] + [b["z1"] for b in lay.get("buildings", [])]) - min(min(r["z0"] for r in rooms), *(b["z0"] for b in lay.get("buildings", [])))
    fig, ax = plt.subplots(figsize=(max(14, span * 0.75), max(6.2, depth * 0.62)), dpi=120)
    ax.set_facecolor("#F7F5F0")

    # Rooms and floor tiles.
    for r in rooms:
        color = "#E3EBD8" if r.get("outdoor") else "#DDD9D2"
        ax.add_patch(mp.Rectangle((r["x0"], r["z0"]), r["x1"] - r["x0"], r["z1"] - r["z0"],
                                  facecolor=color, edgecolor="none", zorder=0))
        ax.text((r["x0"] + r["x1"]) / 2, r["z0"] - 0.45, r["label"].upper(), ha="center", va="center",
                fontsize=11, color="#6A665F", weight="bold", zorder=5)
    tile = lay["floor"]["tile_size"]
    x = x0
    while x <= x1 + 1e-6:
        ax.plot([x, x], [z0, z1], color="#CCC7BE", lw=0.5, zorder=1)
        x += tile
    z = z0
    while z <= z1 + 1e-6:
        ax.plot([x0, x1], [z, z], color="#CCC7BE", lw=0.5, zorder=1)
        z += tile
    # Zone divider (open plan: dashed, no wall).
    for r in inside[1:]:
        ax.plot([r["x0"], r["x0"]], [r["z0"], r["z1"]], ls="--", color="#8E8A82", lw=1, zorder=2)

    # Open-air courts inside the building: stones, glass walls on the listed sides.
    for c in lay.get("courts", []):
        cw, cd = c["x1"] - c["x0"], c["z1"] - c["z0"]
        ax.add_patch(mp.Rectangle((c["x0"], c["z0"]), cw, cd, facecolor=c["color"], edgecolor="none", zorder=3))
        n = 0
        zz = c["z0"] + 0.12
        while zz < c["z1"] - 0.05:
            xx = c["x0"] + 0.12 + (0.09 if n % 2 else 0)
            while xx < c["x1"] - 0.05:
                ax.add_patch(mp.Circle((xx, zz), 0.045, facecolor="#9C9488", edgecolor="none", zorder=4))
                xx += 0.18
            zz += 0.16
            n += 1
        sides = {"south": ((c["x0"], c["z0"]), (c["x1"], c["z0"])), "north": ((c["x0"], c["z1"]), (c["x1"], c["z1"])),
                 "west": ((c["x0"], c["z0"]), (c["x0"], c["z1"])), "east": ((c["x1"], c["z0"]), (c["x1"], c["z1"]))}
        for side, ((sx0, sz0), (sx1, sz1)) in sides.items():
            if side in c.get("outer_walls", []):
                continue  # the house's outer wall is already drawn
            glass = side in c.get("glass", [])
            ax.plot([sx0, sx1], [sz0, sz1], color="#4BA3C7" if glass else "#2E2C29", lw=3 if glass else 7,
                    zorder=8, solid_capstyle="butt")
        for door in c.get("doors", []):
            (sx0, sz0), (sx1, sz1) = sides[door["side"]]
            vertical = sx0 == sx1
            a, b = door["from"], door["to"]
            seg = ((sx0, a), (sx0, b)) if vertical else ((a, sz0), (b, sz0))
            gap = ((sx0, b - door["open_gap"]), (sx0, b)) if vertical else ((b - door["open_gap"], sz0), (b, sz0))
            ax.plot(*zip(*seg), color="#E08A1E", lw=4, zorder=8.5, solid_capstyle="butt")
            ax.plot(*zip(*gap), color="#F7F5F0", lw=6, zorder=8.6, solid_capstyle="butt")
            ax.text(sx0 - 0.1 if vertical else (a + b) / 2, (a + b) / 2 if vertical else sz0 - 0.12, door["label"],
                    rotation=90 if vertical else 0, ha="center", va="center", fontsize=7, color="#A85F0A", zorder=9)
        ax.text((c["x0"] + c["x1"]) / 2, (c["z0"] + c["z1"]) / 2, c["label"], ha="center", va="center",
                fontsize=8, zorder=9, color="#2E2C29",
                bbox=dict(boxstyle="round,pad=0.2", facecolor="#F7F5F0", edgecolor="none", alpha=0.85))

    # Outer walls.
    ax.add_patch(mp.Rectangle((x0 - t, z0 - t), x1 - x0 + 2 * t, z1 - z0 + 2 * t, fill=False,
                              edgecolor="#2E2C29", lw=7, zorder=6))

    # Openings: cut the wall and draw glass.
    for o in lay["openings"]:
        (ax0, az0), (ax1, az1) = wall_segment(o["wall"], o["from"], o["to"], x0, x1, z0, z1)
        ax.plot([ax0, ax1], [az0, az1], color="#F7F5F0", lw=9, zorder=7, solid_capstyle="butt")
        ax.plot([ax0, ax1], [az0, az1], color="#4BA3C7", lw=3, zorder=8, solid_capstyle="butt")
        tx, tz = (ax0 + ax1) / 2, (az0 + az1) / 2
        rot = 90 if o["wall"] in ("west", "east") else 0
        off = {"west": (-0.35, 0), "east": (0.35, 0), "north": (0, 0.3), "south": (0, -0.3)}[o["wall"]]
        ax.text(tx + off[0], tz + off[1], o["label"], rotation=rot, ha="center", va="center",
                fontsize=8, color="#2C6E8A", zorder=9)

    # Garden ground zones (terrace, lawn).
    for zn in lay.get("garden_zones", []):
        g = next(r for r in rooms if r.get("outdoor"))
        ax.add_patch(mp.Rectangle((zn["x0"], g["z0"]), zn["x1"] - zn["x0"], g["z1"] - g["z0"],
                                  facecolor=zn["color"], edgecolor="none", alpha=0.35, zorder=1))
        ax.text((zn["x0"] + zn["x1"]) / 2, g["z1"] - 0.3, zn["label"], ha="center", va="center",
                fontsize=8, color="#3F4A35", zorder=5)

    # Garden edges (posts drawn as dots on the fence line).
    for e in lay.get("garden_edges", []):
        (ex0, ez0), (ex1, ez1) = e["from"], e["to"]
        ax.plot([ex0, ex1], [ez0, ez1], color=e["color"], lw=5 if e["h"] < 3 else 9, zorder=6)
        if e.get("style") == "posts":
            n = max(1, round(abs(ex1 - ex0 + ez1 - ez0) / e.get("post_spacing", 2.0)))
            for i in range(n + 1):
                ax.plot(ex0 + (ex1 - ex0) * i / n, ez0 + (ez1 - ez0) * i / n, "s", color=e["post_color"],
                        markersize=5, zorder=7)
        ax.text((ex0 + ex1) / 2, (ez0 + ez1) / 2 + (0.25 if ez0 >= 2.5 else -0.25), e["label"], ha="center",
                va="center", fontsize=7, color="#5A4A3A", zorder=7)

    # River and walkable ground areas (quays, the begijnhof lawn).
    wt = lay.get("water")
    if wt:
        ax.add_patch(mp.Rectangle((wt["x0"], wt["z0"]), wt["x1"] - wt["x0"], wt["z1"] - wt["z0"], facecolor="#6C9DB0", alpha=0.7, zorder=1))
        ax.text((wt["x0"] + wt["x1"]) / 2, -60, "RIVER", rotation=90, ha="center", va="center", fontsize=12, color="white", weight="bold", zorder=5)
    for g in lay.get("grounds", []):
        ax.add_patch(mp.Rectangle((g["x0"], g["z0"]), g["x1"] - g["x0"], g["z1"] - g["z0"], facecolor=g["color"], alpha=0.6, zorder=1))

    # Neighbouring buildings with walkable flat roofs (height in metres).
    for b in lay.get("buildings", []):
        ax.add_patch(mp.Rectangle((b["x0"], b["z0"]), b["x1"] - b["x0"], b["z1"] - b["z0"], facecolor=b["roof_color"],
                                  edgecolor=b["color"], lw=2.5, alpha=0.35 + 0.05 * b["h"], zorder=2))
        ax.text((b["x0"] + b["x1"]) / 2, (b["z0"] + b["z1"]) / 2, "%s\nroof %.1f m" % (b["label"].split(" (")[0], b["h"]),
                ha="center", va="center", fontsize=8, color="white", weight="bold", zorder=5)
    for p in lay.get("roof_props", []):
        ax.add_patch(mp.Rectangle((p["x"] - p["w"] / 2, p["z"] - p["d"] / 2), p["w"], p["d"], facecolor=p["color"], zorder=6))
        ax.text(p["x"], p["z"] - p["d"] / 2 - 0.15, p["id"], ha="center", va="top", fontsize=7, color="white", zorder=6)
    for e in lay.get("bounds", []):
        (ex0, ez0), (ex1, ez1) = e["from"], e["to"]
        ax.plot([ex0, ex1], [ez0, ez1], color="#C0392B", lw=1, ls=":", zorder=6)

    # The neighbour's wild garden.
    w = lay.get("wild_garden")
    if w:
        ax.add_patch(mp.Rectangle((w["x0"], w["z0"]), w["x1"] - w["x0"], w["z1"] - w["z0"], facecolor=w["color"], alpha=0.45,
                                  edgecolor="none", zorder=1))
        ax.text((w["x0"] + w["x1"]) / 2, w["z1"] - 0.4, w["label"], ha="center", va="center", fontsize=8, color="#2F3A18", zorder=5)
        for b in w.get("bushes", []):
            ax.add_patch(mp.Circle((b["x"], b["z"]), b["r"], facecolor="#2B3D1E", alpha=0.7, zorder=4))
        for key in ("wheelbarrow", "chair", "barrel", "hedgehog"):
            if key in w:
                ax.plot(w[key]["x"], w[key]["z"], "o" if key != "hedgehog" else "*", color="#6B3A1E", markersize=7, zorder=8)
                ax.text(w[key]["x"], w[key]["z"] - 0.3, key, ha="center", va="top", fontsize=7, color="#3A2A1A", zorder=8)
    for e in lay.get("garden_edges", []):
        if e.get("gap_w"):
            ax.plot(e["gap_at"], e["from"][1], "s", color="#F5E663", markersize=8, zorder=9)
            ax.text(e["gap_at"], e["from"][1] - 0.3, "loose plank", ha="center", va="top", fontsize=7, zorder=9)

    # Garden paths (stone slabs).
    for pa in lay.get("garden_paths", []):
        (px0, pz0), (px1, pz1) = pa["from"], pa["to"]
        ax.plot([px0, px1], [pz0, pz1], color=pa["color"], lw=4, zorder=4, solid_capstyle="butt")
        ax.text((px0 + px1) / 2, pz0 + 0.25, pa["label"], ha="center", va="bottom", fontsize=7, color="#5A564E", zorder=5)

    # Climbable surfaces (ivy).
    for c in lay.get("climbables", []):
        if c["type"] == "pipe":
            ax.plot(c["x"], c["z"], "o", color=c["color"], markersize=6, zorder=10)
            ax.text(c["x"], c["z"] + 0.3, c["label"], ha="center", va="bottom", fontsize=7.5, color="white", zorder=13)
            continue
        if c["type"] == "tree":  # trunk and crown seen from above
            ax.add_patch(mp.Circle((c["x"], c["z"]), 1.6, facecolor=c["top_color"], alpha=0.3, edgecolor="none", zorder=9))
            ax.add_patch(mp.Circle((c["x"], c["z"]), c["radius"], facecolor=c["color"], edgecolor="#1F3A18", zorder=10))
            ax.text(c["x"], c["z"] - 0.45, c["label"], ha="center", va="top", fontsize=7.5, color="#1F3A18", zorder=13)
            continue
        w, d = c.get("w", 2 * c.get("radius", 0.2)), c.get("d", 2 * c.get("radius", 0.2))
        ax.add_patch(mp.Rectangle((c["x"] - w / 2, c["z"]), w, max(d, 0.15), facecolor=c["color"],
                                  edgecolor="#1F3A18", lw=1, zorder=10))
        ax.text(c["x"], c["z"] + 0.35, c["label"], ha="center", va="bottom", fontsize=7.5, color="#1F3A18", zorder=13)

    # Props.
    # Low props first, so a rug stays under its table.
    for p in sorted(lay["props"], key=lambda p: p["h"]):
        w, d = p["w"], p["d"]
        ax.add_patch(mp.Rectangle((p["x"] - w / 2, p["z"] - d / 2), w, d, facecolor=p["color"],
                                  edgecolor="#2E2C29", lw=1, alpha=0.9, zorder=10))
        if "top_color" in p:
            ax.add_patch(mp.Rectangle((p["x"] - w / 2 + 0.03, p["z"] - d / 2 + 0.03), w - 0.06, d - 0.06,
                                      facecolor=p["top_color"], edgecolor="none", alpha=0.35, zorder=11))
        fx, fz = FACING_ARROW[p["facing"]]
        if w > 0.3 and d > 0.3:
            ax.annotate("", xy=(p["x"] + fx * 0.3, p["z"] + fz * 0.3), xytext=(p["x"], p["z"]),
                        arrowprops=dict(arrowstyle="->", color="white", lw=1.2), zorder=12)
        dark = sum(int(p["color"][i:i + 2], 16) for i in (1, 3, 5)) < 380
        if d > 2 * w:  # tall narrow piece: vertical label inside
            ax.text(p["x"], p["z"], p["label"], rotation=90, ha="center", va="center", fontsize=7.5,
                    zorder=13, color="white" if dark else "#2E2C29")
        elif p["h"] < 0.05:  # floor covering: label on its inner bottom edge
            ax.text(p["x"], p["z"] - d / 2 + 0.08, p["label"], ha="center", va="bottom", fontsize=7.5,
                    zorder=13, color="white" if dark else "#2E2C29")
        elif d < 0.5:  # small piece: label below it
            ax.text(p["x"], p["z"] - d / 2 - 0.08, p["label"], ha="center", va="top", fontsize=7.5,
                    zorder=13, color="#2E2C29")
        else:
            ax.text(p["x"], p["z"] + 0.1, p["label"], ha="center", va="bottom", fontsize=7.5,
                    zorder=13, color="white" if dark else "#2E2C29")

    # Spawn.
    s = lay["spawn"]
    ax.plot(s["x"], s["z"], marker="*", markersize=16, color="#111111", zorder=14)
    ax.text(s["x"] + 0.18, s["z"], "Drahomil", va="center", fontsize=8, zorder=14)

    # Cat for scale (about 0.45 m long).
    ax.add_patch(mp.Rectangle((s["x"] - 0.06, s["z"] - 0.5), 0.12, 0.45, color="#111111", zorder=14))

    # Scale bar and north arrow.
    sx, sz = x1 - 2.2, z0 - 0.75
    ax.plot([sx, sx + 2], [sz, sz], color="#2E2C29", lw=3)
    for i in range(3):
        ax.text(sx + i, sz - 0.12, f"{i} m", ha="center", va="top", fontsize=8)
    ax.annotate("N", xy=(x1 + 0.6, z1), xytext=(x1 + 0.6, z1 - 0.8), ha="center",
                arrowprops=dict(arrowstyle="->", lw=1.5), fontsize=10, weight="bold")

    ax.set_title(f"{lay['name']} — blueprint v{lay['version']} (rough sizes)", loc="left",
                 fontsize=12, color="#2E2C29")
    ax.set_aspect("equal")
    gx0 = min(r["x0"] for r in rooms)
    bx0 = min([gx0] + [b["x0"] for b in lay.get("buildings", [])] + [g["x0"] for g in lay.get("grounds", [])])
    bz0 = min([z0] + [b["z0"] for b in lay.get("buildings", [])])
    ax.set_xlim(bx0 - 0.5, x1 + 1.1)
    bz1 = max([z1] + [b["z1"] for b in lay.get("buildings", [])])
    ax.set_ylim(bz0 - 1.2, bz1 + 0.6)
    ax.axis("off")
    fig.tight_layout()
    fig.savefig(dst, facecolor=fig.get_facecolor())
    print("wrote", dst)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
