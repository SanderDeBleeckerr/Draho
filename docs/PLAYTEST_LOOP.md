# Playtest loop

Claude improves the game in rounds. Each round has the same steps:

1. **Build.** Push to `main`. The workflow *Web build* builds WebGL and publishes it on
   GitHub Pages (6–10 minutes). The build writes the commit to `version.txt`.
2. **Wait.** `Tools/Playtest/wait-deploy.sh <sha>` waits until Pages serves that commit.
3. **Play.** `NODE_PATH=$(npm root -g) node Tools/Playtest/playtest.cjs <url> <out-dir>` opens the
   game in headless Chromium as an iPhone in landscape, plays a short input script (walk, run, jump,
   duck, wait at the door) and saves screenshots and the browser console (`console.log`).
   - `?beat=N` starts the story at beat N. `?at=x,z` puts the cat at a point of the blueprint.
   - Lines that start with `STORY` in the console show the story progress.
4. **Review.** Look at the screenshots and the log. Write down problems on three axes:
   **graphics**, **gameplay** and **story**.
5. **Change.** Fix the most important item on each axis. Then go back to step 1.

## Round log

| Round | Graphics | Gameplay | Story |
|---|---|---|---|
| 1 | Soft shadows (2048, 2 cascades), post-processing (tone mapping, bloom, vignette), 2 warm lamps, full-screen web page with a loading screen, render at max 2× pixel ratio | A mouse with hearing (run = loud, duck = quiet), 4 mouse holes, catch by touch | Chapter 1 "The scratching sound": 6 beats with a story line and a goal at the top of the screen |
