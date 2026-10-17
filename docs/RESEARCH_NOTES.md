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
