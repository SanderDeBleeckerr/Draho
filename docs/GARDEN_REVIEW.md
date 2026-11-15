# Garden review (game director pass)

One garden entity per round, with the blender-director brief (purpose, budget, style, pivot), the Blender
house style (`Tools/Blender/draho_style.py`: faceted low-poly, palette colours, one material) and a check in
Unity from the garden capture views (`garden_over`, `garden_mid`, `garden_back`, `garden`, `garden_end`,
`wild_garden`).

## Backlog (ordered by how much each one hurts the picture)

| # | Entity | Now | Plan |
|---|--------|-----|------|
| 1 | Saplings (young maple shoots) | cylinder + flat disc "leaves" (lollipops) | **Done, round 1** |
| 2 | Yard wall (whitewash skin) | repeating orange bow-tie brick spots | break the repeat, softer patches |
| 3 | Lawn | one flat saturated green | mown/long variation, worn path, edges |
| 4 | Grass tufts | sparse spiky tufts | denser at the edges, under the wall |
| 5 | Neighbour's workshop wall above the yard wall | flat grey corrugated sheet | check |
| 6 | Ivy on the wall | flat green panel | leafy edge, depth |
| 7 | Stepping-stone path | grey slabs | worn edges, moss, sunk in the lawn |
| 8 | Terrace tiles | plain grey grid | check |
| 9 | Fallen leaves | flat cubes | Blender leaf shapes |
| 10 | Blue tarp | three boxes | Blender crumpled tarp |
| 11 | Neighbour's fence (posts) | Blender-era boards | check |
| 12 | Green end fence | mesh panels | check |
| 13 | Maple tree | Blender | check |
| 14 | Picnic table, firewood store, wall lantern | Blender | check |
| 15 | Compost and wheelie bins | Blender (round 27) | check |
| 16 | Animals in the garden (blackbirds, Fatsocat, Zlatenka) | done in the graphics review | check in place |

## Round 1: the saplings

**Production brief**

| Field | Value |
|-------|-------|
| Type | Vegetation prop, background (six in the lawn) |
| Style | Cozy low-poly, palette (blue-hour art direction) |
| Budget | ≤ 900 tris each; 2 variants |
| Pivot | Foot of the stem, on the ground |
| Animation | None (static) |
| Export | FBX to `Art/Furniture`, Unity bakes the axes |

- **Before:** each sapling was a primitive cylinder with four flattened spheres stacked up it. From the garden
  they read as lollipop poles with yellow and green discs.
- **Now:** `sapling_green` and `sapling_turning` (886 tris each). Each has:
  - a thin stem that bends slightly as it rises, with three twigs off it;
  - clusters of eight five-lobed maple leaves (15-22 cm) at the twig ends and the top, plus single leaves along the
    upper stem;
  - leaves that are double-sided cards 1 mm apart, so they show from both sides with the one-sided palette material.
- **Colours:** greens from the palette (`leaf`, `leaf_dark`, `leaf_light`). In the turning variant about half the
  leaves are `mustard` or `terracotta`.
- **In Unity:** every third sapling is the turning one. Each is turned at random and scaled to its old height
  (0.8-1.5 m), and casts two-sided shadows, so the leaves throw dappled shade on the lawn.
- **Checked:** Blender review sheet; Unity `garden_mid`, `garden_back` and `garden_over` in Day and Evening.
