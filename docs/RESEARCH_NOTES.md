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
