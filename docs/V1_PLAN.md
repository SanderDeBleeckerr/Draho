# Draho: Version 1 Technical Plan

**Goal:** a black cat walks around one realistic room on an iPhone 11.
**Time:** 2–4 weeks.

> **Change (Oct 2026):** we start with a **playground**: one 20 × 20 m floor in empty space.
> The cat falls when it walks off the edge, and it comes back at the start after a 10 m fall.
> The cat is our own **placeholder model** that a Blender script makes
> (`Tools/Blender/build_drahomil.py`). We buy assets later. The code does not depend on
> the model, so we only swap the model.

## 1. Definition of done

- [ ] The cat walks, runs and idles, and the animations blend smoothly by speed.
- [ ] A floating joystick on the left side controls the cat.
- [ ] A swipe on the right side orbits the camera.
- [ ] A third-person camera follows the cat and does not go through walls.
- [ ] The cat collides with the walls and the furniture in one room. He does not fall through the floor.
- [ ] Paw-step sounds play in sync with the walk cycle. A random meow or purr plays when the cat is idle.
- [ ] The game runs at a stable **30 fps** (target 60) on an iPhone 11.
- [ ] The game opens directly into the room in landscape.

**Not in scope:** jumping, physics props, mice, menus, saving, music, IAP and localization.

## 2. Tech stack

| Area | Choice | Why |
|---|---|---|
| Engine | **Unity 6.3 LTS** | Unity supports it until December 2027. |
| Render pipeline | **URP** (Universal Render Pipeline), mobile renderer | It is the only good choice for realistic graphics on mobile. |
| Input | **Input System** package + `OnScreenStick` | This is the modern input system and it has touch support built in. |
| Camera | Own `OrbitCamera` (about 80 lines, sphere-cast collision) | No extra package. Easy to tune for a small cat. |
| Character movement | `CharacterController` (kinematic) | It is predictable and cheap. Rigidbody movement is not necessary. |
| Animation | Animator with a 1D blend tree on `Speed` and no root motion | Easy to tune. The code controls the speed. |
| Audio | Unity Audio and animation events for paw steps | Simple. We can change to FMOD later if necessary. |
| Version control | Git + **Git LFS** for binaries, `.gitignore` for Unity | Standard. |

## 3. Architecture

The code is small, but it is ready for the bounty-hunter future:

```
ICatInput  ──►  CatMotor  ──►  CharacterController
(touch / AI /    (speed, turn,    
 replay)          gravity)       
                     │
                     ▼
               CatAnimator  ──►  Animator (Speed, Grounded)
                     │
                     ▼
               CatAudio  ◄── animation events (footsteps)
```

- **`ICatInput`** gives a move vector, the run state and, later, actions.
  `TouchCatInput` reads the on-screen stick. Later, cutscenes or tests can use
  their own implementation.
- **`CatMotor`** moves the cat relative to the camera. It contains
  acceleration, turn speed and gravity, and it has no input code.
- **`CatStats` (ScriptableObject)** holds the walk speed, run speed, turn rate
  and acceleration. You can tune them without code changes.
- **`CatAnimator`** sends the motor state to the Animator parameters.
- **`CatAudio`** plays paw steps (surface-aware later) and idle sounds.
- **Scenes:** `Boot` (services, settings) loads `Apartment_Room01` additively.
  Each future location is its own scene.

## 4. Project structure

```
Draho/                      ← git root
├── docs/
├── Tools/Blender/          ← script that builds the placeholder cat
├── Draho/                  ← Unity project
│   ├── Assets/
│   │   ├── _Draho/         ← our own content (underscore keeps it at the top)
│   │   │   ├── Art/ (Materials, Shaders)
│   │   │   ├── Audio/
│   │   │   ├── Prefabs/ (Cat, Camera, UI)
│   │   │   ├── Scenes/ (Boot, Apartment_Room01)
│   │   │   ├── Editor/ (PlaygroundBuilder, DrahomilModelPostprocessor)
│   │   │   ├── Scripts/
│   │   │   │   ├── Cat/ (CatMotor, CatAnimator, CatStats)
│   │   │   │   ├── Controls/ (ICatInput, TouchCatInput)
│   │   │   │   ├── CameraRig/ (OrbitCamera)
│   │   │   │   ├── UI/ (VirtualJoystick, TouchLookArea)
│   │   │   │   └── Core/ (FallRespawn, GameBootstrap)
│   │   │   └── Settings/ (URP assets, Input Actions)
│   │   └── ThirdParty/     ← bought assets, not changed by us
│   ├── Packages/
│   └── ProjectSettings/
├── .gitignore
└── .gitattributes          ← LFS rules
```

> ⚠️ **Asset licenses:** the licenses of the Unity Asset Store and of most
> marketplaces do not allow you to redistribute assets. If this GitHub repository
> is public, do not commit `Assets/ThirdParty/` (keep it in `.gitignore`), or
> make the repository private.

## 5. Performance budget (iPhone 11 / A13)

| Item | Budget |
|---|---|
| Frame rate | 60 fps target, stable 30 fps minimum |
| Triangles on screen | ≤ 300k |
| Draw calls (SRP batches) | ≤ 150 |
| Cat mesh | ≤ 20k triangles, 1 material, textures ≤ 2048 |
| Textures | ASTC compression. Room textures ≤ 2048, most ≤ 1024. |
| Lighting | **Baked** lightmaps + light probes. One real-time directional light only for the cat's shadow (or a blob shadow). |
| Post-processing | Tonemapping, a light bloom and color grading. No SSAO, no screen-space reflections. |
| Render scale | 0.8–1.0. Adjust after profiling. |

### Black fur recipe
- Albedo near `#1E1E22`, not `#000000`, so the shape stays visible.
- A Fresnel rim light in a custom Shader Graph, or a back light.
- A soft specular for the fur sheen. Normal map with fur strands.
- No real-time shell fur in version 1, because it is too expensive on the A13.

## 6. Week-by-week plan

### Week 1: project foundation and movement
- [ ] Create a Unity 6.3 LTS project with URP. Switch the platform to iOS. Lock the orientation to landscape.
- [ ] Set up Git LFS, `.gitignore` and `.gitattributes`.
- [x] Install the packages: URP, Input System, uGUI (`Packages/manifest.json`).
- [x] Placeholder cat from Blender: mesh, 25-bone rig, Idle/Walk/Run (`Tools/Blender/build_drahomil.py`).
- [x] Playground: a floor in empty space, fall and respawn (`FallRespawn`).
- [x] Write `ICatInput`, `TouchCatInput` (floating stick) and `CatMotor` + `CatStats`.
- [x] Orbit camera with touch swipe and collision (`OrbitCamera`).
- [x] Mouse and keyboard as an extra input in the editor.
- [ ] Open in Unity 6.3, run **Draho → Build Playground Scene**, test, commit the generated files.

### Week 2: assets
- [ ] Buy and import the cat model and the apartment asset (see [ASSETS.md](ASSETS.md)).
- [ ] Set the scale: the cat is about 25 cm high and 45 cm long. Unity units are meters.
- [ ] Make the Animator controller: a blend tree with idle, walk and run, plus `CatAnimator`.
- [ ] Build one room from the apartment kit. Add simple colliders (boxes, not mesh colliders).
- [ ] Make the black-fur material and the rim light.

### Week 3: lighting, audio, performance
- [ ] Bake the lighting and add light probes. Set the URP mobile quality.
- [ ] Add `CatAudio` with footstep animation events and idle meow and purr.
- [ ] Profile in the editor with the Frame Debugger and the Profiler.
- [ ] Check the triangle count and the draw calls against the budget.

### Week 4: device and polish
- [ ] Do the first iOS build (see §7).
- [ ] Profile on the iPhone 11. Fix the hot spots.
- [ ] Tune the joystick feel, the camera distance and the speeds.
- [ ] Record a short gameplay clip. Plan v0.2.

## 7. Building for iPhone (decision open)

Unity can develop on Windows, macOS or Linux. Only the final iOS build needs
**Xcode on a Mac**. You can choose one of these options:

1. **Xcode on your girlfriend's Mac** (free). Check her macOS version: Xcode 16+
   needs macOS 14.5+. It also needs about 40 GB of free space.
2. **Unity Build Automation** (cloud). Pay per build minute. It still needs an
   Apple Developer account for signing.
3. **A rented cloud Mac** (MacinCloud, AWS EC2 Mac).

**Apple accounts:**
- With a free Apple ID, you can install on your own iPhone. The app expires after 7 days.
- An Apple Developer Program account costs $99 per year. It is necessary for TestFlight and the App Store.

## 8. Risks

| Risk | Mitigation |
|---|---|
| The bought cat and room look mismatched (scale or style) | Check the screenshots and the scale before you buy. Test with a free model first. |
| Realistic assets are too heavy for the A13 | Keep the budget above. Bake the lighting. Use LODs. Profile on the device early (week 3–4). |
| The black cat is not visible on dark floors | Use the rim light, a lifted albedo, and a light floor in the first room. |
| There is no Mac access when you need it | Decide on §7 before the end of week 3. |
| Asset licenses in a public repository | Keep `ThirdParty/` out of git, or make the repository private. |
