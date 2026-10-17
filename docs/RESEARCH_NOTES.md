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
