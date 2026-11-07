# Draho art style: stylised, soft low-poly

Decided: November 2026. This guide replaces the "realistic" direction in [GDD §4](GDD.md#4-art-and-audio-direction).
Why: both asset packs in the game (the Sketchfab cat and the Kenney furniture) are already low-poly; stylised
art ages well, fits a phone's budget, and one person with Blender and scripts can make it consistent.
See [the quality study](3D_ART_QUALITY_STUDY.md) for the full reasoning.

![Style frame: the living room in the house style](images/style/style_frame_game.jpg)

The **style frame** above is the target. It comes from
[`Tools/Blender/style_lookdev.py`](../Tools/Blender/style_lookdev.py), and a Unity scene must come close to it.
When this guide and the picture disagree, change one of them so they agree again.

## 1. In one sentence

A warm, sunny, storybook version of a Flemish town, seen at cat height: simple chunky shapes, flat colours
from one palette, and soft light that does the "painting".

## 2. Shapes

| Rule | Hard surfaces (furniture, walls, houses) | Organic (cat, animals, plants, rocks) |
|---|---|---|
| Edges | **Bevel** every outer edge: 1–3 cm on furniture, 3–7 cm on cushions, 5–10 cm on buildings | No bevels |
| Shading | Smooth, with weighted normals (flat faces stay flat, bevels catch the light) | **Faceted** (flat shading): the facets are part of the look |
| Detail | Model the big parts only (frame, cushion, door panel). No screws, no small handles under 2 cm | Silhouette first: ears, tail, leaf shapes must read from 5 m away |
| Proportions | Real sizes where the cat interacts (jump heights, seat heights). Thicker parts than in real life (table tops ×1.5, legs ×1.3) | Slightly larger heads and eyes on animals |
| Avoid | Perfect cubes without a bevel; noisy tiny parts; photo-real proportions in thin parts | Smooth shading on low-poly animals (it looks like plastic); random facet noise |

`draho_style.soften(obj)` and `draho_style.facet(obj)` apply these rules in Blender.

## 3. Colour

**One palette** for the whole game: [`Art/Palette/DrahoPalette.png`](../Draho/Assets/_Draho/Art/Palette/DrahoPalette.png)
(256 × 256 px, 8 × 8 cells, 40 colours used). The colours are defined only in
[`Tools/Blender/draho_style.py`](../Tools/Blender/draho_style.py) (`PALETTE`); the PNG and
`DrahoPalette.json` (name, hex, UV) are generated from it.

![The palette](images/style/palette.png)

- A model has **no textures of its own**. Every face has its UVs on the centre of one palette cell, and the
  model uses the shared material `DrahoPalette`. All the props in a room then share one material, which keeps
  the draw calls low on a phone.
- Add a colour only at the **end** of `PALETTE` (the position is the cell). Change a hex value freely: every
  model follows.
- Saturation is medium: warm wood, teal, mustard, rose, leaf green. Pure white and pure black are not used
  (`white` = `#F7F4EE`, `black` = `#232227`).
- Accent colours (`red`, `mustard`, `cat_eye`) are for things the player must notice: bowls, toys, mice, doors
  that open, the cat's eyes.
- Exceptions with their own material: glass, light bulbs (emission), water, the sky, the cat's eye and nose
  textures.

## 4. Drahomil (the black cat)

A black cat is hard to read. In this style:

- Fur colour `cat_fur` (`#2F2C35`, a blue-violet black, not pure black), with a cool **sheen** (`#8A87A0`) that
  lights up the facets at grazing angles.
- Amber eyes (`cat_eye`) are the brightest thing on the cat and are slightly too big on purpose.
- In Unity: a rim light (or a fresnel term in the cat shader) does what the sheen does in Blender.
- The cat stays faceted (see §2). A future remodel keeps the same face and silhouette, at 8–15k triangles.

![Drahomil in the style frame](images/style/style_frame_cat.jpg)

## 5. Light

Light makes most of the quality in this style (the colours are flat on purpose).

| Light | Value | In Unity |
|---|---|---|
| Sun (key) | warm `#FFE2B8`, low late-afternoon angle, soft shadows (4° sun size) | Directional light, soft shadows |
| Sky (fill) | cool `#A9C4E6` | Environment lighting: gradient or a Poly Haven HDRI, intensity about 0.9 |
| Bounce | warm light from the floor and walls | **Baked lightmaps** + light probes (the Blender frames are path traced, so they show the target) |
| Contact shadows | dark corners, objects sit on the floor | Baked AO + URP SSAO (low setting) |
| Tonemapping | neutral, no strong colour grade | URP *Neutral* tonemapping (Blender uses *Khronos PBR Neutral* to match) |
| Post | a little bloom on the window and sun spots; no vignette over 0.2; no film grain | URP Volume |

Shadows are never pure black or grey: the fill light keeps them blue-ish and warm bounce keeps them soft.

## 6. Budgets (mobile WebGL and iPhone)

| Asset | Triangles | Textures |
|---|---|---|
| Drahomil | 8–15k (now 2.3k) | the palette + eye and nose textures (256 px) |
| Other animals | 3–6k | palette only |
| Furniture, props | 0.2–2k | palette only |
| A building (town) | 0.5–3k | palette only |
| Whole scene in view | under 150k, under 300 draw calls | |

## 7. The pipeline in Blender

| Step | Tool |
|---|---|
| Palette, styling rules, look-dev light, GPU render | [`Tools/Blender/draho_style.py`](../Tools/Blender/draho_style.py) (a module that other scripts import) |
| The style frame and the palette files | [`Tools/Blender/style_lookdev.py`](../Tools/Blender/style_lookdev.py) |
| The cat | [`Tools/Blender/convert_lowpoly_cat.py`](../Tools/Blender/convert_lowpoly_cat.py) |

Run them with a local Blender 5.x. On Windows, give **absolute paths** (Blender resolves relative output paths
in its own way):

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.0\blender.exe" -b --factory-startup `
  --python Tools/Blender/style_lookdev.py -- `
  --cat "$PWD/Draho/Assets/_Draho/Art/Cat/Drahomil.fbx" --out "$PWD/docs/images/style/raw" `
  --palette "$PWD/Draho/Assets/_Draho/Art/Palette/DrahoPalette.png" --samples 128
```

The script uses the GPU (OptiX/CUDA) when it finds one; an RTX 5080 renders the three frames in about
two minutes. The Kenney models are ASCII FBX, which Blender cannot import; Unity recolours them by material
name in `ApartmentBuilder.Recolor` (next step: map those names to palette colours).

## 8. Next steps

1. **Unity:** import `DrahoPalette.png` with Filter Mode *Point*, no mipmaps, no compression; make one
   `DrahoPalette` URP material; read `DrahoPalette.json` in the builders so code-built objects use palette
   colours.
2. **Unity light:** match §5 (sun colour and angle, gradient sky, SSAO), then bake the apartment.
3. **Bevels in code:** the C# builder makes boxes without bevels; add a bevelled box mesh, or swap boxes for
   Blender-made parts.
4. **Assets:** remake props in the house style with Blender scripts, or take CC0 stylised packs
   (Quaternius, Kenney) and run them through the palette.
5. **Drahomil v2:** the same face and colours at 8–15k triangles.
