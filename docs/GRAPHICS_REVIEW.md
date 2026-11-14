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
| 10 | Drainpipe | code cylinder | Blender: segments, brackets, shoe |
| 11 | Garden picnic table | code boxes | Blender: planks, A-frame legs |
| 12 | Garden lamp | code primitives | Blender: lantern with glass |
| 13 | Garden fences (neighbour, end, wild) | code boxes | check scale, posts, planks variation |
| 14 | Garden trees (maple, others) | code blobs | Blender: faceted canopy clusters, trunk |
| 15 | Lawn, path, terrace | flat colour, tiles | grass variation (vertex colour), path slabs with height |
| 16 | Ivy wall | `WindFoliage` cards | leaf density, colour |
| 17 | Wild garden (bushes, wheelbarrow, chair, barrel) | code primitives | Blender props |
| 18 | Humans (legs at the door) | code cylinders | Blender: legs, shoes, coat hem |
| 19 | Mouse | Blender (animals) | fur/colour check at cat height |
| 20 | Pigeons | Blender | sheen, perched pose |
| 21 | Blackbirds | Blender | check |
| 22 | Hrozna (corgi) | Blender | check |
| 23 | Zlatenka (tortoise), hedgehog, fox, ducks, gull | Blender | check |
| 24 | Fatsocat, the grey cat (NPC cats) | Drahomil model + fur | colour per cat, fur settings |
| 25 | Drahomil | Blender + shell fur | fur at night, eye shine |
| 26 | Town buildings near the flat | code extrusions | window frames, roof edges |
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
