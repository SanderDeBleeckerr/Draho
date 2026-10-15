---
name: flemish-site-survey
description: Measure a real plot, garden or building footprint in Flanders (Belgium) from a lat,lon point with free Digitaal Vlaanderen orthophotos and GRB vector data. Use it to size a game level (for example the Draho apartment garden) to real dimensions.
---

# Flemish site survey

## When to use
- You must know the real size and shape of a garden, plot or building in Flanders.
- You must build or check a game level blueprint at real scale.
- Do not use it outside Flanders. The data covers only the Flemish Region.

## Inputs
- One point as `lat,lon` in WGS84 (for example `51.0500,3.7300`, a fake point in central Ghent).
- The user often gives a Google Maps share link. Resolve a `maps.app.goo.gl` short link like this:
  ```bash
  curl -sS -o /dev/null -w "%{redirect_url}\n" <link>
  ```
  Read the `q=lat,lon` value in the result URL. Put the point on the garden or next to the house.

## Privacy rules
- This repository is PUBLIC.
- Keep coordinates, addresses, parcel numbers (CAPAKEY), place names and all images in the scratchpad directory.
- Never write them into a tracked file, a commit message, a PR or an issue.
- In the repo, write only relative metres and directions (for example "garden is 9.4 m x 6.1 m").
- Use fake points in examples and tests.

## Licence notes
- Digitaal Vlaanderen data (GRB, orthophotos) is open under the "Modellicentie Gratis Hergebruik". Attribute "Digitaal Vlaanderen" when you show an image.
- Do NOT download Google Maps satellite tiles. The Google terms forbid it. Use Google Maps only to get the point.

## Steps
1. Install the dependencies: `pip install pyproj shapely pillow`.
2. Run the script. Write the output to the scratchpad, never into the repo:
   ```bash
   python .claude/skills/flemish-site-survey/scripts/survey.py \
       --lat 51.0500 --lon 3.7300 --out <scratchpad>/survey [--radius 25] [--res 0.05]
   ```
   The script does this:
   - It converts the point to Belgian Lambert 72 (EPSG:31370).
   - It gets GRB buildings (`GRB:GBG`, `LBLTYPE` = hoofdgebouw/bijgebouw) and parcels (`GRB:ADP`, `CAPAKEY`) with WFS 2.0 from `https://geo.api.vlaanderen.be/GRB/wfs`.
   - It takes the parcel(s) within 1 m of the point. Open space = parcel minus all buildings.
   - It prints the areas, the minimum rotated rectangle (side lengths and angles), the distance to the nearest building and the open polygon in metres relative to the point.
   - It gets summer (`OMZ`), winter (`OMW`) and current mosaic (`OMWRGBMRVL`, layer `Ortho`) orthophotos with WMS 1.3.0. It draws buildings (red), the parcel (yellow), the point (cyan) and a 5 m scale bar.
   - It makes a "strip" view: the long axis of the open area is horizontal, the far end is on the left and the house is on the right (`--flip` turns it 180 degrees).
3. Look at `strip_sheet.png` and the `*_overlay.png` files. Compare the GRB lines with the photo. Note sheds, terraces, hedges and paths that GRB does not show.
4. Measure extra items on the photo with the scale bar (pixels x `--res` = metres).
5. Write the sizes into the level JSON (see Outputs). Use metres. Do not copy coordinates.
6. Redraw the blueprint and check it:
   ```bash
   python Tools/Level/render_blueprint.py \
       Draho/Assets/_Draho/Levels/Apartment/apartment_layout.json docs/images/apartment_blueprint.png
   ```

## Outputs
In `--out` (scratchpad only):
- `summary.txt`, `summary.json`: areas, rectangle sides, angles, open polygon relative to the point.
- `grb_gbg.json`, `grb_adp.json`, `open_space_l72.json`: raw vector data (contains real coordinates).
- `summer_*.png`, `winter_*.png`, `mosaic_*.png`: raw photos and overlays.
- `summer_strip.png`, `winter_strip.png`, `strip_sheet.png`: rotated strips of the open area.

In the repo, the results feed `Draho/Assets/_Draho/Levels/Apartment/apartment_layout.json`:
- `rooms` with `"outdoor": true` for the garden area.
- `garden_zones` (lawn, terrace, beds), `garden_edges` (hedge, fence, wall) and `climbables`.
- `garden_note` for the direction mapping (see below).

## Map real directions to game axes
- The script gives angles in degrees from east, counter-clockwise (0 = east, 90 = north).
- In the strip view, the left side is the far end of the garden and the right side is the house.
- Choose which game axis follows the long axis of the garden. Then write one note, for example:
  `"garden_note": "game west = real north-east (garden far end); game east = house side"`.
- Do not write coordinates, angles to a real address or place names in the note.

## Known pitfalls
- Streets and squares are public domain. They have no parcel. The script then uses the nearest parcel and says so. Check that this is the correct parcel.
- A garden can span two or more parcels. The script joins all parcels within 1 m of the point. Move the point if it picks the wrong parcels.
- GRB parcel and building edges do not match exactly. The script removes slivers smaller than `--min-part` m2 (default 1).
- GRB does not show small sheds, terraces or fences. Check the photos.
- Orthophoto layer names change every year. List them and pass `--summer-layer` / `--winter-layer`:
  ```bash
  curl -sS "https://geo.api.vlaanderen.be/OMW/wms?SERVICE=WMS&REQUEST=GetCapabilities&VERSION=1.3.0" \
      | grep -o '<Name>[^<]*</Name>'
  ```
- Winter photos (no leaves) were much sharper than summer photos in our test. Trust winter for edges.
- The WMS limit is 2048 x 2048 px. For a large area the script uses a coarser resolution and prints it.
- The WFS BBOX in EPSG:4326 uses lat,lon order. The script handles this; keep it if you edit the code.
- Rotate the strip about the image centre. A rotation about the point pushed the strip out of the frame.
- Photos have a small lean on tall buildings (roofs shift from the footprint). Use GRB lines for wall positions.
