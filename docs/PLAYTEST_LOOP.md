# Playtest loop

Claude improves the game in rounds. Each round has the same steps:

1. **Build.** Push to `main`. The workflow *Web build* builds WebGL and publishes it on
   GitHub Pages (6–10 minutes). The build writes the commit to `version.txt`.
2. **Wait.** `Tools/Playtest/wait-deploy.sh <sha>` waits until Pages serves that commit.
3. **Play.** `NODE_PATH=$(npm root -g) node Tools/Playtest/playtest.cjs <url> <out-dir>` opens the
   game in headless Chromium as an iPhone in landscape, plays a short input script (walk, run, jump,
   duck, wait at the door) and saves screenshots and the browser console (`console.log`).
   - Third argument: scenario `tour` (default), `climb` (use with `?beat=6&at=-3.75,3.62`) or `mouse`
     (use with `?beat=2&at=5.9,1.05,90`).
   - `?beat=N` starts the story at beat N. `?at=x,z[,yaw]` puts the cat at a point of the blueprint (yaw: 0 = north, 90 = east).
   - Lines that start with `STORY` in the console show the story progress.
4. **Review.** Look at the screenshots and the log. Write down problems on three axes:
   **graphics**, **gameplay** and **story**.
5. **Change.** Fix the most important item on each axis. Then go back to step 1.

## Round log

| Round | Graphics | Gameplay | Story |
|---|---|---|---|
| 1 | Soft shadows (2048, 2 cascades), post-processing (tone mapping, bloom, vignette), 2 warm lamps, full-screen web page with a loading screen, render at max 2× pixel ratio | A mouse with hearing (run = loud, duck = quiet), 4 mouse holes, catch by touch | Chapter 1 "The scratching sound": 6 beats with a story line and a goal at the top of the screen |
| 2 | Rear and Climb clips (paws flat on the wall), garden shed and tree | Tap the stick to jump; a jump keeps the speed of the last 0.45 s; standing still, the cat rears up; rearing in front of a climbable starts a climb, with a pull-up onto the shed roof; JUMP button removed | New beat: climb the shed roof |
