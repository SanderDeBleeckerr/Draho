# Draho: Assets

## Chosen (October 2026)

| Use | Asset | License | How it gets into the repo |
|---|---|---|---|
| Cat | [Lowpoly Cat Rig + Run Animation](https://sketchfab.com/3d-models/lowpoly-cat-rig-run-animation-c36df576c9ae4ed28e89069b1a2f427a) (Sketchfab) | Checked by the workflow. Only CC0, CC BY or Standard pass. | `Fetch assets` workflow → `ThirdParty/Sketchfab/lowpoly-cat/` (raw glTF) |
| Furniture | [Kenney Furniture Kit](https://kenney.nl/assets/furniture-kit) | CC0 (no credit needed) | `Fetch assets` workflow → `Draho/Assets/_Draho/ThirdParty/Kenney/FurnitureKit/` (FBX) |

The level is built from [APARTMENT.md](APARTMENT.md) (the blueprint). The look moves from "realistic" to
**low-poly** for now, because both packs are low-poly.

**The cat has only a run animation.** We still need idle and walk. Plan: convert the glTF to FBX in Blender
and add procedural idle/walk clips for its rig, as we did for our own cat in `Tools/Blender/build_drahomil.py`.

### How to fetch

1. Sketchfab only: make a free account on sketchfab.com, go to **Settings → Password & API**, copy the API token,
   and add it as the repository secret `SKETCHFAB_API_TOKEN`.
2. GitHub app → **Actions → Fetch assets → Run workflow**. The workflow commits the files to that branch.
   Kenney still works without the Sketchfab token.

---

## Old shortlist (realistic, paid — on hold)

## Purchase checklist

For **each** asset, check these items:
- [ ] The license allows commercial use in a paid or free-to-play mobile game.
- [ ] The format is FBX (or a Unity package) with a humanoid or generic rig.
- [ ] The asset supports URP, or uses standard PBR textures that you can convert easily.
- [ ] The polygon count and the texture sizes are in the budget (see [V1_PLAN.md §5](V1_PLAN.md)).
- [ ] The publisher updated it in the last 2–3 years.

## 1. Cat model (realistic, black, rigged)

**Version 1 needs:** idle, walk and run loops.
**Later versions need:** jump, land, sit, lie, sleep, crouch-walk, pounce, eat and meow.
Many animations in one pack save a lot of work later.

| Candidate | Store | Notes |
|---|---|---|
| "Realistic Cat 3D Model PBR Rigged Game Ready" / "Realistic Cat Rigged Animated" (~$40) | [CGTrader – realistic cat](https://www.cgtrader.com/3d-models/realistic-cat) | Several realistic listings are rigged and animated, and some include a Unity package. Compare the animation lists. |
| Black domestic cat with PBR, rig and animation (~$40) | [CGTrader – cat](https://www.cgtrader.com/3d-models/cat) | A black variant is ready. Check the fur method (shells or cards need a custom shader). |
| Free "Black Cat" | [CGTrader – cat](https://www.cgtrader.com/3d-models/cat) | Good for testing the scale and the material before you buy. |
| Animated rigged cats | [CGTrader – animated cats](https://www.cgtrader.com/animated-3d-models/cat) | Wider search. Filter on "game ready" + "Unity". |
| Unity Asset Store search | [assetstore.unity.com](https://assetstore.unity.com/) → search "cat" | Most results are stylized or low-poly. Search for "realistic" and check the black skins. |

**Not a fit:** stylized, chibi or low-poly packs on the Unity Store and on itch.io
(for example [Low Poly Mammal: Cat](https://assetstore-fallback.unity.com/packages/3d/characters/low-poly-mammal-cat-113362)
and [Vibe Poly Cats](https://silver-delivery.itch.io/vibe-poly-cats-rigged-animated)).
They are useful only as a **placeholder** in week 1.

> If no pack has enough animations, you can buy a realistic model and an
> animation pack separately. Then a retargeting step is necessary.

## 2. Apartment interior (realistic, mobile-friendly)

| Candidate | Notes |
|---|---|
| **[HQ Apartment Interior](https://assetstore.unity.com/packages/3d/environments/hq-apartment-interior-284211)** ⭐ first choice | Separate URP and HDRP packages, 66 prefabs, texture atlas, baked lighting. The listing says it is for desktop, VR and mobile. |
| [ArchViz Modern House Interior](https://assetstore.unity.com/packages/3d/environments/archviz-modern-house-interior-141349) | URP materials and clean topology. The textures are ≥ 2048, so you must reduce them for mobile. |
| [HQ ArchViz Interior Vol 4](https://assetstore.unity.com/packages/3d/environments/urban/hq-archviz-interior-vol-4-285932) | Realistic and has URP, but the scene has about 1.1M vertices. Too heavy unless you use only part of it. |
| [HD Apartment](https://assetstore-fallback.unity.com/packages/3d/environments/urban/hd-apartment-97614) | Older. Check the URP support. |
| [House Interior – Apartment – 3D Low Poly](https://marketplace.unity.com/packages/3d/props/furniture/house-interior-apartment-3d-low-poly-296986) | About 108k triangles and very mobile-friendly, but **stylized**. Use it only if realistic does not work. |

Use only **one room** of the pack for version 1 (the living room). Keep the rest for v0.2.

## 3. Audio (cat sounds)

| Need | Where to look |
|---|---|
| Paw steps (wood, carpet, tile) | Unity Asset Store footstep packs, [Freesound.org](https://freesound.org) (check that the license is CC0 or CC-BY) |
| Meow, purr, chirp, hiss | Freesound.org, Sonniss GDC bundles (free, royalty-free), or **record a real cat** 🐈‍⬛ |

## 4. Engine packages (free)

- Universal RP, Input System and uGUI come from the Unity Package Manager (see `Draho/Packages/manifest.json`).
