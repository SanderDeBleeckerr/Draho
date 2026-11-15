# Garden review (game director pass)

One garden entity per round, with the blender-director brief (purpose, budget, style, pivot), the Blender
house style (`Tools/Blender/draho_style.py`: faceted low-poly, palette colours, one material) and a check in
Unity from the garden capture views (`garden_over`, `garden_mid`, `garden_back`, `garden`, `garden_end`,
`wild_garden`).

## Backlog (ordered by how much each one hurts the picture)

| # | Entity | Now | Plan |
|---|--------|-----|------|
| 1 | Saplings (young maple shoots) | cylinder + flat disc "leaves" (lollipops) | **Done, round 1** |
| 2 | Yard wall (whitewash skin) | repeating orange bow-tie brick spots | **Done, round 2** |
| 3 | Lawn | one flat saturated green | **Done, round 3** |
| 4 | Grass tufts | sparse spiky tufts | **Done, round 4** |
| 5 | Neighbour's workshop wall above the yard wall | flat grey corrugated sheet | **Done, round 5** |
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

## Round 2: the yard wall's whitewash

**Brief:** a texture fix (no mesh), the biggest surface in the garden: 21 x 2.6 m along the whole south side.

- **Before:** a 256 px whitewash tile of 1 m, repeated 21 times. Its fine "peel" noise left the same small orange
  brick spots every metre, a bow-tie pattern that marched down the wall.
- **Now:** `WhitewashWallTexture` (2048 px, 8 x 8 m at 256 px a metre; the wall uses the bottom 2.6 m):
  - Peeling in patches, as whitewash weathers: ragged three-octave noise, much more of it in the bottom metre (rising
    damp), and a few small flakes higher up. A thin rim of flaking paint around each patch.
  - The brick under the peel is in its own shades, each brick a different red-brown, darker in the damp.
  - Grey rain streaks run down from the coping, stronger towards the top.
  - There is green algae at the foot, and the mortar courses show faintly through the paint.
  - One tile covers 8 m, so the 21 m wall shows it 2.6 times, with no visible rhythm.
- **Fix found on the way:** `Mat()` adds a normal map by texture name. The material asset still held the 1 m brick
  normal from the old "Whitewash" texture, and at the new tiling it drew giant blurred bricks. The wall material
  now clears its normal map explicitly: the joints are painted, and a 2048 normal map would cost memory on the
  phone.
- **Light:** low sun now rakes across an even, matte white with a weathered foot, instead of a speckled
  surface. The evening light turns it a warm grey.
- **Checked:** Unity `garden_mid`, `garden_back` and `garden_over` in Day and Evening.

## Round 3: the lawn

**Brief:** a texture (no mesh): the lawn is one 19 x 5 m box, the floor of every garden shot.

- **Before:** a 256 px grass tile repeated every 1.5 m. It had fine blades but nothing at the metre scale, so the
  lawn read as one flat green from wall to fence.
- **Now:** `LawnTexture`, one 2048 x 539 texture (about 108 px a metre) over the whole lawn. `MakeTextureWH` handles
  non-square sizes, and the texture is clamped, not tiled. It tells the lawn's story:
  - longer, darker, bluer grass in the margins the mower misses: about 0.5 m along the wall and 0.45 m along the
    fence, with wavering edges;
  - bright moss at the damp foot of the wall;
  - a trodden line just on the lawn side of the stepping stones, where feet step off: yellowed grass with bare-earth
    specks;
  - sun-dried yellow patches in the open middle, and the fine blades and clumps on top.
- **Two fixes on the way:**
  1. The lawn's dark green tint (#4A6332) multiplies the texture, so the first bands were far too faint. Their
     colours are now pushed much harder.
  2. On the box's top face v runs from the fence to the wall: the first worn strip came out beside the fence, and the
     texture now flips v. Checked in the PNG: the strip is at 1.11 m from the wall, where the path is.
- **Light:** the trodden line and the dry patches catch the low sun warmly, and the long margins stay dark and cool in
  the shade of the wall and the fence, so the lawn has depth instead of one flat value.
- **Checked:** Unity `garden_over`, `garden_mid` and `garden_back` in Day and Evening, plus a cat-height crop.

## Round 4: the grass tufts (and dandelions)

**Brief:** small vegetation props, many instances, about 150 tris each, no shadows (too small to matter), one
palette material.

- **Before:** one six-blade tuft (8-16 cm, 24 tris) in a line along the wall and fence and about 1.2 per m² over
  the lawn. Every clump was the same short spike, and nothing followed the long margins painted into the lawn in
  round 3.
- **New props:**
  - `grass_tuft_tall` (168 tris): ten arching, double-sided blades of 24-38 cm in three greens and a straw tone,
    fanned out from a 7 cm base, with two seed stalks carrying buff seed heads.
  - `dandelion` (128 tris): a flat rosette of six leaves, a stem with a yellow flower head, and a stem with a white
    seed clock.
  - A `_blade` helper: a tapering, arching strip made double-sided, for grass and leaf shapes.
- **Placement** (`BuildGrassTufts`):
  - The margins along the wall and the fence get two rows of tall tufts (every 0.22-0.4 m, then a looser row behind),
    mixed with some short ones.
  - Short tufts thin out to about 0.8 per m² and mostly skip the trodden line beside the stones.
  - Twenty-two dandelions dot the sunny lawn.
- **Light:** the seed heads and the straw blades catch the sun along the wall, and the long margins read as long
  grass against the lawn's darker bands.
- **Checked:** Unity `garden_over`, `garden_mid` and `garden_back` (Day), plus a cat-height crop.

## Round 5: the corrugated cladding above the yard wall

**Brief:** the neighbour's workshop cladding, 13 x 3.9 m and 8 x 3.2 m: the largest surface over the garden, seen from
everywhere. A procedural mesh (an exact sine profile, so code is the right tool, not Blender), about 1,200 tris for the
two areas, plus a texture.

- **Before:** two flat boxes with the ridges painted into a 1 m tile, so the light could not rake across them.
- **Now:**
  - `CorrugatedSheets`: real corrugations, 177 mm pitch and 25 mm deep, sampled 8 times a wave, with smooth normals
    from the slope, facing the garden. Low sun now gives each ridge a lit side and a shadow side.
  - `CorrugatedSheetsTexture` (1024 px, 8 x 8 m): no painted ridges. Each 1 m sheet has its own grey, with a darker
    line where two sheets overlap and where one row laps the next. There are fixing bolts along the purlins, grey dirt
    streaks running down from the top, and lichen in patches. The second area starts at another texture offset, so
    the two walls do not match.
- **Fixes on the way:**
  1. The first mesh did not show: seen from the garden (looking towards -z), +x runs to the left on screen, so the
     triangles' winding was reversed.
  2. Generalised round 2's fix: `Mat()` now clears a stale normal map on any material whose texture has no relief
     map of its own, so no material can keep an old texture's bumps.
- **Checked:** Unity `garden_over`, `garden_mid` and `garden_back` in Day and Evening.
