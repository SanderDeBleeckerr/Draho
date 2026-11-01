# Study: what is missing for quality 3D art in Draho

Date: November 2026. Scope: why the graphics look "mediocre", and what an
AI-driven project needs to reach indie-mobile quality.

## 1. Short answer

The quality gap is mostly a **tool and pipeline gap**, not a limit of AI-driven
development. In this project, Claude writes every shape as code (boxes,
cylinders, extruded outlines) and checks the result only through headless
screenshots. No real modelling tool, no generative 3D model, no image
generator, no GPU and no human art review are part of the loop. Indie studios
get their look from artists, asset packs, baked lighting and art direction.
Draho has none of these four. With the pipeline in section 5, indie-mobile
quality is a realistic target. AAA quality is not: that needs large art teams,
and AI tools do not replace them yet.

## 2. What the project uses now

| Part | How it is made | Result |
|---|---|---|
| Apartment, garden, town (717 buildings) | C# code in `Editor/ApartmentBuilder.cs` (about 5000 lines): boxes, extrusions, ear-clipped outlines | Correct sizes, but flat, hard edges, no bevels, no wear |
| Cat (Drahomil) | A low-poly Sketchfab cat, converted and rigged with Blender Python (`Tools/Blender/`) | The best asset in the game, but low-poly, simple keyframed animation |
| Fox, hedgehog, mice, birds, people | Primitives put together in code | Read as "toy figures" |
| Furniture | Kenney CC0 Furniture Kit (low-poly, flat colour) | Clean but generic |
| Textures | 21 textures drawn by code (brick, plaster, cobbles, tiles), 2 image files | No real photo detail, few normal maps, no roughness or AO maps |
| Lighting | Real-time only: sun, a few lights, bloom, tonemapping, vignette | No baked light, no soft shadows indoors, no light bounce |

## 3. Why it looks mediocre (most important first)

1. **Lighting.** Players notice light first. Good mobile games bake light
   (lightmaps) and light probes, so that corners get darker, light bounces off
   walls and objects sit on the ground. Draho has no baked light, so rooms look
   flat and objects seem to float.
2. **Materials.** Every surface is one colour or a small code texture. Real
   materials need a full PBR set: colour, normal, roughness and ambient
   occlusion. Without them, wood, cloth and stone all look like plastic.
3. **Shapes.** Code-built boxes have no bevels, no silhouette detail and no
   small details (wear, cracks, seams). A real artist adds these with a few
   bevels and a normal map baked from a high-poly model.
4. **Characters and animation.** The animals are made from primitives and move
   with hand-written keyframes. Good games use sculpted, rigged models and
   motion capture or many hand-animated clips with blending.
5. **No art direction.** No style guide, no colour script and no reference
   board exist. Each round added "more things", but no one fixed one
   consistent look (for example: stylised, soft shapes, warm palette).
6. **Blind review.** Claude sees the game only in screenshots from a headless
   browser, at low resolution and without a human eye. Small faults (seams,
   Z-fighting, wrong scale, lifeless poses) pass too easily.

## 4. What is missing in this environment

| Missing | Why it matters | Status now |
|---|---|---|
| **Generative 3D model service** (image-to-3D or text-to-3D: Meshy, Tripo, Rodin/Hyper3D, CSM) | Gives textured models in minutes, instead of shapes made from boxes | The hosts are reachable from the container, but there is **no API key** |
| **GPU** | Open models (Hunyuan3D, TRELLIS) and fast Blender baking need one | The container has 4 CPU cores, 15 GB RAM, **no GPU** |
| **Blender in the loop** | Remesh, decimate, UV unwrap, bake normal and AO maps, rig, export | Blender scripts exist in `Tools/Blender/`, but Blender is not installed in this container, and not in CI |
| **Image generator** | Concept art, reference sheets for image-to-3D, tiling textures | Not available here. (The Figma connector lists image and model tools, but it needs authorization first) |
| **CC0 asset libraries** | Poly Haven (textures, HDRIs, models), Quaternius and Kenney (stylised models), ambientCG (textures) | Only the Kenney furniture kit is used |
| **Baked lighting in the build** | The biggest quality step on mobile WebGL | The scene is built in code during CI, and no lightmap bake runs |
| **Art direction and a human reviewer** | Decides the style, and says "this looks wrong" | Not defined |

## 5. A pipeline that would work

Each step can run without a person, but a person checks the result at step 6.

1. **Style first.** Write a one-page style guide: shape language, palette,
   texel density, polygon budgets. Make 3–5 concept images per asset (image
   generator or the owner's own photos).
2. **Generate the model.** Send the concept image to an image-to-3D service
   (Rodin or Tripo for clean topology, Meshy for the full chain with
   texturing and rigging). Ask for 4 candidates, keep the best.
3. **Clean up in Blender, headless.** A Python script does a quad remesh or
   decimate to the budget, UV unwraps, bakes normal and AO maps from the
   high-poly source, makes 2–3 LODs and exports FBX or glTF. Budgets for
   mobile WebGL: hero cat 8–15k triangles, animals 3–6k, props 0.2–2k,
   textures 512–1024 px.
4. **Rig and animate.** Use the auto-rig of the generator, or a quadruped rig
   script in Blender. For the cat, buy or license a set of quadruped motion
   clips, or keep hand keys but add many more clips with blending.
5. **Light the scene.** Bake lightmaps and light probes in Unity during CI
   (the Progressive CPU lightmapper works without a GPU, but it is slow, so
   bake only the apartment and the garden). Add screen-space AO and one
   reflection probe per room.
6. **Review.** Render each new asset in a fixed "turntable" scene: 4 angles,
   neutral light, next to the cat for scale. A vision model compares it with
   the concept image and the style guide. The owner approves or rejects in the
   PR.

Order of value: step 5 (light) and CC0 materials give the biggest gain for the
least work. Generated models (steps 2–4) come next, the cat and the fox first.

## 6. Quick wins that need no new account

- Replace the code textures with **Poly Haven / ambientCG** CC0 PBR sets
  (wood floor, plaster, brick, cobbles, grass), at 512–1024 px.
- Use a **Poly Haven HDRI** for the sky reflection and ambient light.
- **Bevel** the edges of code-built boxes (a 1–2 cm chamfer catches light).
- Swap code-built props for **Quaternius / Kenney** CC0 stylised models
  (plants, bins, bikes, garden items).
- Add **SSAO** to the URP renderer, and contact shadows under the animals.
- Install Blender in the CI image, so the existing `Tools/Blender/` scripts run
  on every build.

## 7. What the owner must give or decide

1. **An API key** for one 3D generator (for example Tripo or Meshy), stored as
   an environment secret, not in the repo. Expect a monthly subscription.
2. **A style decision**: stylised (easier, ages well, fits mobile) or
   realistic (harder, needs more texture memory).
3. **References**: photos of the real cat from several sides, so that
   image-to-3D can make a better Drahomil. These stay out of the public repo,
   like the garden photo.
4. **Optional: a human artist** for the hero assets (cat, fox). A few days of
   work from a freelance artist on the cat gives more than many AI rounds.

## 8. Honest limits

- Generated models still need cleanup: topology, scale, UV seams and texture
  faults. All current reviews say a remesh step is needed for game-ready
  output.
- Generated characters often have bad paws, eyes and fur. The cat is the
  main character, so it needs the most care.
- Mobile WebGL limits the budget (about 300 draw calls, 150k triangles), so a
  better look comes more from light and materials than from more polygons.
- AAA mobile quality depends on teams of artists, custom shaders and months of
  polish. No current AI tool chain reaches that alone.

## Sources

- 2026 tool comparisons: [Strayspark – Meshy, Rodin, Tripo, CSM](https://www.strayspark.studio/blog/generative-3d-tools-comparison-meshy-rodin-tripo-csm-2026),
  [3D AI Studio – game-ready output](https://www.3daistudio.com/blog/best-ai-game-asset-generators-2026),
  [Krea – best AI 3D generators 2026](https://www.krea.ai/blog/best-ai-3d-model-generators-2026),
  [Virtuall – 12 AI tools for 3D modelling](https://virtuall.pro/blog/ai-tools-for-3-d-modeling).
  Many of these are written by vendors, so the rankings are only a guide.
- Headless Blender by an AI agent: [dcc-mcp-blender on PyPI](https://pypi.org/project/dcc-mcp-blender/0.1.36/)
  (UV, bake and export tools; no auto-retopology found).
- CC0 assets: Poly Haven, ambientCG, Quaternius, Kenney.

## 9. Research update: papers from July to October 2026

Google Scholar refuses automated access (HTTP 403), so this list comes from
arXiv, where most of these papers appear first. They are preprints (not yet
peer reviewed), and only the abstracts were read.

| Trend | Papers | What it means for Draho |
|---|---|---|
| **Native "artist" meshes**: clean, low-poly topology made directly, fast (flow matching, not token by token) | Meshy T2 (2607.28675), MeshCarve (2610.09723), MeshOctave (2609.38985), TaoFlowForge (2609.37139), OptimusMesh (2610.01148), Dynamic Homing RL (2610.01233) | Attacks the biggest problem in section 8: the remesh step. Meshy T2 comes from a commercial tool, so it is likely to reach the product |
| **Part-level generation**: the model comes out in separate parts | FlexPart (2610.31671), KaiNinja (2609.15659), Seg3DParts (2609.36918), MultiCube (2608.20448) | Separate legs, tail and head make rigging and editing easier |
| **PBR and relightable textures** | SUPRA-TEX (2610.22352), Luce (2608.23943), Texture++ (2607.21504), BTC3D (2609.39709) | Materials that react to the game light. Texture++ makes low-resolution textures sharper, which could help the existing assets |
| **Moving and physical assets** | MoRE (2610.30337), MotionAnimator (2610.15074), PhysAg3D (2610.39924), PhysX-CoT (2608.08053) | Doors and drawers with working joints. These are hinged objects, not animals |
| **An LLM writes the model as a program** | USDCraft (2610.11322) | The same method as Draho's code-built level. It works for hard, mechanical objects, but not for organic shapes like animals |
| **Faster and larger** | Channel Match few-step (2610.15321), Filigree3D up to 2048³ voxels (2609.34900), Hunyuan3D-Buffalo: generate, understand and edit in one model (2608.02711) | Lower cost per asset, and edits by text ("make the tail longer") |
| **Doubts about evaluation** | Frozen Scenes (2610.00447), Cyc3D (2608.28080) | Leaderboards change with the camera and prompt settings. Test the tools on Draho's own assets, not on rankings |

**Conclusion:** research in 2026 moves straight at the gaps in this study:
clean topology, parts, PBR materials and editing. But no paper in this list
gives a rigged, animated, furred quadruped ready for a phone. Most need a
large GPU, and research code often has a non-commercial licence. For Draho,
the practical route stays the same: a commercial service that puts these
methods into a product, and then the cleanup, rig and baked lighting steps
from section 5.

## 10. The top 3D generators and the benchmarks (November 2026)

### Live ranking: 3D Arena (Hugging Face)

3D Arena shows people two models made from the same image, and they vote for
the better one (Elo score, start value 1200). Read from its API on
1 November 2026:

| # | Model | Elo | Votes | Open |
|---|---|---|---|---|
| 1 | PicGen3D | 1415 | 4376 | no |
| 2 | TRELLIS (Gaussian splats) | 1397 | 4840 | yes |
| 3 | Meshy-6 | 1351 | 1037 | no |
| 4 | 404_GEN | 1334 | 1469 | no |
| 5 | Meshy-7 | 1313 | 263 | no |
| 6 | TRELLIS (mesh) | 1303 | 5782 | yes |
| 7 | Zaohaowu3D | 1294 | 2990 | no |
| 8 | Hunyuan3D-2 | 1283 | 5247 | yes |
| 9 | Hunyuan3D-2.1 | 1273 | 2218 | yes |
| 10 | InstantMesh | 1269 | 11614 | yes |
| 11 | SAM-3D-Objects (splats) | 1267 | 1416 | yes |
| 18 | TRELLIS.2-4B | 1127 | 1439 | yes |
| 20 | TripoSR (old, open) | 1081 | 11390 | yes |

Read it with care:
- Only models that someone submitted are ranked. **Rodin (Hyper3D), Tripo
  3.x, CSM Cube and Hunyuan3D 3.x are not on the board**, although reviews
  often name them as the best.
- "Splat" entries are Gaussian splats, not meshes. Unity on a phone cannot
  use them as normal game models.
- Votes judge how a model looks in a viewer, not clean topology, UVs or how
  easy it is to rig.
- Models with few votes (Meshy-7: 263) can still move a lot.

### Reviews (not benchmarks)

Most 2026 reviews name **Meshy, Tripo, Rodin and Hunyuan3D** as the top four,
and TRELLIS.2 (MIT licence) and Hunyuan3D 2.1 as the best open models. Many
reviews come from vendors, and the version names differ between sources.

### Research benchmarks

| Benchmark | Year | Covers | Method |
|---|---|---|---|
| T3Bench | 2023 | text-to-3D | prompts at 3 levels of difficulty, 10 methods |
| GPTEval3D | 2024 | text-to-3D | a vision-language model (GPT-4V) judges, close to human votes |
| MATE-3D | 2024 | text-to-3D | 1280 meshes, 4 quality dimensions |
| 3DGen-Bench | 2025 | text and image-to-3D | 19 open models, human pairwise votes |
| Hi3DEval | 2025 (NeurIPS) | text and image-to-3D | 15,300 assets from 30 generators, scores per part and per material |
| Cyc3D | 2026 | image-to-3D | also tests if the asset is usable in a graphics pipeline |
| Frozen Scenes | 2026 | text-to-3D evaluation itself | shows that rankings change with camera and prompt settings |

**No benchmark yet measures what a game needs most**: clean low-poly
topology, good UVs, a working rig and performance on a phone. So for Draho the
best test stays a small own benchmark: 5 Draho assets (cat, fox, hedgehog,
garden bin, bike), made by 3 or 4 generators, rendered in the turntable scene
from section 5, and scored by the owner.
