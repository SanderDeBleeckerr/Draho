# Playtest loop

Claude improves the game in rounds. Each round has the same steps:

1. **Build.** Push to `main`. The workflow *Web build* builds WebGL and publishes it on
   GitHub Pages (6–10 minutes). The build writes the commit to `version.txt`.
2. **Wait.** `Tools/Playtest/wait-deploy.sh <sha>` waits until Pages serves that commit.
3. **Play.** `NODE_PATH=$(npm root -g) node Tools/Playtest/playtest.cjs <url> <out-dir>` opens the
   game in headless Chromium as an iPhone in landscape, plays a short input script (walk, run, jump,
   duck, wait at the door) and saves screenshots and the browser console (`console.log`).
   - Third argument: scenario `tour` (default), `climb` / `climbhold` (ivy: `?beat=7&at=-22,0.45,180`; maple: `?beat=6&at=-22,3.9,0`), `garden` (`?beat=4&at=-0.5,1.2,270`),
     `birds` (`?beat=5&at=-3,3.3,270`) or `mouse` (use with `?beat=2&at=5.9,1.05,90`).
   - `?beat=N` starts the story at beat N. `?at=x,z[,yaw]` puts the cat at a point of the blueprint (yaw: 0 = north, 90 = east).
   - The steps send input into the game (`TouchCatInput.TestMove/TestJump/TestCrouch` through
     `unityInstance.SendMessage`), so they do not depend on browser focus or the slow software frame
     rate. One step is a real touch tap on the joystick.
   - Console lines: `STORY` = story beat, `CAT` = jump / rear / climb / pull up,
     `STATE` = position, mode, speed, mouse and fps at each screenshot.
4. **Review.** Look at the screenshots and the log. Write down problems on three axes:
   **graphics**, **gameplay** and **story**.
5. **Change.** Fix the most important item on each axis. Then go back to step 1.

## Round log

| Round | Graphics | Gameplay | Story |
|---|---|---|---|
| 1 | Soft shadows (2048, 2 cascades), post-processing (tone mapping, bloom, vignette), 2 warm lamps, full-screen web page with a loading screen, render at max 2× pixel ratio | A mouse with hearing (run = loud, duck = quiet), 4 mouse holes, catch by touch | Chapter 1 "The scratching sound": 6 beats with a story line and a goal at the top of the screen |
| 2 | Rear and Climb clips (paws flat on the wall), garden shed and tree | Tap the stick to jump; a jump keeps the speed of the last 0.45 s; standing still, the cat rears up; rearing in front of a climbable starts a climb, with a pull-up onto the shed roof; JUMP button removed | New beat: climb the shed roof |
| 3 | — | Camera follows a teleport or respawn | Larger story text |
| 4 | — | Tap also counts within 2 frames (slow devices); fix: cat faced backwards after the new clips | — |
| 5 | — | Playtest input sent into the game (reliable); climb + pull-up verified | — |
| 6 | Afternoon sun from the south-west, procedural gradient sky | Mouse first appears at the fridge hole | Roof line matches the scene |
| 7 | Real garden: 24 m strip, terrace + lawn, post-and-plank fence, building wall, picnic table, firewood, ivy | Ivy is the climbing object (rays at two heights: ivy does not reach the ground); firewood to jump on | Climb beat moves to the ivy |
| 8 | Calmer lawn texture | Cat keeps a small gap to the climbing surface (it no longer pushes itself under the ivy); verified: rear → climb to 2.96 m → hold → leap off → land | — |
| 9 | Ivy as one mesh of ~1600 small flat leaves (was big spheres); brick texture on the building wall; camera moves back and looks level while the cat climbs | Blackbirds on the lawn: hop, peck, hear the cat (run 4 m, walk 2.2 m, sneak 0.7 m), fly to a fence post or the wall top, come back far from the cat | New beat: sneak up on a blackbird; end text waits until the cat is at the top of the ivy |
| 10 | Climb camera closer (1.7 m), darker young ivy leaves | **Hold the middle of the stick to stand up and climb** (owner's request); birds: stalk radius 1.3 m, no false scare when the cat stands up | Climb goal explains the hold |
| 11 | Stone path (30 cm slabs, 1 m from the building wall), maple tree with branches and a wide crown (owner's description) | Maple trunk climbable to 1.9 m; blackbirds perch in the maple | — |
| 12 | — | Round 11 test: the cat did not start to climb the maple (trunk 0.31 m away, reach 0.30 m, one thin ray). Now a sphere probe and a 0.3 m reach beyond the body | — |
| 13 | Camera closer and lower (0.6 m, 10° instead of 1.3 m, 18°; climbing 0.95 m) so Drahomil fills more of the screen (owner's request); darker stone path | Maple climb verified (to 1.6 m, hold, leap off) | — |
| 14 | Soft fill light on the camera (1.6 m range): the black cat is readable on the dark ivy and in shade | A blackbird waits in the maple for the cat | New beat 6: climb the maple after the blackbird; then the ivy (beat 7), end (beat 8) |
| 15 | Rooftops from the Flemish height model: brick blocks, gravel roofs, stone coping, chimney | The ivy goes up to the 6.5 m roof (pull-up); drainpipe to the 8.6 m block; climbing 0.6 m/s; invisible walls at the roof edges | Chapter 2 "The Rooftops": 3 new beats; plan to 4 hours in docs/CHAPTERS.md |
| 16 | The rest of the block as scenery (36 buildings from GRB footprints and DHM heights, no colliders), ground under the block, finer roof gravel | Verified: ivy to the roof + pull-up (chapter 2 starts), drainpipe to 8.6 m + pull-up | — |
| 17 | The neighbour's wild garden: tall-grass mesh, brambles, rusty wheelbarrow, fallen chair, barrel; workshop and annex buildings | Loose plank (a hole in the fence), a hedgehog (curls up when the cat moves near it, trusts a still cat, leads it to its nest); drainpipe back up from the low roof | Chapter 3 "Over the Fence": 5 new beats |
| 18 | — | Hedgehog leads at 0.35 m/s; verified: fence hole → chapter 3, hedgehog curls, trusts, leads to its nest; drainpipe back up from the low roof | Shorter story lines (no overflow); the nest beat ends within 2.5 m |
| 19 | River (water surface), stone quays, riverside rows, a house on the water with a narrow edge, begijnhof (white wall, pitched-roof houses with doors and windows, church, pollarded lindens) | Roofs continue west to the river; drop via a shed to the quay; the 45 cm edge; falling in the river puts the cat back on the quay; ivy over the begijnhof wall | Chapters 4 "The Riverside" and 5 "The Begijnhof": 6 new beats |
| 20 | The begijnhof in its real shape (127 houses, church, wall, 38 lindens, listed trees; static batching), mallards on the river | Ducks: scared by a running cat, curious when it sits still at the edge; riverside roofs are one level (6 m) for the rooftop route | New beat: sit still until a duck comes close; begijnhof goals use the real lindens and church door |
| 21 | Darker river with moving ripples; footbridge; bakery shopfront (awning, bread, crates, bin) on the far bank; church bells (sound made in code) | Grey begijnhof cat (watches you, turns away from a running cat, welcomes a calm one); a climbable garden shed as the way out of the begijnhof; far bank playable; bakery mouse | Begijnhof cat beat; chapter 6 "The Bakery" (3 beats) |
| 22 | The garden ivy reaches the ground | A jump against a climbable surface grabs it: the cat hangs on, climbs on pushing up, drops off on a tap | — |
| 23 | Cork bounty board in the kitchen; 24 floating feathers; coins and feathers on screen | Save data (story beat, position, coins, feathers, bounties) in the browser, ?fresh=1 for a new game; coins for beats, mice and feathers; mice come back warier after a catch; mouse territories at the workshop wall and in the begijnhof | The kitchen beat points to the bounty board |
