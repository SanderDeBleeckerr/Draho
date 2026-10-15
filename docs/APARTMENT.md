# Level 1: Drahomil's apartment

The first real level is the owner's own apartment: **kitchen → living room → sliding glass door → garden**.
We make a blueprint first. We place and colour the models only after the blueprint is correct.

![Blueprint](images/apartment_blueprint.png)

- **Source of truth:** [`Draho/Assets/_Draho/Levels/Apartment/apartment_layout.json`](../Draho/Assets/_Draho/Levels/Apartment/apartment_layout.json).
  All sizes are in metres. Each prop names the Kenney model that will stand in for it.
- **Redraw the image:** `python Tools/Level/render_blueprint.py Draho/Assets/_Draho/Levels/Apartment/apartment_layout.json docs/images/apartment_blueprint.png`
- **Scale:** the cat is about 0.45 m long. To a cat, the coffee table is a high platform and the sofa is a climb.

## What we saw in the photos and the video

| Area | Details |
|---|---|
| Floor | Large dark-grey tiles (about 60 × 60 cm) everywhere inside. |
| Kitchen | Island with white handle-less drawers, dark-grey top, black induction hob. A back counter with a coffee machine in front of a wood panel wall. |
| Living room | Teal velvet 3-seat sofa against the far wall. Radiator with plants on the left. Wooden square coffee table on a red patterned rug. Cat tree (grey, sisal post) to the right of the sofa. Puzzle poster above the sofa. |
| Inner court | Open-air court with round stones, 1.8 m × 3 m, same width as the kitchen (3 m). North wall = living room north wall, east side = kitchen east wall. Drahomil enters through the west glass door, which is open a crack, next to the cat tree. Its north and east sides are the outer walls of the house; wall-high glass on the south and west sides. |
| Garden side | Floor-to-ceiling sliding glass door. Outside: terrace tiles, a wooden picnic bench, a wooden fence and a brick wall. |
| Cat ideas | Open drawers to hide in (video), the cat tree, the radiator shelf with plants, the sofa back. |

The photos are **not** in the repository, because they show people. Only the blueprint is public.

## Open questions (measure with a phone app or a tape)

1. Total size of the open room (length kitchen → glass door, and width).
2. Is the glass door on the end wall (as drawn) or on a side wall?
3. Size and position of the kitchen island, and which side the drawers face.
4. Where are the front door, the hallway and the stairs (if any)?
5. Size of the garden and where the fence and the brick wall are.

## Garden (blueprint v14)

From the owner's description and open aerial data of Flanders (Digitaal Vlaanderen orthophotos and
GRB parcel outlines, free reuse licence). The exact location is not stored in this repository.

- A strip about **4.2 m wide and 24 m long** behind the sliding door (the game keeps 5 m, the width
  of the apartment).
- In real life the garden runs north-east from the house. In the blueprint it runs west:
  game west = real north-east, game south = real north-west, game north = real south-east.
- **Terrace** (pavers) for the first 5 m, with the **picnic table** close to the house; **lawn** after it.
- **Game south (real north-west):** the side wall of the neighbouring building (guess: 6 m high), with a
  small **firewood stack** against it. At the far end **ivy** hangs down from the top: the cat can climb it.
- **Game north (real south-east):** the fence to the neighbour's garden: **concrete posts with
  horizontal wooden planks** (guess: 1.8 m).
- **Stone path:** 30 cm wide, 1 m from the building wall, from the terrace to the far end, over the lawn.
- **Maple tree:** 2 m from the far end, close to the neighbour's fence. The cat can climb the trunk to
  about fence height (1.9 m); the crown is higher. Blackbirds also perch in it.
- **Far end:** an outbuilding about 4.2 m high (from the height model), not a fence.
- **Rooftops (chapter 2):** behind the ivy wall is a flat roof at about 6.5 m, about 13 m x 22 m. Next to it:
  a lower roof (5.8 m), a high block (8.6 m, with a drainpipe) and a low back roof (3.7 m). Heights come from
  the open Flemish height model (DHM II, surface minus terrain, 1 m grid).

## The neighbour's garden (chapter 3)

From the height model: a strip about 24 m x 4.5 m over the east fence, mostly low wild growth (no trees),
closed at the back by the long wall of a workshop (about 5.5 m) and near the house by a low annex (about 3 m).
The owner says the neighbour is almost never home and the garden is wild. In the game: a loose plank in the
fence near the maple, tall grass, brambles, an upside-down wheelbarrow (hedgehog nest), a fallen chair, a barrel.

