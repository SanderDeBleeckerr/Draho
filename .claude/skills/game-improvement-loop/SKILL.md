---
name: game-improvement-loop
description: Run one or more rounds of the Draho improvement loop - playtest the live web build, research on the web how good games solve the weak points, change graphics / gameplay / story, deploy, test again and log the round. Use when the user asks to keep improving the game, to "start/keep the loop going", or to make the graphics or gameplay better.
---

# Draho improvement loop (with web research)

One round = **observe → research → change → verify → log**. Do one to three rounds per user turn.
Explain to the user in about 80 % ASD-STE100 (short sentences, simple words).

## 1. Observe (the live build)
- Wait for the deploy: `bash Tools/Playtest/wait-deploy.sh <sha> 45` (the page serves `version.txt`).
- Play it headless: `node Tools/Playtest/playtest.cjs "<url>?beat=N&at=x,z,yaw[,y]" <scratchpad>/<dir> <scenario>`
  (`NODE_PATH=$(npm root -g)`; scenarios and test hooks are listed at the top of the script).
- Look at the screenshots and the log (`STORY`, `CAT`, `COINS`, `TOAST`, `PERF`).
- Use the `perf` scenario for render counters (draw calls, batches, triangles) at six places.
- Write down the 2-4 weakest points, on three axes: **graphics**, **gameplay**, **story**.

## 2. Research (before changing anything)
For each weak point, search the web first. Use `WebSearch` (mode `standard`; `extended` only when the
first results are thin), then `WebFetch` on the 1-2 best pages. Good queries name the engine, the
platform and the problem, for example:
- "Unity URP mobile WebGL stylized grass shader performance"
- "third person camera feel small character platformer best practices"
- "game feel jump coyote time buffer squash stretch"
- "stealth game readable detection feedback design"
- "low poly lighting color palette warm afternoon"
Prefer: Unity manual and Unity blog, GDC talks (gdcvault), Game Developer (gamedeveloper.com),
well-known postmortems and tutorials (Catlike Coding, Brackeys, GMTK). Check the date: Unity 6 / URP 17.

Turn each finding into one concrete, small change that fits this project. Write it in
`docs/RESEARCH_NOTES.md` (date, question, sources as links, what we do with it).

## 3. Change (constraints of this project)
- Scenes are built from code: `Draho/Assets/_Draho/Editor/PlaygroundBuilder.cs` and `ApartmentBuilder.cs`,
  level data in `Draho/Assets/_Draho/Levels/Apartment/apartment_layout.json`. No hand-made assets in scenes.
- No Unity editor in the container: read C# changes carefully (CI compiles them).
- Budget for a phone (iPhone, WebGL): about 300 batches and 150 k triangles where the player stands;
  measure with the `perf` scenario before and after a graphics change.
- No downloaded art unless the licence allows it (CC0 / CC BY with credit in the README).
- Privacy: never write real coordinates, addresses or names of places near the owner's home into the repo.
- Keep test hooks working (`SendMessage` names, `link.xml`).

## 4. Verify
- Commit, push to `main` (triggers the web build), wait for the deploy.
- Run the scenarios that cover the change plus one regression scenario (`bounty`, `board`, `climbhold`).
- A change is done only when the playtest shows it works. If not, fix and repeat.

## 5. Log
- Add a row to the round table in `docs/PLAYTEST_LOOP.md` (graphics | gameplay | story).
- Name the research source in the commit message when a change comes from it.
