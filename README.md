# Draho

A mobile iPhone game about **Drahomil**, a black cat and mouse bounty hunter.
The game uses Unity.

![Drahomil, low-poly](docs/images/lowpoly_cat_sheet.png)

- [Game Design Document](docs/GDD.md)
- [Version 1 Technical Plan](docs/V1_PLAN.md)
- [Assets](docs/ASSETS.md): Sketchfab low-poly cat (CC BY) and Kenney Furniture Kit (CC0); see [CREDITS.md](CREDITS.md)
- [Level 1: the apartment](docs/APARTMENT.md): blueprint and layout file
- [Working from a phone](docs/PHONE_WORKFLOW.md): GitHub builds the game, you play it in Safari
- [Wiki](wiki/README.md): the public wiki about the game and its characters (published with GitBook Git Sync, see `.gitbook.yaml`)
- [Funding page](https://sanderdebleeckerr.github.io/Draho/funding/): funding strategy and milestones (source in [`site/funding/`](site/funding/index.html); the game stays at the site root)

## Current state: the apartment

The game opens in **Drahomil's apartment** (kitchen, living room, inner court, garden). Drahomil can
jump onto furniture and duck. Wait in front of the sliding garden door and it opens.
The level is built from the blueprint file `Draho/Assets/_Draho/Levels/Apartment/apartment_layout.json`
with Kenney furniture. The old test playground (a floor in empty space) is the second scene.

## Open the project

1. Clone the repository. (Git LFS is prepared in `.gitattributes`, but it is off for now.)
2. Install **Unity 6000.3.26f1** (6.3 LTS) with **iOS Build Support** from Unity Hub.
3. In Unity Hub, click **Add → Add project from disk** and select the `Draho/` folder in this repository.
4. Unity asks *"enable the new Input System backends?"*. Click **Yes**. The editor restarts.
5. In the menu bar, click **Draho → Build Scenes**. The script creates the URP settings, the
   materials, the animator, the cat prefab, `Assets/_Draho/Scenes/Apartment.unity` and `Playground.unity`.
6. Press **Play**.
7. Commit the files that Unity made: `ProjectSettings/`, `Packages/packages-lock.json`,
   all `.meta` files, and the generated assets.

| Control | Editor | iPhone |
|---|---|---|
| Move | WASD / arrow keys | Left thumb (floating joystick) |
| Run | Hold Shift | Push the joystick to the edge |
| Jump (keeps the running speed) | Space | **Tap the stick** (a short touch, no push) |
| Stand up on the hind legs (when standing still) | Space | **Tap the stick** |
| Climb (in front of the ivy at the end of the garden) | Space, then W / S | **Hold the middle of the stick** to stand up and climb (or tap, then push up / down); tap to leap off |
| Duck / sneak (on/off) | C or Ctrl | **DUCK** button (bottom right) |
| Orbit the camera | Drag with the right mouse button | Swipe with the right thumb |

## Rebuild the cat model

The cat is the Sketchfab model ["Lowpoly Cat Rig + Run Animation"](https://sketchfab.com/3d-models/lowpoly-cat-rig-run-animation-c36df576c9ae4ed28e89069b1a2f427a)
by Daily Lowpoly (CC BY 4.0). [`Tools/Blender/convert_lowpoly_cat.py`](Tools/Blender/convert_lowpoly_cat.py) cleans it up,
adds Idle, Walk, CrouchIdle, CrouchWalk, Jump, Fall, Rear and Climb clips and a low-poly face, and exports `Drahomil.fbx`:

```bash
pip install bpy            # Blender as a Python module (Python 3.13 for bpy 5.x)
python Tools/Blender/convert_lowpoly_cat.py -- --glb ThirdParty/Sketchfab/lowpoly-cat/model.glb \
  --out Draho/Assets/_Draho/Art/Cat/Drahomil.fbx --renders /tmp/cat_renders
```

The script prints the measured clip speeds. Put them in `CatStats.cs` (`walkClipSpeed`, `runClipSpeed`, `crouchClipSpeed`)
so the paws do not slide.

The garden door comes from [`Tools/Blender/convert_sliding_door.py`](Tools/Blender/convert_sliding_door.py)
(Sketchfab model by faiyaz5yaz, CC BY 4.0).

The first placeholder cat (made fully by code) is still in [`Tools/Blender/build_drahomil.py`](Tools/Blender/build_drahomil.py).
