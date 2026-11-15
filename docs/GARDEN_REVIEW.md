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
| 6 | Ivy on the wall | flat green panel | **Done, round 6** |
| 7 | Stepping-stone path | grey slabs | **Done, round 7** |
| 8 | Terrace tiles | plain grey grid | **Done, round 8** |
| 9 | Fallen leaves | flat cubes | **Done, round 9** |
| 10 | Blue tarp | three boxes | **Done, round 10** |
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

## Round 6: the ivy

**Brief:** a climbable (the cat climbs it to the flat roof), 4 m wide and 6.5 m high at the end of the yard wall.
Procedural meshes in the builder, about 9,000 tris in three meshes (mass, stems, leaves): the leaves are one draw call
on the existing wind-sway material.

- **Before:** a 12 cm green box, 4 x 6.5 m, with about 1,800 small diamond leaves on its face. It read as a flat green
  rectangle on the wall.
- **Now:**
  - The box stays as the climbing collider, with its renderer removed.
  - `IvyMassMesh` is a lumpy dark-green mass in 10 cm cells inside an outline (`IvyHalfWidth`): 18 % of the width at
    the foot, spreading to the full width by about half way up, with ragged sides and a ragged top. It stands 3-15 cm
    off the wall (`IvyDepth`), thickest in the middle, so the wall never shows between the leaves.
  - Four woody stems rise from the ground, wandering up and forking, as on the foot of old ivy.
  - The leaves are about 1,300 three-lobed ivy leaves (stem point, two side lobes, a tip lobe, 8 triangles), set on
    the lumpy surface, biased towards the outline, and 15 % of them spill up to 18 cm past it, so the silhouette is
    ragged. They face out of the wall and tip up to catch the sun, with the sway weight on the tips.
- **Light:** the tipped-up leaves catch the sun in many small facets over a dark interior, which gives depth. The
  dark mass stops the white wall glowing through.
- **Checked:** the triangle winding by hand before building (lesson from round 5), and the stems are double-sided. Unity
  `garden_end`, `garden_mid` and `garden_over` in Day and Evening.

## Round 7: the stepping-stone path

**Brief:** a prop in three variants (Blender), 108 tris each, pivot at the middle of the base, no collider (the lawn
carries the cat).

- **Before:** about 30 identical 30 x 60 cm boxes, 4 cm apart. They read as one pale, ruler-straight concrete strip.
- **Now:** `stepping_stone_a`, `stepping_stone_b` and `stepping_stone_c`:
  - Each is a natural flagstone of about 40-50 x 33-38 cm, 4 cm thick, with an irregular, chipped outline (14 points
    round a rounded rectangle) and a bevelled top edge.
  - The top is a flat face inside a ring of edge faces. The first version fanned the top from a raised middle and
    showed pinwheel facets in the sun.
  - Each variant has its own grey (`grey_mid`, `stone`, `grey_dark`), with darker sides and moss on about 30 % of the
    top-edge faces. The first version used the light greys and glowed white in the sun.
- **Placement** (`BuildPaths`): one stone per stride of about 0.6 m, with lawn between them. Each is turned ±12°,
  nudged ±4 cm sideways and scaled 0.92-1.08, and set 6 mm proud of the turf, so the line reads as laid by hand,
  alongside round 3's trodden strip.
- **Checked:** Unity `garden_over`, `garden_mid` and `garden_back` (Day), plus a cat-height crop.

## Round 8: the terrace

**Brief:** a texture (no mesh): the 5 x 5 m terrace of 50 cm concrete tiles between the sliding door and the lawn.

- **Before:** the generic floor-tile texture (the same one as the town's pavements), an even, clean grey grid.
- **Now:** `TerraceTexture`, one 1024 px texture over the terrace (205 px a metre, clamped):
  - each tile is its own grey, with a fine grain;
  - dark grout with moss in places, and grass creeping into the joints near the lawn edge;
  - a darker, damp drip line under the eaves along the house wall;
  - the rusty ring of a plant pot that once stood in the corner by the yard wall, about 1.1 m from the house;
  - pale lichen dots.
  - Checked in the capture: the pot ring sits in that corner, so the u direction and the house side are right. v is
    flipped as on the lawn.
- **Light:** the grout lines and the moss give the terrace a fine grid of shade in low sun. It stays the calmest,
  lightest surface in the garden, which leads the eye from the door to the lawn.
- **Checked:** Unity `garden_over`, `garden`, `door_out` (Day) and `garden_over` (Evening).

## Round 9: the fallen leaves

**Brief:** litter props (Blender), three patch variants, palette, one material, single-sided (only seen from above),
no shadows.

- **Before:** 260 flat cube primitives, each its own GameObject and draw call. They read as coloured confetti
  rectangles.
- **Now:** patches of real maple leaves, the same five-lobed shape as the saplings' leaves (`_maple_leaf`, now
  optionally single-sided), in autumn palette colours: `mustard`, `terracotta`, `walnut`, `oak`, `oak_light`.
  - `leaf_litter_dense`: 40 leaves on a 0.9 m disc, 400 tris.
  - `leaf_litter_sparse`: 14 leaves, 140 tris.
  - `leaf_litter_strip`: 24 leaves over 2 x 0.35 m, 240 tris.
  - The leaves lie almost flat (tilted ±18°) and overlap a little.
- **Fix on the way:** the first export stood the leaves on edge: the leaf outline already lies flat, and the tilt
  was set about 90° too far. It was caught by checking the mean face normal (0 → 0.98).
- **Placement** (`BuildGardenDetails`):
  - three dense and two sparse patches in the maple corner (where the cube cluster was);
  - strips along the foot of the wall and the fence;
  - six sparse patches on the lawn.
  - That is about 25 objects instead of 260, and the cubes stay as the fallback.
- **Checked:** Unity `garden_end`, `garden_back` and `garden_over` (Day).

## Round 10: the blue tarp

**Brief:** a prop (Blender), 1.2 x 0.9 m, 276 tris, palette, no collider (the cat and the blackbirds walk over it).

- **Before:** three small boxes stacked at angles on the lawn.
- **Now:** `garden_tarp`:
  - a faceted 12 x 9 cloth grid crumpled in soft folds: a ridge along one diagonal and two gentle waves, settling to the
    ground;
  - one corner folded back over the tarp, an exact mirror across the fold line, showing its lighter `teal` underside
    against the `navy` top;
  - metal eyelets at three corners.
- **Fixes on the way:**
  1. The first fold used approximate mirror factors and tore the corner into a staircase. The exact reflection across
     the line (normal (1, 0.6), so p - 2f/1.36 x (1, 0.6)) folds it cleanly.
  2. A dandelion grew up through the tarp. `BuildGrassTufts` now keeps tufts and dandelions out of a 0.75 m circle
     under it.
- **Checked:** Blender review sheet; Unity `garden_over` (Day), crop.
