# Graphics review, one entity per round

A recurring review (owner, November 2026): each round takes one entity of the game, checks its shape, textures and
light against [ART_STYLE.md](ART_STYLE.md) with the Blender skills (`blender-director` brief, `prop-artist`,
`materials`, `lighting`, `collision-proxy`, `unity-export`, `qa-review`), improves it, and checks it in Unity
(`ArtCapture`). After round 30, the day/night cycle (`SkyMood`, `DayCycle`) gets its own review.

Furniture is built by [`Tools/Blender/build_apartment_props.py`](../Tools/Blender/build_apartment_props.py) and loaded
from `Art/Furniture/<layout id>.fbx`; see [APARTMENT_GRAPHICS.md](APARTMENT_GRAPHICS.md) for the first rounds before
this log (sofa, coffee table, cat tree, radiator, poster, rug, dining bar, bedroom window, court stones, firewood).

## Backlog (most visible first)

| # | Entity | Now | Plan |
|---|---|---|---|
| 1 | Kitchen island | Kenney cabinets + code worktop | **Done, round 1** |
| 2 | Back counter (sink, coffee machine) | Kenney cabinets, flat top | **Done, round 2** |
| 3 | Fridge column | Kenney fridge, stretched | **Done, round 3** |
| 4 | Plant stand by the sliding door | Kenney potted plant | **Done, round 4** |
| 5 | Sliding door | Sketchfab, white | **Done, round 5** (transparent-frame glitch) |
| 6 | Front door | code boxes | **Done, round 6** |
| 7 | Ceiling and lamps | flat box, no fittings | **Done, round 7** |
| 8 | Walls, ceiling and floor | flat colour, tile texture | **Done, round 8** |
| 9 | Court light well (shaft walls, windows, gutter) | code boxes | **Done, round 9** |
| 10 | Drainpipes | code cylinder | **Done, round 10** |
| 11 | Garden picnic table | code boxes | **Done, round 11** |
| 12 | Garden lamp | code primitives | **Done, round 12** |
| 13 | Garden fences (neighbour, end, wild) | code boxes | **Done, round 13** |
| 14 | Climbable trees (maple, bank alder) | code blobs | **Done, round 14** |
| 15 | Lawn, path, terrace | flat colour, tiles | **Done, round 15** (grass tufts) |
| 16 | Ivy | leaf mesh | **Done, round 16** (and the chapter 9 route) |
| 17 | Wild garden (bushes, wheelbarrow, chair, barrel) | code primitives | **Done, round 17** |
| 18 | Humans (legs at the door) | **Done, round 18** | Blender: legs, shoes, coat hem |
| 19 | Mouse | **Done, round 19** | fur/colour check at cat height |
| 20 | Pigeons | **Done, round 20** | sheen, perched pose |
| 21 | Blackbirds | **Done, round 21** | check |
| 22 | Hrozna (corgi) | **Done, round 22** | check |
| 23 | Zlatenka (tortoise), hedgehog, fox, ducks, gull | **Done, round 23** | check |
| 24 | Fatsocat, the grey cat (NPC cats) | **Done, round 24** | colour per cat, fur settings |
| 25 | Drahomil | **Done, round 25** | fur at night, eye shine |
| 26 | Town buildings near the flat | **Done, round 26** | window frames, roof edges |
| 27 | Courtyard / street props (bins, cars, lamps) | code | Blender props |
| 28 | Quay, bridges, boats, water | code | water colour and glint, quay stones |
| 29 | Bakery, begijnhof | code | facades |
| 30 | Sky, clouds, fog | shaders | blue-hour colour check |
| 31+ | Day/night cycle | `SkyMood` presets | review, then decide |

## Rounds

### Round 1: kitchen island

- **Review:** a Kenney drawer cabinet and stove stretched to 2.0 × 0.95 m, a code box for the worktop and the hob. The
  owner described it as white handle-less drawers with a dark-grey top; the Kenney cabinets have handles and a
  different drawer layout, and the stretch made the handles wide.
- **Change:** Blender model `island` (2,464 triangles, palette material): 3 × 3 white drawer fronts with 3 mm shadow
  gaps (the gaps carry the shape; no handles), end and back panels, a recessed dark plinth, a 3 cm dark-grey worktop
  with a small overhang, a black induction hob with ring marks. Collider shaped from the mesh.
- **Light:** the worktop is the darkest surface in the kitchen; the sink spots and the court light reach it. No
  change needed.
- **Checked:** Blender look-dev render; Unity `kitchen` view (Day).

### Round 2: back counter

- **Review:** Kenney cabinet, sink and coffee-machine modules stretched to 2.1 m; the sink was a flat panel in a
  flat top and the coffee machine a stretched block. It sits under the sink spots (IndoorLight), so it is the most
  lit thing in the kitchen at night.
- **Change:** Blender model `back_counter` (2,468 triangles): four columns of white handle-less fronts matching
  the island (two doors under the sink), a recessed plinth, the dark worktop with a real stainless basin cut in
  (boolean), a drain, a mixer tap with an arched spout and a lever, a coffee machine with a drip tray, a water tank
  and a white cup at the left end.
- **Light:** the sink spots land on the basin and the tap; fine by day and at night. Noted for the day/night
  review: at Night the dining corner next to it gets almost no light (the living-room lamp is far).
- **Checked:** Blender look-dev render; Unity `dining` view (Night) with the counter in the spot light.

### Round 3: fridge column

- **Review:** the Kenney large fridge stretched to 0.6 × 0.6 × 2.1 m: a freestanding appliance with handles next to
  handle-less built-in units.
- **Change:** Blender model `fridge` (672 triangles): an integrated column matching the island and the back
  counter: a tall fridge door and a cupboard door above it, 3 mm shadow gaps, a dark grip channel along the opening
  edge of each door instead of handles, white side panels, a recessed plinth.
- **Light:** a large white surface at the east end of the south wall; the sink spots light its side. No change.
- **Checked:** Blender look-dev render; the Unity scene builds with it (no capture view shows that corner yet).

### Round 4: plant by the sliding door

- **Review:** the Kenney potted plant stretched to 0.4 × 0.4 × 0.7 m: a smooth olive pot and a few spikes, recoloured
  by name. It stands in the daylight patch by the sliding door, so it is seen in every living-room shot.
- **Change:** Blender model `plant_stand` (192 triangles, faceted: organic shapes are flat-shaded): a terracotta pot
  with a rim and soil, a snake plant of 14 broad, flat, sharp blades leaning out on a golden-angle spread, in two
  greens with a few lighter blades so the clump reads against the wall.
- **Light:** the window light catches the faceted blades (each face its own tone); the pot sits in the floor patch.
- **Checked:** Blender look-dev render; Unity `sliding_door` view (Day).

### Round 5: sliding door

- **Review:** the owner saw the frame "lose its solidity" from inside once the door had slid open. Materials were
  right (frame opaque, panes glass, checked per sub-mesh in Unity). The cause is the Sketchfab mesh: in Blender the
  frame has 263 open edges and the sliding sash 132, so whole sides of the profiles were never modelled. Drawn
  one-sided, those sides were culled, and from some angles inside you looked through the frame.
- **Change:** the door frame material renders both sides (`_Cull` off). No new geometry needed.
- **Light:** the frame casts the window light's shadow onto the floor; the panes cast none (round 4 of the lighting).
- **Checked:** Unity with the door forced open (`ArtCapture -captureOpenDoors`), `living_low` and `sliding_door`
  views; the frame material is saved with `_Cull: 0`.

### Round 6: front door

- **Review:** since the earlier fix the door sits in a real opening with linings and an architrave, but the leaf was a
  flat brown 4 cm box with a block for a handle.
- **Change:** Blender model `front_door_leaf` (1,552 triangles): a walnut leaf with two moulded panels on the room
  side, a lever handle on a round rose, a lock cylinder and a peephole at eye height. The old box stays as the
  invisible collider (the cat and the mouse cannot pass; probe hits it at x 7.72).
- **Light:** in the kitchen's shade the mouldings and the metal handle are what read; the court light reaches it in
  the evening.
- **Checked:** Blender look-dev render; Unity `kitchen` view (Evening); collider probe.

### Round 7: ceiling lamps

- **Review:** the living-room and kitchen lamps were point lights with nothing to see: light from nowhere. At Night
  the room stayed one blue tone (the lamps were weak: 1.3 and 1.6).
- **Change:** Blender fittings at real height (Art/Furniture/lamp_[layout id].fbx, about 530 triangles each): over
  the island a dark metal dome pendant with a pale inside, in the living room a linen drum shade with a diffuser,
  both on a cord from a ceiling rose. A small bulb in each with an emissive material that IndoorLight switches with
  the lamps (dark by day, warm from the evening). Lamps 1.3 -> 2.2 (living) and 1.6 -> 2.4 (kitchen).
- **Light:** at Night the lamp now warms the ceiling, the back wall and the rug while the window side stays moonlit
  blue: the cool/warm split of ART_STYLE §3.
- **Checked:** Blender export (heights 2.18-2.6 m); Unity `living` and `living_low` views (Night).

### Round 8: walls, ceiling and floor

- **Review:** the flat had no ceiling: you saw the underside of the upper-floor buildings, with their outside plaster
  texture (and its normal map) stretched across the room, blotchy under the night lamp. The walls were one flat
  colour from floor to ceiling. The floor tiles and grout read well; no change.
- **Change:** a flat off-white ceiling over the living room and the kitchen (not over the open court). The
  full-height wall pieces get a soft vertical gradient (about 8 % darker at the floor, a 64 px tint texture); the
  short pieces above the openings stay flat so no dark band runs over the windows.
- **Light:** the ceiling now catches the lamp's warm pool at night and the window's bounce by day; the gradient adds
  the floor-level shade that baked light would give, at no cost.
- **Checked:** Unity `living` (Day) and `living_low` (Night) views.

### Round 9: the court's light well

- **Review:** the upper-floor windows round the light well were flat dark boards (1 cm) above the stone sills: no
  frame, no glazing bars, nothing for the light to catch. The cat sees them looking up from the court and from the
  drainpipe (chapters 8 and 10); the pigeons sit on their sills.
- **Change:** Blender windows `shaft_window_wide` (1.3 m, north wall) and `shaft_window_narrow` (0.9 m, west wall),
  792 triangles each: a light frame with a mullion and a transom, dark blue-grey panes set back 3 cm, a handle. The
  stone sills (pigeon perches) stay. New `ArtCapture` view `light_well` (looking up from the court).
- **Light:** the frames catch the sky light and the sun high in the well; the panes stay dark against the plaster.
- **Checked:** Blender look-dev render; Unity `light_well` view (Day), pigeons on the sills.

### Round 10: drainpipes

- **Review:** every climbable drainpipe (the court pipe the owner kept, the pipes up the high walls) was one stretched
  cylinder with flat square plates for brackets.
- **Change:** three Blender parts assembled along each pipe by ApartmentBuilder (scaled to the pipe's radius):
  `pipe_segment` (1 m with a socket collar, 184 triangles), `pipe_bracket` (clip ring, arm and fixing plate with
  screws, towards the wall), `pipe_shoe` (an outlet bending away from the wall over a small grate, where the pipe
  reaches the ground). The primitive cylinder stays as the climbing collider (no gameplay change).
- **Light:** the collars and the dark brackets break the pipe into readable lengths; the smooth zinc catches the
  well's sky light along one side.
- **Checked:** Blender export (bounds); Unity `court_window` view (Day).

### Round 11: garden picnic table

- **Review:** code boxes: plank strips, two bench slabs and tilted sticks for legs that did not meet the frame.
- **Change:** Blender model `picnic_table` (2,024 triangles, weathered grey woods from the palette): five top planks
  with 1 cm gaps, two planks per bench, crossed A-frame legs at each end with a bar under the benches and one under
  the top, a centre brace, bolt heads. Collider shaped from the mesh (bench -> table jumps, free underneath). The
  layout's `facing` is now "south" (the code builder ignored it; this keeps the table along the garden).
- **Light:** full sun on the terrace by day: the plank gaps and the leg shadows give it texture.
- **Checked:** Blender look-dev render; Unity `garden` view (Day).

### Round 12: garden wall lantern

- **Review:** chapter 9's lantern by the sliding door was five cubes: a plate, a stick, a flat roof, a glowing block and
  a base. The moths circle it at night; the cat jumps from the table towards it.
- **Change:** Blender model `garden_lantern` (1,038 triangles, black iron): a wall plate, an arm with a diagonal
  brace and a hook, four corner posts and glazing bars round the glass, a base plate, a pyramid roof with a finial.
  The glowing glass stays a separate Unity part (slightly taller to fill the frame) so `NightLamp` keeps switching
  its glow; the old cubes stay only as the fallback.
- **Light:** at night the glass glows inside the dark frame; the frame's posts break the glow into panes.
- **Checked:** Blender look-dev render; the Unity scene builds with it (no capture view looks at the outside wall
  beside the door yet).

### Round 13: garden fences

- **Review:** the neighbour's fence (1.8 m, concrete posts, horizontal planks) and the wild garden's old fence were
  identical planks in two alternating tones down to the grass, on bare square posts: stripes, and nothing at the
  foot. The green mesh fence at the garden end (round "garden end") is fine.
- **Change (code, `PostFence`):** a concrete gravel board along the bottom (25 cm, the way these fences stand),
  split round the neighbour fence's cat hole so the hole stays open (now 0-0.45 m); planks start above it in three
  tones; a slightly lighter cap on every post. The fallen plank by the hole uses the new tones.
- **Light:** the darker gravel board grounds the fence; the caps catch the sun along the top.
- **Checked:** Unity `garden` view (Day).

### Round 14: climbable trees

- **Review:** the garden maple and the river-bank alder were a cylinder, four stick branches and 14 smooth spheres:
  balloons, against the house rule (organic shapes are faceted) and the faceted animals.
- **Change:** Blender trees `tree_maple` and `tree_bank_alder` (1,408 triangles each, faceted): a tapering bark trunk
  with a root flare, five branches out of the fork, a crown of 16 irregular low-poly leaf clumps in three greens
  (lighter on top; the alder darker). The cylinder stays as the invisible climbing collider (`climb_top` unchanged).
- **Light:** each facet takes its own tone of the sun, so the crown reads as volume; the lighter top clumps catch the
  sky.
- **Checked:** Blender look-dev render; Unity `garden` view (Day).

### Round 15: lawn, path and terrace

- **Review:** the lawn already has a soft grass texture (blades and patches) and the stone path two slab tones with a
  little rotation each; the terrace tiles read well. What was missing is height: the lawn was a flat sheet that met
  the wall and the fence in one hard line.
- **Change:** a faceted grass tuft (`grass_tuft`, six blades, 24 triangles) scattered by `BuildGrassTufts`: dense
  rows along the lawn's long edges (the strip the mower misses) and about one per m² over the lawn, never on the
  stone path; random size and turn, one shared palette material, no colliders, no shadows, seeded (same every build).
- **Light:** the blades catch the low sun at their tips; at the base of the wall they soften the line.
- **Checked:** Unity `garden` view (Day).

### Round 16: ivy, and a broken route home

- **Review:** the garden ivy and the begijnhof ivy are one leaf mesh each (many small pointed leaves, denser at the
  top, ragged at the bottom, a four-colour strip): they read well and move with the wind; no change. But the third
  ivy, `shed_ivy`, grew up the back of the outbuilding at the garden end, which was removed when the owner asked for
  the green fence with open space behind it: it stood 4.2 m tall in mid-air, and chapter 9's way home ("climb the ivy
  on the shed onto its roof") could no longer be done.
- **Change:** `shed_ivy` removed. Chapter 9 now ends the night walk over the low green fence: from the river bank,
  across the neighbours' grass, onto the fence top (1.0 m, within the cat's 1.05 m jump), then down into the garden.
  The beat's target and check follow the fence (cat on top: y > 0.85, within 0.45 m of x = -24).
- **Checked:** the Unity scene builds; layout JSON valid.

### Round 17: the neighbour's wild garden

- **Review:** smooth spheres for the brambles, boxes for the upturned wheelbarrow (the hedgehog's nest), three slabs
  for the fallen chair and a stretched cylinder for the barrel.
- **Change:** Blender models, all palette: `wild_bramble` (660 triangles, faceted clumps with black and red
  berries on their surface, scaled per bush), `wild_wheelbarrow` (1,892: tapered rust-red tray turned over, rolled
  rim, wooden handles with grips, tyre and hub), `wild_chair` (760: a white monobloc chair modelled standing and
  tipped onto its back), `wild_barrel` (856: rusty drum with rolling hoops, rim, bung, a rust patch). The parts that
  carry colliders stay (renderers removed); the nest's leaves and hoglets stay. New capture view `wild_garden`.
- **Light:** the faceted brambles take the sun facet by facet; the barrow's red is the one warm colour in the green.
- **Checked:** Blender look-dev render; Unity `wild_garden` view (Day).

## Round 18: the humans at the front door

- **Shape:** the code cylinders are replaced by two Blender figures cut off at the coat hem (only the legs show
  when the door opens at cat height). `human_a` (navy trousers, oak coat, black shoes) and `human_b` (deep grey
  trousers, dark-green coat, walnut shoes, a shopping bag with bread set down beside the feet). Both are about 1.4 m
  tall and use faceted organic legs, bevelled shoes with soles and a flared hem, at roughly 900-1000 tris each.
- **Texture:** DrahoPalette cells only; the coats keep the two coat colours of the old code materials.
- **Light:** they stand in the stairwell in the door's light; the bag's pale oak catches the light from the hall.
- **Checked:** Blender look-dev; the Unity rebuild (`FurnitureModel` per person, primitive fallback kept). They sit
  outside the door, so the kitchen capture shows the closed door only.

## Round 19: the mouse

- **Shape:** the body loft goes from 8 to 10 sides, with two more rings. The rump now rounds off into the tail
  instead of ending in a flat cut, and the head has fuller cheeks. The eyes were floating 4 mm off the cheek and are
  now half sunk into it. The ear bases sit in the skull, and the ears are cupped (the inner face dips back) and lean
  back 14°. The hind feet are long and flat on the floor, as a mouse's are; the front feet are unchanged.
  436 tris (was 364).
- **Texture:** the body was `grey_stone`, which disappeared against grey floors and stone. Two animals-only palette
  cells were added at the end of the palette: `a_mouse_fur` #7D6E62 (house-mouse grey-brown) and `a_mouse_belly`
  #BDB1A2 (buff). The palette PNG and JSON are regenerated, and the existing cells are unchanged.
- **Light:** the warmer brown separates from the cool kitchen tiles and the court stones in shade. The pink ears and
  feet now stand out against the body.
- **Checked:** Blender QA (no floating parts, no z-fighting; palette UVs are zero-area by design); Unity animal
  studio (front, side, top) next to Drahomil.

## Round 20: the pigeons (and the shared bird body)

- **Shape:** the wings ran straight back from the shoulder and stood off the body in a V, with floor showing
  between wing and flank from above. They now fold like a resting bird's: each wing follows the flank, tapers in over
  the back and rises so the primaries lie over the sides of the tail. The head is smaller (radius 37 to 33 mm, a
  pigeon's head is small for its body), and the eyes and eye rings moved in onto the smaller head. The pivots are
  unchanged, so the flap animation still swings the wings from the shoulder.
- **Shared:** `bird()` also builds the blackbird and the gull, and both get the folded wings and the smaller head
  (checked: the gull's croissant still sits across the beak). Pigeon 448 tris, blackbird 488, gull 512.
- **Texture/light:** the colours are unchanged (blue-grey `metal`, `charcoal` wing bars, a `teal_dark` neck, red
  feet). With the wings folded, the wing bars now sit on the flank where the sun catches them, instead of on an edge.
- **Checked:** Blender QA (no floating parts, no z-fighting, no inside-out parts); Unity animal studio: pigeon front,
  side and flap, blackbird side.

## Round 21: the blackbirds

- **Shape:** a blackbird has a long tail (about 40% of its length) and often holds it raised, while ours had the
  pigeon's tail scaled down. `bird()` takes `tail_len` and `tail_lift`, and the blackbirds use 1.35 and 0.12
  (length 0.30 to 0.33 m).
- **Texture:** all three birds were black males, and charcoal is also Drahomil's colour, so the lawn read as three
  dark blobs. Every second blackbird is now a hen (`Blackbird_Hen`, 448 tris): `walnut_dark` body, a paler
  `walnut` breast, `soil` wings, a dull `oak` bill and no eye ring. The male keeps a step of separation, with
  `metal_dark` wings and a `cat_fur_light` belly against the charcoal body, and the orange bill and eye ring.
- **Light:** the hen's browns take the low sun warmly on the grass, while the male stays a glossy dark accent.
- **Code:** `BuildBirds` uses the hen for every second bird and falls back to the male model. ArtCapture now
  captures a male and the hen.
- **Checked:** Blender QA (no floating parts, no z-fighting); Unity animal studio: `blackbird_3` (male) and
  `blackbird_2` (hen), front, side, top and flap.

## Round 22: Hrozna (the corgi)

- **Shape:** the legs were white pillars with flat tops under a boxy body, so they read as stilts stuck on.
  - Each leg now starts inside the body and tapers through a middle ring.
  - A tan shoulder (front) and haunch (back) bulge over the top of each leg, so the legs grow out of the body.
  - The body loft goes from 8 to 10 sides.
  - The tail hung straight back like the fox's brush. It is now a fluffy corgi tail carried up in an arc (tip 16 cm
    above the root), with 8 sides instead of 6.
  - 704 tris.
- **Texture:** the legs are tan above and white below (socks), which matches a red Pembroke. The tan/white
  split on the face and chest is unchanged.
- **Light:** the haunch and shoulder bulges take the sun on their tops, so the body is no longer one flat side.
- **Checked:** Blender QA (no floating parts, no z-fighting); Unity animal studio front, side and top. The run also
  confirmed round 21's capture change: it takes a male blackbird and the hen.

## Round 23: Zlatenka, the hedgehog, the fox, the ducks, the gull

- **Ducks (changed):** they read as wooden decoys. The head sat straight on the back with no neck, and the wings
  stood on the flanks like the rim of a boat.
  - A neck now rises from the breast (0.095 to 0.192 m) to a smaller head (radius 52 to 44 mm). The Head pivot
    moved up with it.
  - The wings are flat, folded plates lying over the back, with the tips meeting over the tail.
  - The hen's bill was the drake's yellow and is now `terracotta`, the dull orange-brown of a female mallard.
  - On the drake, the longer neck shows the white collar between the chestnut breast and the green head.
  - Each duck is 364 tris.
- **Hedgehog, fox (checked, unchanged):** the hedgehog's spines, pale face and dark snout read well against the
  ground. The fox's rust coat, black stockings and white tail tip are the classic marks, and the legs stand on the
  ground.
- **Zlatenka (checked, unchanged):** the leopard-tortoise shell reads as cream with dark blotches. A later pass could
  add scute rings (raised plates) to give it relief in low sun. Noted, not done.
- **Gull:** got the folded wings and the smaller head in round 20.
- **Checked:** Blender QA (ducks: no floating parts, no z-fighting); Unity animal studio: drake `Duck_2` and hen
  `Duck_8`.

## Round 24: Fatsocat and the grey cat (the NPC cats)

- **Capture fix:** the review captures showed both cats standing upright like people. Their shadows showed four
  legs, so this was the bind pose of the skinned mesh in edit mode: only Drahomil had an Idle frame sampled.
  `ArtCapture.PoseCat` now samples Idle on every animator with an Idle clip. This affected the captures only. In
  play mode the animator poses the cats.
- **Shape:** Fatsocat ("the big white rival") was only Drahomil at 1.45x, a big cat but not a fat one.
  `NpcCatModel` takes a `girth`, a width-only scale on a `Girth` node between the root and the rigged model, so the
  animator, which drives the bones, does not undo it. Fatsocat is 1.4x with girth 1.3: 0.41 m wide (was 0.32) and
  1.11 m long. The grey cat is unchanged (1.05x).
- **Texture/light:** the fur colours are unchanged: off-white (0.93, 0.93, 0.90) and blue-grey (0.46, 0.47, 0.50) in
  the shared fur-shell shader. The off-white is kept a little below pure white, so he keeps his form in the noon sun
  instead of blowing out; the captures show the shading across his flank.
- **Checked:** Unity animal studio (front, side, top) for Fatsocat and GreyCat, both now posed.

## Round 25: Drahomil

- **Light (changed):** indoors he was a flat black silhouette, and the rim light that follows him drew no outline.
  The rim light only added to the diffuse term, `albedo x light`, and his fur albedo is about 0.03, so the
  light disappeared. `ShellFur.shader` now has a sheen term: each extra light (the rim light, and the lamps for any
  cat) adds `light x attenuation x grazing^2 x _Rim x layer x 3`, whatever the fur colour. Hair at a glancing angle
  catches the light behind it, as on a real black cat. The sheen grows towards the hair tips (`layer`), so it reads as
  a soft fur edge along the spine, ears and tail, not a glow (ART_STYLE §4).
- **Capture fix:** the `cat_face` camera stood inside the kitchen island (the island now reaches the cat's right
  side at the spawn) and showed only a white panel. It now shoots from the cat's left: the face, amber eyes, pink
  nose and whiskers are all clear.
- **Checked:** the full Unity capture cycle: the cat, cat_back and cat_face views in Day, Evening and Night (sheen
  on the spine and tail at night; the eyes and whiskers clear by day), and the room views unchanged.

## Round 26: the town buildings

- **Shape:** the 44 scenery blocks around the block were all flat-topped boxes with a thin dark slab, so the town
  read as cubes from the roofs and the garden. `ContextRoofline` (in each block's own rotated frame) now gives:
  - **Gable roofs:** about two in five of the mid-rise blocks (13.5 m and under, index n % 5 in {1, 3}) get a gable
    roof, ridge along the long side, 0.42 x span high. It alternates slate (#4A5466) and tile (#7D5A4F), with the
    slate texture rows, a darker ridge cap and gable ends in the block's wall colour (the PitchedHouse method).
  - **Cornices:** the other blocks get a pale stone cornice (0.24 m, 16 cm overhang) with the flat roof set inside it.
  - **Plinths:** every block gets a plinth band 0.9 m high at street level, in its wall tone x 0.62, so the blocks
    stand on the pavement instead of floating on it.
  - No colliders (scenery).
- **Light:** the pitched slopes take the sun at different angles, so the skyline shows light and shadow sides. The
  cornices catch a bright line along each flat roof.
- **New capture views:** `town_east` and `town_west`, high over the roofs.
- **Checked:** the full Unity capture cycle (48 views): the town in Day and Evening from both new views.
