---
description: The tools, assets and test loop behind Draho.
---

# How Draho is made

## Tools

| Part | Tool |
|---|---|
| Game engine | Unity 6 (Universal Render Pipeline) |
| 3D models and animation | Blender, driven by Python scripts |
| Builds | GitHub Actions makes a web build after each change |
| Hosting | GitHub Pages hosts the free test build |

## Built from real places

The apartment level comes from a blueprint of the real apartment. The garden is measured with open map data
from Flanders (Digitaal Vlaanderen aerial photos and GRB parcel outlines). The exact address is not published.

## The test loop

1. A change is saved to the project.
2. GitHub builds the game for the web in about 6 to 10 minutes.
3. Jim plays the new build on a phone.
4. His notes ("the cat is too small on screen", "the path looks white in the sun") become the next change.

## Assets and credits

* **Drahomil's model:** "Lowpoly Cat Rig + Run Animation" by Daily Lowpoly on Sketchfab (CC BY 4.0), changed for Draho with new animations and a new face.
* **Furniture:** Furniture Kit by Kenney (CC0).
* **Garden door:** a sliding door model by faiyaz5yaz on Sketchfab (CC BY 4.0).

The full list is in [CREDITS.md](https://github.com/SanderDeBleeckerr/Draho/blob/main/CREDITS.md).
