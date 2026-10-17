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
