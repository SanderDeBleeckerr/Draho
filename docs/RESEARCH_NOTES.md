# Research notes (improvement loop)

The improvement loop (`.claude/skills/game-improvement-loop`) searches the web before it changes the game.
Each entry: the question, the sources, and what we do with it in Draho.

## Round 28 (2026-10-17)

**Stealth: how does the player know why an animal noticed him?**
Sources: [How to design a stealth game's detection system (Bugnet)](https://bugnet.io/blog/how-to-design-a-stealth-games-detection-system),
[Godot stealth genre notes](https://skills.cat/skills/thedivergentai/gd-agentic-skills/godot-genre-stealth),
[Sneaking as a playful input modality (arXiv)](https://arxiv.org/pdf/2102.02024),
[Sly 2 detection redesign concept (The Rookies)](https://www.therookies.co/projects/89534).
Findings: detection should be graded and readable; players must see *why* they were detected; noise is
hard to read, so games show a noise meter or the range of a sound.
In Draho: a **noise ring** on the ground around the cat (still 0.25 m, sneak 0.4 m, walk 1.2 m, run 2.4 m,
the mouse's hearing distances), shown only when a listening animal is near.

**Navigation: how to guide the player in a large area without spoiling exploration?**
Sources: [Guiding with a Soft Touch (Press Start)](https://press-start.gla.ac.uk/press-start/article/view/129),
[Diegetic guidance thesis (Dartmouth)](https://digitalcommons.dartmouth.edu/masters_theses/264),
[Why the waypoint marker should go away (TheSixthAxis)](https://www.thesixthaxis.com/2015/12/02/opinion-why-the-waypoint-marker-should-go-away/),
[Minimap menace (Wayline)](https://www.wayline.io/blog/minimap-menace-waypoints-killing-exploration).
Findings: players accept on-screen help that gives *relative* direction; full paths on the ground make
exploration boring; too little help frustrates.
In Draho: a small **goal compass** (a chevron and the distance) that points to the next step of the
current goal, and hides when the goal is closer than 8 m. Long routes point to the next waypoint only.

**Graphics: low resolution on slow phones, flat low-poly lighting.**
Sources: [Unity manual: Introduction to changing resolution scale](https://docs.unity3d.com/Manual/resolution-scale-introduction.html),
[How to make low poly look good in Unity (forum)](https://makegamessa.com/discussion/comment/30841).
Findings: URP can upscale with AMD FSR 1 (sharper than bilinear) when the render scale is below 1;
low-poly scenes get much of their look from gradient ambient light and the palette, and ambient
occlusion / depth effects are expensive on phones.
In Draho: URP upscaling filter **FSR 1** (sharpness 0.8) for the adaptive render scale; a **warmer
trilight ambient** (warm horizon and ground bounce). No SSAO (cost on phones).

## Round 29 (2026-10-17)

**Game feel: how do jumps and landings feel good, and how should the camera follow a small, quick character?**
Sources: [Jumping and landing feedback (Vaults and Vampires devlog)](https://rcxa.itch.io/vaults-and-vampires/devlog/662233/jumping-and-landing-feedback),
[GDevelop: character game feel](https://wiki.gdevelop.io/gdevelop5/extensions/character-game-feel/),
[Platformer animation guide (MoCap Online)](https://mocaponline.com/blogs/mocap-news/platformer-animation-guide),
[Cinemachine follow cameras in practice](https://uhiyama-lab.com/en/notes/unity/unity-cinemachine-camera-guide/),
[Build a robust third person camera (Wayline)](https://www.wayline.io/blog/build-robust-third-person-camera-system),
[Camera damping in a platformer (MakeCode forum)](https://forum.makecode.com/t/camera-damping-in-platformer/39276).
Findings: land with a squash (lower and wider) scaled by the fall speed, keep the volume; keep
anticipation very short (2-4 frames) so the jump does not feel late; the camera's feel comes from
damping, a dead zone and lookahead; too much damping feels unresponsive, snapping feels bad.
In Draho: stretch on take-off, squash on landing (6-22 %, by fall speed, volume kept), a dust puff on hard
landings, camera lookahead (0.18 s of movement) and calmer vertical follow (sharpness 5 instead of 12)
except while climbing. No anticipation crouch (input must stay instant on a phone).

**Moving vegetation (wiki milestone 2).** The search found mostly asset-store wind shaders
([Stylized Grass Shader](https://assetstore.unity.com/packages/vfx/shaders/stylized-grass-shader-143830),
[Stylized Grass with Wind](https://marketplace.unity.com/packages/3d/vegetation/plants/stylized-grass-wind-194396)):
opaque geometry, a global wind controller, cheaper modes for weak devices. Next: our own small vertex wind
shader for the grass and ivy meshes (to do in a later round, measured with the perf scenario).

## Round 30

**Wind in the grass and ivy: how to write a small URP foliage shader by hand?**
Sources: [Introduction to custom lighting in URP (Unity 6.3 manual)](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/custom-lighting/introduction.md),
[Use built-in lighting methods in a URP shader (Unity 6 manual)](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/use-built-in-shader-methods/lighting),
[Creating a foliage shader in URP (Ned Makes Games)](https://nedmakesgames.medium.com/creating-a-foliage-shader-in-unity-urp-shader-graph-5854bf8dc4c2).
Findings: include Lighting.hlsl and call GetMainLight (with a shadow coord) in a "UniversalForward" pass;
put the wind offset in the vertex function before the clip-space transform; foliage is two-sided, so flip the
normal on back faces; a weight per vertex keeps the roots still.
In Draho: `Draho/WindFoliage` (Art/Shaders). uv.y is the sway weight (grass root 0, tip 1; ivy stem 0, sides 0.55,
tip 1). A slow gust in world space (so it still works after static batching) plus a quick flutter. Wrap lighting,
back-light translucency, ambient SH, fog. Grass sways 7 cm at the tip, ivy 1.2 cm. No shadow-caster pass
(the grass already cast no shadows; the ivy leaves now cast none either).

## Round 31 (2026-10-17): chapter 8, the inner court

**Question.** How do birds in games (and in real life) react to an approaching predator, and how do we make
a tiny, tall space (a 3 x 1.8 m light well) fun to play in?

**Sources.**
- Feral pigeons in New York City flee later where there are more people: their flight initiation distance (FID)
  becomes shorter with human activity. ([Zenodo record 3936120](https://zenodo.org/records/3936120))
- A study of 62 bird species: birds tell a direct approach from a tangential one, maybe by the speed or
  acceleration of the approach. ([Behavioral Ecology 25(4):960](https://academic.oup.com/beheco/article/25/4/960/257797))
- A bird that flees the player: a trigger radius, then a flight away from the player and upwards.
  ([Bird AI and Inheritance, itch.io devlog](https://hookedfeelings.itch.io/dungeon-siege-project/devlog/100759/bird-ai-and-inheritance))
- Flocking and steering behaviours (Reynolds' boids) as the base of group movement.
  ([AI for Game Developers, ch. 4: Flocking](https://oreilly.com/library/view/ai-for-game/0596005555/ch04.html))
- Vertical levels: a "golden path" that winds upward, so the player feels progress through height; short way back
  after a fall. ([Jungle Gym level design write-up](https://hammillb.myportfolio.com/junglegym))

**What we do.**
- `Pigeon` (new creature): the flee distance depends on the cat's speed (run 2.6 m, walk 1.3 m, sneak 0.45 m)
  and on the approach: x1.6 when the cat comes straight at the pigeon, x0.6 when it walks past to the side.
  City pigeons are used to people, so the distances are short (the court is small).
- Flock alarm: when one pigeon flies up, every other pigeon on the ground follows after 0.15-0.5 s.
- The court becomes a light well (8.8 m, from the height model) with window sills and a gutter: the pigeons
  perch higher and higher, and the jingle ball sits up the drainpipe, so the goal leads the eye upward.
- The way down is short (a 3 m drop onto the stones), so a mistake costs little.

## Round 32 (2026-10-18): a better crawl (stalking) animation

**Question:** the crawl looked stiff: curled front paws, a straight tail held high, a high head and a flat,
board-like body. How does a real cat stalk, and how do animators make a sneak walk look alive?

**Sources**
- Bishop, Pai & Schmitt (2008), "Whole body mechanics of stealthy walking in cats", PLOS ONE,
  https://www.ncbi.nlm.nih.gov/pmc/articles/PMC2583958/ : the trunk stays level, the hips go about 30 % lower,
  the head is low; crouched cats use a more diagonal footfall pattern and recover less energy (stealth over economy).
- Animation Mentor, "Animating a sneak walk cycle with personality",
  https://www.animationmentor.com/blog/tutorial-animating-sneak-walk-cycle-with-personality/ : a perfectly level
  body looks mechanical; add a small up-down motion to hips, shoulders and head; knees never fully straight.
- SIGGRAPH HyperGraph walking exercise,
  https://education.siggraph.org/static/HyperGraph/animation/character_animation/walking/walking_excercise.htm :
  animate the hips first; the shoulders answer the hips.
- Student cat gait study (arts.ac.uk) : the paws lift very little, the head stays still on the prey.

**What we do** (in `Tools/Blender/convert_lowpoly_cat.py`, baked into `Drahomil.fbx`)
- Paws stay flat: a damped-track constraint keeps each paw in its rest direction while the legs bend deep.
- Head low and forward under the shoulder line, eyes level, ears forward; tail low, tip twitches.
- Steps: the paw reaches forward, hovers just above the ground and then sets down (`hover` in `leg_offset`).
- Slightly more diagonal phases (fore legs at 0.3 / 0.8 instead of 0.25 / 0.75), elbows and knees 1.2 cm out.
- Two small dips per cycle in hips and shoulders, out of phase; the head cancels the chest rock.
- 36-frame cycle, stride 0.16 m: clip speed 0.196 m/s, so at the crouch speed (0.28 m/s) the clip plays at 1.4×.

## Round 33 (2026-10-18): evening and night light (SkyMood)

**Question:** chapter 9 plays at night, and the hero is a black cat. How do games make night feel dark but
stay readable, and how do they blend the time of day at runtime in URP?

**Sources**
- The Level Design Book, "Lighting for darkness", https://book.leveldesignbook.com/process/lighting/darkness :
  "Hollywood darkness": feel dark without being dark; dim, soft, blue-ish fill lights without shadows; rim light
  to pick out silhouettes; let some shadows reach black (contrast, not darkness); avoid plain blue-orange, add a
  third hue.
- Wikipedia, "Day for night", https://en.wikipedia.org/wiki/Day_for_night : moonlight looks blue to the eye
  (Purkinje effect), so film and games tint it blue.
- Unity BoatAttack sample, `DayNightController.cs` : one time value drives the sun rotation and colour, the sky
  tint, the fog colour and the ambient light together.
- Wayline, "Easy URP day/night cycle", https://www.wayline.io/blog/easy-urp-day-night-cycle-unity-scripting :
  lerp the directional light and the skybox exposure from a script.

**What we do** (`Scripts/World/SkyMood.cs`, created at runtime in every scene)
- Three presets, blended with a smooth step: Day (the scene as built), Evening (low warm sun, pink-orange fog),
  Night (high cool moon at 0.42 that still casts shadows, blue-violet trilight ambient, fog 25-95 m, sky exposure 0.22).
- The camera's CatFillLight becomes a moon-blue rim at night (1.1, range 2.2 m): the black fur keeps its shape.
- The story sets the mood: beat 31 turns to Evening. Test hook `SkyMood.TestMood`, scenario `sky`.

## Round 34 (2026-10-18): moths at the garden lamp, night colour grading

**Questions:** how do moths really fly around a lamp (so the catch game feels natural)? How do games grade
night so it reads as night and not as a dark day?

**Sources**
- Fabian, Sondhi et al. (2024), Nature Communications, reported by Mongabay
  https://news.mongabay.com/2024/02/like-a-moth-to-a-flame-science-finally-explains-why-insects-flock-to-artificial-lights/
  and Physics Today https://physicstoday.aip.org/news/why-insects-orbit-light-at-night : insects keep their
  backs to the brightest point (dorsal light response). Near a lamp this gives orbiting, stalling and
  inverting flights, not a straight line into the light.
- Factorio forum on night rendering https://forums.factorio.com/viewtopic.php?p=398614 and The Artist's Road,
  "The Purkinje effect" https://www.theartistsroad.net/p/the-purkinje-effect : in the dark the eye loses colour
  and shifts to blue-green; games desaturate and tint shadows blue, and keep the highlights near neutral or warm.

**What we do**
- `Moth.cs`: loops around the lamp, body tilted with its back to the light; random stalls with a turn back;
  dives (about 1 m lower, wider ring) are the moments a jumping cat can catch one. Hidden by day.
- `NightLamp.cs`: the lantern switches on with `SkyMood.NightAmount`; emissive glass for the bloom.
- `SkyMood`: at night saturation -35 and split toning (blue shadows, warm highlights).
- Story: chapter 9 opens at the lamp; the garden table is 1 m from the lantern, a natural jumping-off point.

## Round 35 (2026-10-18): the fox, and a follow mission that is fair

**Questions:** how do urban foxes and cats behave toward each other at night? Players often dislike
"tailing" missions: what goes wrong, and how do we avoid it?

**Sources**
- "Adaptations of the red fox to urban environments in Sydney" (Journal of Urban Ecology, 2020),
  https://researchers.mq.edu.au/en/publications/adaptations-of-the-red-fox-ivulpes-vulpesi-to-urban-environments-/ :
  fox activity overlapped 78 % with cats (both nocturnal), only 20 % with dogs.
- Anita Kelsey, cat behaviourist, "Are foxes a threat to cats", https://www.catbehaviourist.com/?p=332 :
  foxes and cats meet many times every night and mostly ignore each other (a secondary source).
- Game Developer, "Protect the moron", https://www.gamedeveloper.com/design/protect-the-moron- ; PC Gamer forum
  and Hitman forum threads on tailing missions : the complaints are instant fails, hidden rules, slow pace and
  missions used only for exposition.

**What we do** (`Scripts/World/Fox.cs`)
- The fox is no enemy: the cat follows it out of curiosity, to learn where it goes.
- No fail state: the fox waits (looks back) when the cat is more than 7 m behind; a cat within 1.8 m that does
  not sneak makes it stop and stare; if the cat stays, the fox trots on to its next spot.
- The rule is in the goal text ("stay low, keep a few metres back"); the route is short (about 22 m, 6 stops).

## Round 37 (2026-10-18): lit windows on the block

**Question:** the night block was a set of dark boxes. How do games light a night town cheaply, with lit and
dark windows mixed?

**Sources**
- Fab, "Interior Cubemap System" (UE5), https://www.fab.com/listings/428d45ff-e4e8-456a-84f4-ab7009b9dc7c :
  a setting for the share of lit rooms at night and a warm emissive with some temperature variation.
- Unreal Answers, "Randomize section of material", https://answers.unrealengine.com/questions/180562/view.html :
  per-window variation needs per-pane data (UVs or vertex colours); one material instance varies only per mesh.

**What we do**
- No interior mapping (too costly for the phone). Flat panes on each face of each scenery block, one row per
  3 m floor, chosen lit or dark at build time (35 % lit, fixed seed): two meshes, two draw calls.
- `WindowGlow` sets the emission of the shared lit material from `SkyMood.NightAmount`; by day all panes are
  dark glass, which also breaks up the plain block walls.

## Round 38 (2026-10-18): stars and the moon

**Question:** the night sky was an empty dark blue. How do stylized mobile games draw stars and a moon cheaply?

**Sources**
- Farland Skies - Low Poly (Unity Asset Store), https://assetstore.unity.com/packages/slug/64604 : a single-pass
  skybox shader, three star types, adjustable twinkle, horizon extinction; made for mobile.
- Polyverse Skies, https://assetstore.unity.com/packages/vfx/shaders/polyverse-skies-low-poly-skybox-shaders-104017 :
  three star layers and a moon mesh, mobile-optimised shaders.
- Astral Fjord Skybox, https://codinfinity.itch.io/astral-fjord-skybox : procedural, adjustable twinkle, no textures.

**What we do** (no paid assets): `Art/Shaders/NightSky.shader`
- Stars: 700 billboards 140 m around the camera (inside the far plane), upper sky only (the horizon is in the
  fog), mostly small with a few big, warm or blue ones; each twinkles at its own speed (seed in vertex alpha).
- Moon: one billboard in the night-light direction, a pale disc with a soft halo.
- Additive, no fog, no shadows, one mesh each; strength from `_DrahoNight`, set by SkyMood.

## Round 39 (2026-10-18): chapter 10, the high roofs (first part)

**Question:** how should a rooftop level guide the player over pitched roofs?

**Sources**
- Game Developer, "Traversal level design principles",
  https://www.gamedeveloper.com/design/traversal-level-design-principles : good traversal lets the player move
  through, on top of and under a space; even linear routes feel fresh in 3D.
- 80.lv, "The principles of traversal gameplay", https://80.lv/articles/the-principles-of-traversal-gameplay
- BAFTA Young Game Designers, "Rooftoppers", https://ygd.bafta.org/competition/competition-news/2024/rooftoppers :
  tall landmarks (a tower, a chimney) anchor the player on the roofs.

**What we do**
- The route goes up the light well (chapter 8's drainpipe, now to the gutter), over a flat strip and up the slate.
- Ridges run along the street, so the slope (29°, the cat walks up to 50°) is a clear way up from the flat strip.
- Chimneys on the ridges are the goals: the player sees them from the top of the drainpipe.
- Roof-edge bounds start above the ground (`Edge.y0`), so the cat cannot fall into the street and get stuck.

## Round 40 (2026-10-18): readable roofs

**Sources:** Polycount roof-tile threads (https://polycount.com/discussion/comment/2808527), Kalponic Studio
"100 Stylized Roof Textures" (https://kalponic-studio.itch.io/100-stylized-roof-textures), freestylized.com:
a clean tiling base colour with varied tiles reads best in a stylized look; trims frame the shape.
**What we do:** a generated slate texture (4 rows of 5 per metre, shifted rows, a shadow line at each overlap,
variation per slate), tiled at real scale with the rows parallel to the ridge; a ridge cap and zinc gutters.

## Round 41 (2026-10-18): the storm - rain, lightning, wind gusts

**Questions:** how do games make wind gusts fair? How is rain done cheaply on a phone?

**Sources**
- Game Developer, "Design lesson 101: Frontier's LostWinds",
  https://www.gamedeveloper.com/design/design-lesson-101---frontier-s-em-lostwinds-em- : wind as a core mechanic.
- "Gone with the Wind" (itch.io), https://thejoro.itch.io/gone-with-the-wind : players could not tell when the wind
  acted; the fix is sound and visuals that start before the gust.
- Construct forum, "How do I create horizontal wind physics",
  https://www.construct.net/en/forum/construct-3/how-do-i-8/create-horizontal-wind-137320 : a push hidden in friction
  does not read; make it an explicit velocity term.
- "Gusty Glade" trope, https://tropedia.fandom.com/wiki/Gusty_Glade : gusts at intervals; the player times moves.
- Unity manual, Particle System Renderer (stretched billboards),
  https://docs.unity.cn/cn/current/Manual/PartSysRendererModule.html ; rain write-ups on ArtStation and the Asset
  Store (camera-attached rain, light for mobile).

**What we do**
- Each gust is announced 1.3 s before it pushes: the rain slants, and the HUD howls for the first three gusts.
- `CatMotor.Wind` is an explicit push; a crouched cat feels 15 % of it. Gusts only push on the roofs.
- Rain: at most 1200 stretched soft particles round the camera; lightning: two short cold pulses of light.

## Round 43 (2026-10-18): eyeshine

**Question:** what does a cat's eyeshine look like, and when does it show?

**Sources**
- Wikipedia, "Tapetum lucidum", https://en.wikipedia.org/wiki/Tapetum_lucidum : a reflective layer behind the
  retina; eyeshine is iridescent, its colour varies with the angle and the reflector.
- Sci.News, https://www.sci.news/biology/tapetum-lucidum-10711.html : in cats the reflector is riboflavin.
- Vetstreet, https://www.vetstreet.com/our-pet-experts/why-do-my-cats-eyes-glow-in-the-dark : cats mostly glow
  green to yellow; the glow shows when light comes from near the viewer (a flash, a torch).

**What we do** (`Scripts/Cat/EyeShine.cs`): the camera's fill light acts as the light near the viewer. At night
the Eye material glows green-gold, scaled by how straight the cat faces the camera (dot product to the 6th power).

## Round 44 (2026-10-18): the bakery gull

**Question:** a gull is much bigger than a pigeon: should it flee sooner?

**Sources**
- Mayer et al. (2019), PLoS ONE, https://doaj.org/article/16e500f3e94a456888f3e5a9b7285997 : flight-initiation
  distance is species-specific and can depend on body size; heavier birds take flight at greater distances.
- Universidade Federal de Uberlândia study on flight-initiation distance,
  https://repositorio.ufu.br/bitstream/123456789/27892/1/Dist%c3%a2nciaInicialVoo.pdf : larger solitary birds flee
  sooner; in flocks the body-mass effect was not significant.
- Round 31 sources (direct vs tangential approach).

**What we do:** the gull is a solitary bird of the Pigeon kind in its own group: it flees at 1.8 m from a
walking cat (pigeons 1.3 m), 3.2 m from a running cat (2.6 m) and 0.6 m from a sneaking cat (0.45 m); a direct
approach still multiplies the distance by 1.6, a side approach by 0.6.

## Round 45 (2026-10-18): butterflies on the rooftops

**Question:** how do butterflies fly, and why are they so hard to catch?

**Sources**
- Cornell Chronicle, "Hind wings help butterflies make swift turns to evade predators" (Eisner & Jantzen 2009,
  PNAS), https://news.cornell.edu/node/272576 : without hind wings, turning acceleration roughly halves.
- SICB abstract, "Erratic flight in butterflies" (Horisawa, Byrnes & Dudley), https://sicb.org/?p=32602 :
  palatable butterflies fly erratic, sinuous paths that help them evade aerial predators.
- Journal of Experimental Biology (small tortoiseshells),
  https://cob.silverchair.com/jeb/article-pdf/211/1/24/1262751/24.pdf : after a simulated attack they fly faster
  and straighter than in spontaneous flight.

**What we do** (`Moth.dayFlier`): zigzag flight round the geraniums (speed, turn rate and height change all the
time); a cat walking within 1 m triggers a fast, straight escape up and away; the butterfly comes back after
about 1.6 s. Sneak close, then jump or rear when one flutters low.

## Round 46 (2026-10-18): the fisherman (red light, green light)

**Question:** how does a "freeze when watched" game stay fair?

**Sources**
- Playworks, "Red Light, Green Light", https://www.playworks.org/game-library/red-light-green-light/ : the caller
  turns round on "red"; anyone still moving goes back to the start line.
- University of Twente ProDeo, "Red Light, Green Light", https://www.prodeo.utwente.nl/?p=2966 : variants with a
  clear turn signal and handicaps that balance the game.
- Round 41 notes: announce an event before it acts.

**What we do** (`Fisherman.cs`): he hums for 1.3 s before he turns (a HUD line when the cat is near), looks for
2.2-3 s, then fishes for 3.5-7 s. Moving within 7 m while he looks sends the cat back up the quay (no fail state).
The bucket is the goal; the turn of his upper body shows where he looks.

## Round 47 (2026-10-18): dawn

**Question:** what does the light look like between night and full morning?

**Sources**
- PhotographyTalk, "Golden hour vs blue hour",
  https://www.photographytalk.com/golden-hour-vs-blue-hour-when-to-shoot-what-in-landscape-photography/ : golden
  hour about 2000-3000 K (daylight about 5500 K) with long directional shadows, the sun about 6 degrees up; blue hour
  cool, even and blue (9000-12000 K).
- Swintons Art, "Twilight: the magic of the golden hour and the blue hour",
  https://www.swintonsart.com/post/twilight-the-magic-of-the-golden-hour-and-the-blue-hour

**What we do:** `SkyMood.Dawn`: the sun 6 degrees up in the east, warm gold at 0.85, the ambient and sky still
cool blue-pink (the blue hour fading), pinkish fog; the lamps stay dimly on. The story goes night → dawn (the
humans come home) → day (breakfast).

## Round 48 (2026-10-18): squeaky toys in the neighbour's flat

**Question:** how do stealth games make noise sources readable and fair?

**Sources**
- Wikipedia, "Thief: The Dark Project", https://en.wikipedia.org/wiki/Thief:_The_Dark_Project : soft surfaces are
  quiet, tiles and metal are loud; the player watches both their noise and what they walk on.
- GameSpot review, https://www.gamespot.com/reviews/thief-the-dark-project-review/1900-2533843/ : each surface
  sounds different, so sound itself is feedback.
- critpoints, "Stealth game spotting deconstruction", https://critpoints.net/2015/03/30/stealth-game-spotting-deconstruction/
- Uppsala speldesign student post, https://babel.speldesign.uu.se/?p=3933 : ripples (as in Mark of the Ninja) show
  what made a sound and how far it travels.

**What we do** (`SqueakyToy.cs`): bright rubber toys are the loud "surface" of the flat: easy to see, easy to step
round. A squeak plays, the toy squashes, a ring spreads 3.5 m over the floor and Hrozna wakes; the HUD explains the
first squeak. The noise ring round the cat (round 20s) still shows how loud the cat's own steps are.

## Round 49 (2026-10-18): a shaft of morning sun

**Question:** how do games fake light shafts and dust in a sunbeam cheaply (WebGL, phone)?

**Sources**
- Unity Discussions, "Volumetric lighting for moving lights",
  https://discussions.unity.com/t/volumetric-lighting-for-moving-lights-with-unity-pro-for-webplayer/17173 : the cheap
  way is a transparent mesh matched to the shaft of light.
- McNeel forum, "God rays in render", https://discourse.mcneel.com/t/god-rays-in-render/113151/4 : a gradient on the
  alpha adds depth.
- Volumetric Light Beam (mobile, WebGL),
  https://discussions.unity.com/t/volumetric-light-beam-volumetric-lighting-solution-compatible-with-mobile-vr-and-webgl/679333
  and Volumetric Light Mesh (https://assetstore.unity.com/packages/3d/environments/volumetric-light-mesh-224485) :
  beams that fade with distance and near the camera.
- FakeLight (itch.io), https://realanthonycolmenero.itch.io/fakelight : cone meshes with a semi-transparent shader,
  cheap enough for mobile.

**What we do** (`SunBeam.cs`): the four sides of the shaft from the sliding-door glass to where the light meets the
floor or the far wall, rebuilt from the real sun direction; additive, warm, bright at the glass and 15 % at the far
end; 60 dust motes drift in the room while the sun shines in. In this block only the low morning sun comes in
through that door, so the shaft belongs to the dawn of chapter 11.

## Round 50 (2026-10-19): the camera in tight places

**Question:** the camera only pulled in when a wall was behind the cat; in the light well and under roof edges it
crowded the cat. What do good third-person cameras do?

**Sources**
- Unity Cinemachine Collider (2.x) and Deoccluder (3.x) docs,
  https://docs.unity3d.com/Packages/com.unity.cinemachine@2.7/manual/CinemachineCollider.html and
  https://docs.unity3d.com/Packages/com.unity.cinemachine@3.0/manual/CinemachineDeoccluder.html : strategies "pull
  camera forward", "preserve camera height", "preserve camera distance" (find another viewpoint); "minimum occlusion
  time" ignores brief occluders (Unity forum: https://discussions.unity.com/t/pull-camera-forward-and-player-occlusion/761274).
- StraySpark, "Third person and deocclusion" (Godot), https://www.strayspark.studio/docs/godot-camera-system/third-person-and-deocclusion :
  occlusion hysteresis, asymmetric damping, pitch-coupled distance.
- John Nesky, "50 Camera Mistakes", GDC 2014, https://gdcvault.com/play/1020460/50-Camera : breaking line of sight is a
  failure case.

**What we do** (`OrbitCamera`): keep "pull forward" (instant in, slow out), and add "preserve distance": after 0.3 s
blocked below 60 % of the wanted distance, probe 7 nearby viewpoints and turn towards the freest at 70°/s, preferring
small changes. Off while climbing; the player's swipe always wins.

## Round 51 (2026-10-19): real-size cobbles, old plaster

**Question:** the court's round stones read tiny and white. How big are Flemish cobbles ("kasseien")?

**Sources**
- Opalis, "Kasseien" (reclaimed building materials, Belgium), https://opalis.eu/sites/default/files/2022-01/1.11_nl_-_kasseien_v01_0.pdf :
  mosaic cobbles (cube-like) have sides of 7-12 cm; square setts 13-20 cm.
- Opalis, "Recuperatiekassei", https://opalis.eu/sites/default/files/2022-01/recuperatiekassei_0.pdf : old porphyry
  mosaic cobbles 7/9, 9/10, 9/11 cm.

**What we do:** the pebble texture now repeats every 0.9 m (stones of about 11 cm, at the top of the mosaic range;
the round-11 commit text said "10-14 cm", corrected here), in a darker, warmer stone colour. A generated old-plaster
texture (tileable noise: soft unevenness, faint damp stains, fine grit; one tile = 3 m) on the light-well walls and
on every plaster building.

## Round 52 (2026-10-19): catnip and a scent trail

**Question:** how do cats react to catnip, and how long?

**Sources**
- Espín-Iturbe et al. (2017), via Gwern's catnip archive, https://www.gwern.net/doc/cat/psychology/drug/catnip/2017-espiniturbe.pdf :
  60 domestic cats; about a fifth showed active responses (rolling) and most a passive one; other studies put the
  share of responders at about two thirds; the response is inherited and kittens do not react.
- Hatch (1972), https://www.gwern.net/doc/cat/psychology/drug/catnip/1972-hatch.pdf : the behaviour ends after
  about ten minutes.
- Todd (1963), https://www.gwern.net/doc/cat/psychology/drug/catnip/1963-todd.pdf : sniffing, head shaking,
  chin and cheek rubbing, rolling.
- RSC Education, nepetalactone resource, https://edu.rsc.org/download?ac=14970

**What we do** (`Catnip.cs`): the grey cat "points" at the herb garden; a trail of faint green wisps leads there
(a new skill: follow a smell). A sniff gives the zoomies: 1.6 × speed for 12 s (a game-sized burst of the real
ten minutes), then 40 s of rest before the bed works again.

## Round 53 (2026-10-19): glitter paths of lamps on the river

**Question:** why do lamps make long streaks on water at night, and how do we fake it cheaply?

**Sources**
- NASA Earth Observatory, "A glittering path of San Francisco sunglint",
  https://earthobservatory.nasa.gov/blogs/earthmatters/2016/11/09/ground-to-space-a-glittering-path-of-san-francisco-sunglint/ :
  a glitter path is many bright points reflected by tiny ripples back to the viewer.
- University of Colorado physics demo, "Rippled reflection",
  https://physicslabs.colorado.edu/demos/optics/geometrical-optics/reflection-from-flat-surfaces/rippled-reflection/ :
  rippled water turns a point light into a vertical pillar; its extent grows with the wave amplitude.
- Atmospheric Optics, "Bent glitter paths", https://atoptics.co.uk/fz684.htm : the width depends on the light's
  altitude and the slopes of the waves.
- Astronomy.com, "The long track of the moon", https://www.astronomy.com/observing/the-long-track-of-the-moon/

**What we do** (`WaterGlint.cs`): lamps across the river from the player (the far bank), each with a flat strip on
the water that points at the camera, up to 13 m long; a dashed texture (ripple facets) scrolls slowly, the width
wobbles a little and the light flickers. Additive, warm, only while the lamp is on.

## Round 54 (2026-10-19): sitting down

**Question:** what does a resting cat look like, so the idle cat looks alive?

**Sources**
- Catster, "Cat tail language" guides, https://www.catster.com/guides/cats-tail-meanings : a tail wrapped round the
  body or the paws is a normal resting posture (the "loaf"); not a mood signal on its own.
- Ohio State University, Indoor Pet Initiative, "Reading cat body language",
  https://indoorpet.osu.edu/sites/indoorpet/files/assets/documents/hospital/indoorcat/Reading%20Cat%20Body%20Language.pdf

**What we do:** a Sit clip (the body 43° nose-up about the hips and sunk 11.5 cm, straight front legs with flat paws
under the shoulders, folded hind legs, the tail along the floor and round the paws, breathing, a slow look round, an
ear twitch). The cat sits down after 4 s standing still and stands up at any move (Animator `IdleTime`).

## Round 55 (2026-10-19): nap spots (a collectible that uses the sit)

**Question:** what makes a collectible worth finding?

**Sources**
- Game Wisdom, "The craft of collectible design in videogames", https://game-wisdom.com/critical/collectible-design-videogames :
  the hunt needs a reason (extrinsic motivation); rewards must keep their value (money loses value late in a game).
- Game Developer, "How to make catchy collectibles", https://www.gamedeveloper.com/design/how-to-make-catchy-collectibles
- TechRaptor, "IMO: the case of collectibles", https://techraptor.net/content/imo-case-collectibles : knowing exactly
  where everything is kills the search.
- Collectathon design notes, https://summerengine.com/templates/platformer/collectathon-platformer/collectathon-platformer :
  items you can see but not yet reach are a promise to come back.

**What we do** (`NapSpot.cs`): the collectible uses the cat's own verb (sit). Spots are soft golden rings in every area,
several on high roofs you can see from below before you can reach them. A found spot gives a few coins (a weak reward);
**to do:** a lasting reward for finding all twelve (for example a new resting pose, or the cat's sun-warmed fur).
