# Playtest loop

Claude improves the game in rounds. Each round has the same steps:

1. **Build.** Push to `main`. The workflow *Web build* builds WebGL and publishes it on
   GitHub Pages (6–10 minutes). The build writes the commit to `version.txt`.
2. **Wait.** `Tools/Playtest/wait-deploy.sh <sha>` waits until Pages serves that commit.
3. **Play.** `NODE_PATH=$(npm root -g) node Tools/Playtest/playtest.cjs <url> <out-dir>` opens the
   game in headless Chromium as an iPhone in landscape, plays a short input script (walk, run, jump,
   duck, wait at the door) and saves screenshots and the browser console (`console.log`).
   - Third argument: scenario `tour` (default), `climb` (use with `?beat=6&at=-22,0.45,180`), `garden` (`?beat=4&at=-0.5,1.2,270`),
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
