# Day/night cycle: what exists, and a plan

Written after the 30-round graphics review (docs/GRAPHICS_REVIEW.md).

## What exists today

- **SkyMood** (`Scripts/World/SkyMood.cs`) has five preset moods: Day, Evening, Night, Storm and Dawn.
  - `SkyMood.Set(mood, seconds)` blends the whole look from one preset to the next. That covers the sun's direction
    (a low sun for Evening, a "moon" position for Night), the sun, sky, equator and ground colours, fog, exposure,
    sky tint, colour grade, clouds and the cat's fill light.
  - `SkyMood.NightAmount` (0 day, 0.5 evening, 1 night) is the switch that about 18 systems listen to: `IndoorLight`
    (lamps, bulbs, sink spots), `NightLamp` (street, quay and garden lamps), `NightWindows`, `WindowGlow`, `TvGlow`,
    moths, glow-worms, shooting stars, swifts, chimney smoke, eyeshine, the soundscape and the clouds.
- **The story sets the time.** Chapter 8 ends in Evening (`StoryDirector`), chapter 9 is Night, the homecoming goes
  Night, then Dawn, then Day, and the storm sets Storm. Between those beats, time stands still.
- **DayCycle** (`Scripts/World/DayCycle.cs`) runs only in the free-roam epilogue: a 16.5 minute day (Day 8 min,
  Evening 2, Night 5, Dawn 1.5) that jumps between the presets with 20 to 40 s blends.

## What is missing

1. **No sun path.** A blend slerps the sun between two fixed directions. There is no arc across the sky, and the
   shadows do not sweep round the garden over a day.
2. **No clock.** Nothing knows "19:30". Moods are states, so nothing can be "a little later in the evening", and
   nothing can happen at a given hour.
3. **Time stands still during the story.** Each chapter keeps its mood until a beat changes it.
4. **Weak spots at night** (from the review):
   - the dining corner is almost dark;
   - the bakery window is dark (it should be a warm, lit display);
   - lamps glow only at run time.

## Proposal (recommended)

**A continuous clock with the presets as keyframes.**

- `TimeOfDay.Hours` (0 to 24) drives `SkyMood`. The presets sit at hours (Dawn 6:30, Day 12:00, Evening 19:30,
  Night 23:00), and the look between them is interpolated by the clock rather than by a fixed blend timer.
- **The sun follows an arc.** Its azimuth and elevation come from the hour: up in the east, high at noon, down in the
  west. At night the moon takes over (it is already the "sun" light at night). `NightAmount` follows the sun's
  elevation, so the lamps come on at dusk by themselves.
- **The story keeps control.** A beat sets a target hour instead of a mood: `TimeOfDay.GoTo(19.5f, seconds)`, and
  `SkyMood.Set(mood)` maps onto it, so the existing beats keep working. Within a chapter the clock can creep forward
  slowly, so a long chapter goes from afternoon into early evening, never past the next story beat.
- **DayCycle** becomes `TimeOfDay` running freely in the epilogue: the same day length, with a sun that moves.
- **Night pass:** a warm pendant over the dining bar, a lit bakery display (an interior light and an emissive
  shelf), and a check that every lamp and window comes on at dusk.

## Decisions for the owner

1. **Where does time run?** Only in the epilogue (as now, but with a moving sun), or also during the story (slowly,
   within each chapter)?
2. **How long is a day?** About 16 minutes (as now), 24 minutes, or tied to the real clock?
3. **The sun:** a real arc (the shadows move), or keep the fixed preset angles?

## Decisions (owner, after round 30)

1. **Where does time run:** everywhere, freely.
2. **Day length:** about 24 minutes.
3. **The sun:** "make a smart choice given that we bake the textures." Nothing in the game bakes light: the scenes
   are built in code, there are no lightmaps or light probes, the sun is a real-time light with soft shadows, and the
   ambient light is set by SkyMood. The palette textures and Blender models carry colour only, never light. So a
   **real arc costs nothing extra** (the shadow map is redrawn every frame anyway), and the moon needs no second bake
   either: the same light takes the moon's own arc at night, cooler, dimmer and with softer contrast.

## Implemented (step 1: the clock)

- `SkyMood.Hours` (starts at 09:00) runs at 24 game hours per `SkyMood.DayMinutes` = 24 real minutes.
  `SkyMood.ClockRunning` stops it (the captures do).
- **Presets as keyframes:** Night until 05:00, Dawn 06:30, Day 09:00 to 17:00, Evening 19:30, Night from 21:30.
  The whole preset (colours, fog, exposure, sky tint, grade, clouds, `NightAmount`) is interpolated by the hour, so
  the lamps, windows, moths, glow-worms and stars follow the clock with no changes of their own.
- **Sun arc:** sunrise 05:30 to sunset 21:00, up to 52 degrees at midday, from the old dawn bearing round to the old
  evening bearing.
- **Moon arc:** moonrise 19:30 to moonset 06:30, 10 to 40 degrees up.
- **Hand-over:** one directional light changes from sun to moon as the sun goes below about 6 degrees. Its
  intensity dips while it swings, so the turn of the shadows is not seen.
- **Story:** `SkyMood.Set(mood, s)` time-lapses the clock forward to that mood's hour (Dawn 06:30, Day 10:00,
  Evening 19:30, Night 22:30) over `s` seconds, and does nothing if it is already that time of day. Every existing
  beat works unchanged. The storm is weather blended over the clock; the light does not swing during it.
- `SkyMood.Current` is derived from the hour (or Storm). `DayCycle` now only says "Evening comes..." and the other
  lines in the epilogue.
- **Tests:** `SendMessage('SkyMood', 'TestHour', 19.75)`; ArtCapture's `-captureHours 6,13,20.5
  -captureHourViews garden,town_west`.

Verified in play mode (a probe, not committed):
- 10 s moved the clock by 0.17 h.
- `Set(Night, 2)` from 09:13 ran through Evening (18:18) and Night (20:30) to 22:30, with the moonlight at 0.32.
- A second `Set(Night)` did not jump.
- The storm faded the light to 0.16 without moving it.
- `Set(Dawn)` ran forward to 06:30, with a low warm sun at 11 degrees.

## Implemented (step 2: the night pass, saving the clock)

- **Dining pendant:** `lamp_dining_lamp` (Blender, 848 tris). A teal cone shade with a brass rim and a cream inside,
  hung low (light at 1.95 m, 1.1, range 2.6, #FFCB8E) over the dining bar. It is a layout lamp, so `IndoorLight` turns
  it on at dusk with the others.
- **Per-object light limit 4 to 8** (`PlaygroundBuilder`, URP Forward). The flat's big wall and floor meshes are
  reached by more than four extra lights (window, court, the sink spots, three lamps, the cat's fill), so the dimmest
  dropped out. That is why the dining corner was dark at night even before the pendant: its lamp lost to the
  others. 8 is URP Forward's maximum, and the lights are small and range-culled.
- **Bakery display:** an emissive back panel behind the loaves and a warm point light inside under the transom
  (outside, it glared on the glass). Both are driven by `NightLamp`: faint by day, as a shop's lights are, and bright
  at night.
- **Captures:** ArtCapture runs `NightLamp` per view, so the evening and night pictures show the street lamps and the
  display lit.
- **The clock is saved:** `Progress` stores `hours` with every save and restores it on load (`SkyMood.SetClock`).
  Saves from before have -1 and start at 09:00.

## Next steps

- Check the story chapters that are set by day now that time runs (the sunbeam on the floor, the shadows in the
  court).
