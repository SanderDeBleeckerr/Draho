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

    fig, ax = plt.subplots(figsize=(14, 6.2), dpi=120)
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

    # Garden edges.
    for e in lay.get("garden_edges", []):
        (ex0, ez0), (ex1, ez1) = e["from"], e["to"]
        ax.plot([ex0, ex1], [ez0, ez1], color=e["color"], lw=5, zorder=6)

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

    ax.set_title(f"{lay['name']} — blueprint v{lay['version']} (all sizes are guesses)", loc="left",
                 fontsize=12, color="#2E2C29")
    ax.set_aspect("equal")
    gx0 = min(r["x0"] for r in rooms)
    ax.set_xlim(gx0 - 0.5, x1 + 1.1)
    ax.set_ylim(z0 - 1.2, z1 + 0.6)
    ax.axis("off")
    fig.tight_layout()
    fig.savefig(dst, facecolor=fig.get_facecolor())
    print("wrote", dst)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
