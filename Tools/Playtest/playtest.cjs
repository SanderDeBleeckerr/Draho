// Plays the live WebGL build in headless Chromium and saves screenshots + the console log.
// Usage: node Tools/Playtest/playtest.cjs [url] [outDir] [tour|perf|board|bounty|rivals|flat|flatloud|wayback|climb|climbhold|grab|roof|pipe|fence|hedgehog|nest|quay|ducks|ledge|roofroute|begwall|begijnhof|greycat|exit|bakery|mouse|garden|birds|court|sneak|sky|moths|fox|home|highroof|storm|homecoming|eyes|gull|butterflies|fisher|squeak|sunbeam|catnip|glint|sit|naps|rested|wet|cones|epilogue]
// Needs the global playwright package (NODE_PATH) and Chromium in /opt/pw-browsers.
const { chromium } = require('playwright');
const fs = require('node:fs');

(async () => {
const url = process.argv[2] || 'https://sanderdebleeckerr.github.io/Draho/';
const out = process.argv[3] || 'playtest-out';
fs.mkdirSync(out, { recursive: true });
const log = [];
const t0 = Date.now();
const stamp = () => ((Date.now() - t0) / 1000).toFixed(1).padStart(6);

const browser = await chromium.launch({
  args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--enable-webgl'],
});
// iPhone 15 in landscape.
const page = await browser.newPage({
    viewport: { width: 852, height: 393 }, deviceScaleFactor: 1, // 1x: software WebGL is slow
    isMobile: true, hasTouch: true,
    userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
  });
page.on('console', m => log.push(`${stamp()} [${m.type()}] ${m.text()}`));
page.on('pageerror', e => log.push(`${stamp()} [pageerror] ${e.message}`));
page.on('requestfailed', r => log.push(`${stamp()} [requestfailed] ${r.url()} ${r.failure()?.errorText}`));

const shot = async name => {
  await page.evaluate(() => window.unityInstance && window.unityInstance.SendMessage('StoryDirector', 'LogState'));
  if ((process.argv[4] || '') === 'perf') await page.evaluate(() => { try { window.unityInstance.SendMessage('PerfProbe', 'Log'); } catch (e) {} });
  await page.screenshot({ path: `${out}/${name}.png` });
  log.push(`${stamp()} [shot] ${name}`);
};
const hold = async (keys, ms) => {
  for (const k of keys) await page.keyboard.down(k);
  await page.waitForTimeout(ms);
  for (const k of keys) await page.keyboard.up(k);
};

await page.goto(url, { waitUntil: 'load', timeout: 120000 });
// Wait until Unity has started (the loader logs this) or 150 s pass.
const started = await page.waitForFunction(
  () => window.unityInstance || document.querySelector('#unity-loading-bar')?.style.display === 'none'
    || document.querySelector('#loading')?.style.display === 'none',
  null, { timeout: 150000 }).then(() => true, () => false);
log.push(`${stamp()} [info] started=${started}`);
await page.waitForTimeout(6000);
await page.mouse.click(426, 196); // focus the canvas
await shot('01_spawn');

// A short touch on the joystick area is a tap (jump, or stand up when the cat stands still).
// Down and up 100 ms apart, like a thumb (one-frame taps can be missed by the game).
const cdp = await page.context().newCDPSession(page);
const tapStick = async () => {
  const point = [{ x: 140, y: 300 }];
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: point });
  await page.waitForTimeout(100);
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  log.push(`${stamp()} [input] tap stick`);
};
const scenario = process.argv[4] || 'tour';
// Steps: [name, action, arg]. 'move' = stick x,y,run,seconds (sent into the game, see TouchCatInput.TestMove);
// 'jump' / 'duck' / 'hold' (seconds) = game hooks; 'tap' = a real touch tap on the joystick; 'wait' = seconds.
const steps = {
  tour: [
    ['02_walk_north', 'move', '0,1,0,2.5'],
    ['03_run_north', 'move', '0,1,1,1.5'],
    ['04_turn_west', 'move', '-1,0,0,1.2'],
    ['05_run_jump_west', 'runjump', '-1,0,1,2'],
    ['06_land', 'wait', '1'],
    ['07_duck', 'duck', ''],
    ['08_sneak', 'move', '0,1,0,1.5'],
    ['09_stand', 'duck', ''],
    ['10_tap_still', 'tap', ''],
    ['11_wait', 'wait', '3'],
  ],
  // Start with ?beat=7&at=-22,0.45,180 : in front of the ivy at the end of the garden, facing it.
  climb: [
    ['02_tap_still', 'tap', ''],
    ['03_rear_climb', 'wait', '0.8'],
    ['04_climb_up', 'move', '0,1,0,2.5'],
    ['05_climb_more', 'move', '0,1,0,2.5'],
    ['06_climb_top', 'move', '0,1,0,2.5'],
    ['07_leap_off', 'jump', ''],
    ['08_landed', 'wait', '1.5'],
  ],
  // Same start as climb, but only the hold gesture: hold the middle of the stick to stand up and climb.
  climbhold: [
    ['02_hold', 'hold', '2'],
    ['03_hold_more', 'hold', '3'],
    ['04_hold_top', 'hold', '3'],
    ['05_let_go', 'wait', '1.5'],
    ['06_leap_off', 'jump', ''],
    ['07_landed', 'wait', '1.5'],
  ],
  // Start with ?beat=7&at=-22,0.45,180 : the whole ivy, up to the flat roof (6.5 m), then a walk on the roof.
  roof: [
    ['02_hold', 'hold', '4'],
    ['03_hold_more', 'hold', '4'],
    ['04_hold_high', 'hold', '4'],
    ['05_pull_up', 'hold', '2'],
    ['06_on_roof', 'wait', '1'],
    ['07_walk_roof', 'move', '0,1,0,3'],
    ['08_look', 'wait', '1'],
  ],
  // Start with ?beat=13&at=-18,7,90 : in the wild garden, the hedgehog ahead. Sit still, then follow it.
  hedgehog: [
    ['02_walk_close', 'move', '0,1,0,3.5'],
    ['03_sit_still', 'wait', '6'],
    ['04_trust', 'wait', '3'],
    ['05_follow', 'move', '0,1,0,4'],
    ['06_follow_more', 'move', '0,1,0,4'],
    ['07_follow_on', 'move', '0,1,0,4'],
    ['08_follow_end', 'move', '0.3,1,0,3'],
    ['09_wait', 'wait', '6'],
  ],
  // Start with ?beat=18&at=-34.6,-20,270 : at the quay edge, facing the water; sit still until a duck comes.
  ducks: [
    ['02_sit', 'wait', '6'],
    ['03_ducks_come', 'wait', '8'],
    ['04_run', 'move', '0,-1,1,1.5'],
    ['05_ducks_flee', 'wait', '3'],
  ],
  // Start with ?beat=20&at=-27.5,-20,180,6 : on the riverside roofs; run south along them and drop into the begijnhof.
  roofroute: [
    ['02_run', 'move', '0,1,1,10'],
    ['03_run', 'move', '0,1,1,10'],
    ['04_run', 'move', '0,1,1,10'],
    ['05_run', 'move', '0,1,1,12'],
    ['05b_over', 'move', '0,1,1,2'],
    ['06_inside', 'wait', '2'],
  ],
  // Start with ?beat=23&at=52.6,-205.6,180 : in front of the grey cat on the church step; walk slowly to her.
  greycat: [
    ['02_walk', 'move', '0,1,0,4'],
    ['03_welcome', 'wait', '2'],
  ],
  // Start with ?beat=25&at=-30.5,-114.85,0 : inside the begijnhof, at the garden shed; climb it, jump onto the wall, out to the quay.
  exit: [
    ['02_climb_shed', 'hold', '4.5'],
    ['03_on_shed', 'wait', '1'],
    ['04_jump_wall', 'movejump', '0,1,0,5,4.3'],
    ['05_on_wall', 'wait', '1'],
    ['06_walk_west', 'move', '-1,0,0,4'],
    ['07_drop_out', 'move', '0,1,0,1.5'],
    ['08_quay', 'wait', '1'],
  ],
  // Start with ?beat=25&at=-33,-93,270 : on the quay at the footbridge; cross it, find the bakery, hunt its mouse.
  bakery: [
    ['02_cross', 'move', '0,1,1,11'],
    ['03_to_shop', 'move', '-1,0,0,9'],
    ['04_look', 'wait', '3'],
    ['05_duck', 'duck', ''],
    ['06_sneak', 'move', '0,1,0,3'],
    ['07_wait', 'wait', '4'],
    ['08_sneak', 'move', '0.5,1,0,3'],
    ['09_wait', 'wait', '3'],
  ],
  // Start with ?beat=17&at=-25,-12,270,6.5 : on the riverhouse roof; run west, drop onto the quay shed and the quay.
  quay: [
    ['02_run_west', 'move', '0,1,1,2'],
    ['03_drop', 'move', '0,1,1,1.5'],
    ['04_off_shed', 'move', '0,1,1,1.5'],
    ['05_quay', 'wait', '1.5'],
  ],
  // Start with ?beat=42&at=-31,7.5,270 : on the river bank at night, looking across the water: the far-bank lamps and
  // their glitter paths on the river; then turn a little.
  glint: [
    ['02_look', 'wait', '3'],
    ['03_turn', 'move', '-0.4,1,0,0.4'],
    ['04_settle', 'wait', '2'],
  ],
  // Start with ?beat=6&at=2.2,2.6,0&fresh=1 : on the rug nap spot; sit still until it is found; then the garden table spot.
  naps: [
    ['02_ring', 'wait', '1'],
    ['03_sit_on_it', 'wait', '6'],
    ['04_table', 'call', 'StoryDirector.TestTeleport:-1.4,2.6,90,0.8'],
    ['05_sit_table', 'wait', '6'],
  ],
  // Start with ?beat=53&at=3.4,3.4,60 : THE END; after 14 s the free-roam epilogue (day, the board works again).
  epilogue: [
    ['02_the_end', 'wait', '3'],
    ['03_epilogue', 'wait', '14'],
    ['04_day', 'wait', '6'],
  ],
  // Start with ?beat=42&at=-31,7.5,270 (the far-bank lamps) or ?beat=38&at=-2.2,1.2,90 (the garden lamp) at night:
  // soft cones of lamplight; then rain: the cones grow stronger.
  cones: [
    ['02_look_up', 'call', 'Main Camera.TestView:-5,1.4'],
    ['02_cones', 'wait', '3'],
    ['03_rain', 'call', 'Rain.TestStorm'],
    ['04_cones_rain', 'wait', '6'],
  ],
  // Start with ?beat=48&at=14.4,4.6,180,11.9 : on the ridge in the storm; dry roofs, then soaked at once (wet look).
  wet: [
    ['02_dry', 'call', 'WetWeather.TestDry'],
    ['03_dry_look', 'wait', '1.5'],
    ['04_soak', 'call', 'WetWeather.TestWet'],
    ['05_wet_look', 'wait', '1.5'],
    ['06_wet_walk', 'move', '0,1,0,2'],
    ['07_wet_turn', 'move', '1,0,0,1.5'],
  ],
  // Start with ?beat=38&at=-8,2.5,180 : night on the lawn; all nap spots done: a warm glow in the fur, higher jumps.
  rested: [
    ['02_before', 'wait', '1.5'],
    ['03_jump_before', 'movejump', '0,1,1,1.2,0.5'],
    ['04_rested', 'call', 'WellRested.TestComplete'],
    ['05_glow', 'wait', '1.5'],
    ['06_jump_after', 'movejump', '0,1,1,1.2,0.5'],
    ['07_glow_walk', 'move', '1,0,0,1.5'],
  ],
  // Start with ?beat=6&at=-8,2.5,180 : on the lawn by day; stand still: after 4 s the cat sits down; walk on.
  sit: [
    ['02_standing', 'wait', '2'],
    ['03_sits', 'wait', '4'],
    ['04_side', 'call', 'StoryDirector.TestTeleport:-8,2.5,90'],
    ['05_sits_side', 'wait', '5.5'],
    ['06_walks_on', 'move', '0,1,0,1'],
  ],
  // Start with ?beat=24&at=52.6,-210.6,0 : at the grey cat's church step; follow the scent trail to the catnip (zoomies),
  // then run a little to see the speed.
  catnip: [
    ['02_trail', 'wait', '2'],
    ['03_to_bed', 'call', 'StoryDirector.TestTeleport:59.6,-216.4,135'],
    ['04_sniff', 'move', '0,1,0,0.8'],
    ['05_zoomies', 'move', '0,1,1,2.5'],
    ['06_next', 'wait', '2'],
  ],
  // Start with ?beat=20&at=-34.78,-36,180 : on the quay; run along the narrow edge to the begijnhof wall.
  ledge: [
    ['02_run', 'move', '0,1,1,8'],
    ['03_ledge', 'move', '0,1,1,8'],
    ['04_more', 'move', '0,1,1,8'],
    ['05_wall', 'move', '0,1,1,9'],
  ],
  // Start with ?beat=21&at=-33,-111.55,180 : at the ivy on the begijnhof wall; climb over and drop in.
  begwall: [
    ['02_hold', 'hold', '3'],
    ['03_hold', 'hold', '3'],
    ['04_on_wall', 'wait', '1'],
    ['05_drop_in', 'move', '0,1,0,1.5'],
    ['06_inside', 'wait', '1.5'],
  ],
  // Start with ?beat=22&at=21.6,-223.5,180 : by a real linden in the begijnhof; then (teleport) the church door.
  begijnhof: [
    ['02_to_linden', 'move', '0,1,0,3'],
    ['03_sit', 'wait', '2'],
    ['04_church', 'call', 'StoryDirector.TestTeleport:50.1,-211.2,0'],
    ['05_door', 'wait', '3'],
    ['06_look_up', 'move', '0,-1,0,1'],
  ],
  // Start with ?beat=15&at=-16.5,7,90 : the hedgehog trusts the cat at once (test hook); follow it to its nest.
  nest: [
    ['02_trust', 'call', 'Hedgehog.TestTrust'],
    ['03_follow', 'move', '0,1,0,4'],
    ['04_follow', 'move', '0,1,0,4'],
    ['05_follow', 'move', '0,1,0,4'],
    ['06_follow', 'move', '0.3,1,0,3'],
    ['07_wait', 'wait', '5'],
  ],
  // Start with ?beat=12&at=-19.5,4.4,0 : in front of the loose plank in the fence; walk through it.
  fence: [
    ['02_through', 'move', '0,1,0,2.5'],
    ['03_wild', 'wait', '1'],
  ],
  // Start with ?beat=10&at=-7,-4.15,180,5.8 : on the lower roof, in front of the drainpipe.
  pipe: [
    ['02_hold', 'hold', '3'],
    ['03_hold_more', 'hold', '3'],
    ['04_on_top', 'wait', '1.5'],
    ['05_walk', 'move', '0,1,0,2'],
  ],
  // Start with ?beat=6&at=-10,2.5,270 : in the garden; Fatsocat comes (test hook): stand your ground. Then nap with Zlatenka.
  rivals: [
    ['02_fatsocat', 'call', 'Fatsocat.TestArrive'],
    ['03_he_comes', 'wait', '10'],
    ['04_stand', 'wait', '8'],
    ['05_gone', 'wait', '3'],
    ['06_to_zlatenka', 'call', 'StoryDirector.TestTeleport:-11.4,3.2,90'],
    ['07_duck', 'duck', ''],
    ['08_nap', 'wait', '7'],
  ],
  // Start with ?beat=32 : performance stations (teleport, settle, log fps and render counters).
  perf: [
    ['02_apartment', 'call', 'StoryDirector.TestTeleport:2,2.5,0'], ['02b', 'wait', '6'],
    ['03_garden', 'call', 'StoryDirector.TestTeleport:-3,2.5,270'], ['03b', 'wait', '6'],
    ['04_roofs', 'call', 'StoryDirector.TestTeleport:-18,-10,200,6.5'], ['04b', 'wait', '6'],
    ['05_quay', 'call', 'StoryDirector.TestTeleport:-33,-50,180'], ['05b', 'wait', '6'],
    ['06_begijnhof', 'call', 'StoryDirector.TestTeleport:60,-235,180'], ['06b', 'wait', '6'],
    ['07_far_bank', 'call', 'StoryDirector.TestTeleport:-53,-60,180'], ['07b', 'wait', '6'],
  ],
  // Start with ?beat=2&at=5.9,1.05,90 : the kitchen mouse comes out; the test hook catches it; then read the board.
  bounty: [
    ['02_mouse_out', 'wait', '4'],
    ['03_catch', 'call', 'Mouse.TestCatch'],
    ['04_paid', 'wait', '3'],
    ['05_to_board', 'call', 'StoryDirector.TestTeleport:7.0,2.0,90'],
    ['06_board', 'wait', '2'],
  ],
  // Start with ?beat=30&at=-4,7.1,90 : in the wild garden before the neighbour's window; jump in, sneak to the toy.
  flat: [
    ['02_jump_in', 'movejump', '0,1,0,5,3.4'],
    ['03_inside', 'wait', '1'],
    ['04_near_basket', 'call', 'StoryDirector.TestTeleport:4.6,5.6,90'],
    ['05_duck', 'duck', ''],
    ['06_sneak', 'move', '0,1,0,6'],
    ['07_got_it', 'wait', '2'],
    ['08_sneak_out', 'move', '0,-1,0,6'],
  ],
  // Start with ?beat=33&at=3.8,4.05,90 : in the living room by the court door, facing the gap. Walk into the court,
  // walk straight at the pigeons (they fly), sneak, then (test hook) touch one and climb the court pipe to the jingle ball.
  court: [
    ['02_squeeze_in', 'move', '0,1,0,2.5'],
    ['03_in_court', 'wait', '1'],
    ['04_walk_at_them', 'move', '0.3,1,0,1.5'],
    ['05_flock_up', 'wait', '2'],
    ['06_duck', 'duck', ''],
    ['07_wait_down', 'wait', '12'],
    ['08_sneak_side', 'move', '1,0,0,2'],
    ['09_touch', 'call', 'Pigeon_1.TestTouch'],
    ['10_at_pipe', 'call', 'StoryDirector.TestTeleport:7.3,4.15,90'],
    ['11_stand', 'duck', ''],
    ['12_climb', 'hold', '4'],
    ['13_climb_more', 'hold', '4'],
    ['14_top', 'hold', '2'],
    ['15_down', 'jump', ''],
    ['16_landed', 'wait', '2'],
  ],
  // Start with ?beat=52&at=2.2,2.4,270 : breakfast time at dawn, in the living room facing the sliding door: the
  // morning sun shines in (a shaft of light, dust motes). Turn round to see it from the side.
  sunbeam: [
    ['02_look', 'wait', '2'],
    ['03_side', 'move', '0,-1,0,0.6'],
    ['04_settle', 'wait', '2'],
  ],
  // Start with ?beat=31&at=-0.6,6.6,90 : inside the neighbour's flat by the window; walk onto the rubber duck: it
  // squeaks (ring, toast) and Hrozna wakes and chases the cat out.
  squeak: [
    ['02_walk_onto_duck', 'move', '0,1,0,2.2'],
    ['03_squeak', 'wait', '0.6'],
    ['04_chase', 'wait', '3'],
    ['05_out', 'wait', '2'],
  ],
  // Start with ?beat=19&at=-33.3,-22,180 : on the quay north of the fisherman; walk down while he fishes, freeze
  // when he hums (one shoo on purpose: keep walking while he looks), then reach the bucket.
  fisher: [
    ['02_walk', 'move', '0,1,0,3'],
    ['03_hum', 'call', 'Fisherman.TestLook'],
    ['04_keep_walking', 'move', '0,1,0,3'],
    ['05_shooed', 'wait', '2'],
    ['06_hum_again', 'call', 'Fisherman.TestLook'],
    ['07_freeze', 'wait', '4.5'],
    ['08_to_bucket', 'call', 'StoryDirector.TestTeleport:-33.6,-26.6,180'],
    ['09_grab', 'move', '0,1,0,0.6'],
    ['10_next', 'wait', '3'],
  ],
  // Start with ?beat=9&at=-15.5,-5,90,6.6 : on the big gravel roof by the geraniums; walk at the butterflies (they
  // escape), sneak in, rear up / jump at a low one, then a test catch so the beat finishes.
  butterflies: [
    ['02_walk_at_them', 'move', '0,1,0,1.5'],
    ['03_escape', 'wait', '2'],
    ['04_duck', 'duck', ''],
    ['05_wait', 'wait', '4'],
    ['06_sneak', 'move', '0,1,0,2'],
    ['07_dive', 'call', 'Butterfly_White.TestDive'],
    ['08_jump', 'jump', ''],
    ['09_catch', 'call', 'Butterfly_Admiral.TestCatch'],
    ['10_next', 'wait', '3'],
  ],
  // Start with ?beat=27&at=-54.5,-99,270 : on the far bank by the bakery; walk at the gull (it flees), then sneak in
  // from the side and touch it, eat the croissant it drops.
  gull: [
    ['02_walk_at_it', 'move', '0,1,0,2'],
    ['03_flies', 'wait', '10'],
    ['04_back_down', 'wait', '6'],
    ['05_duck', 'duck', ''],
    ['06_touch', 'call', 'Gull.TestTouch'],
    ['07_stand', 'duck', ''],
    ['08_crumbs', 'call', 'StoryDirector.TestTeleport:-57.1,-99,270'],
    ['09_eaten', 'wait', '3'],
  ],
  // Start with ?beat=38&at=-8,2.5,270 : night on the lawn; turn to face the camera: the eyes should glow (eyeshine).
  eyes: [
    ['02_turn', 'move', '0,-1,0,0.5'],
    ['03_facing', 'wait', '0.2'],
    ['04_away', 'move', '0,1,0,0.8'],
  ],
  // Start with ?beat=49&at=3.2,6.5,270,8.9 : on the flat roof by the hatch after the storm; slip in, dry off on the
  // cat tree, morning, greet the humans at the front door, the end.
  homecoming: [
    ['02_to_hatch', 'move', '0,1,0,1'],
    ['03_fade', 'wait', '3'],
    ['04_on_tree', 'call', 'StoryDirector.TestTeleport:4.1,4.35,0,0.8'],
    ['05_morning', 'wait', '5'],
    ['06_to_door', 'call', 'StoryDirector.TestTeleport:6.4,1.05,90'],
    ['07_rub', 'move', '0,1,0,0.7'],
    ['08_purr', 'wait', '4'],
    ['09_to_bowl', 'call', 'StoryDirector.TestTeleport:4.7,1.0,90'],
    ['10_eat', 'move', '0,1,0,1.2'],
    ['11_end', 'wait', '4'],
  ],
  // Start with ?beat=48&at=14.4,4.6,180,11.9 : on the ridge by the front chimney in the storm; walk north along the
  // ridge, standing through one gust and ducking through the next, then to the neighbour's chimney.
  storm: [
    ['02_rain', 'wait', '3'],
    ['03_gust_warn', 'call', 'Rain.TestGust'],
    ['04_gust_standing', 'wait', '2.5'],
    ['05_back_up', 'call', 'StoryDirector.TestTeleport:14.4,8.0,0,11.9'],
    ['06_duck', 'duck', ''],
    ['07_gust_warn2', 'call', 'Rain.TestGust'],
    ['08_gust_ducked', 'wait', '2.5'],
    ['09_stand', 'duck', ''],
    ['10_walk_ridge', 'move', '0,1,0,4'],
    ['11_chimney', 'call', 'StoryDirector.TestTeleport:14.4,11.9,0,11.9'],
    ['12_shelter', 'wait', '3'],
  ],
  // Start with ?beat=45&at=7.3,4.15,90 : at the court drainpipe; climb to the roof, walk up the slate to the
  // chimney on the ridge (a teleport at the end in case the walk misses it).
  highroof: [
    ['02_climb', 'hold', '5'],
    ['03_climb_more', 'hold', '5'],
    ['04_climb_top', 'hold', '5'],
    ['05_on_roof', 'hold', '2'],
    ['06_up_the_slate', 'move', '0,1,0,6'],
    ['07_to_chimney', 'move', '-1,0,0,2.5'],
    ['08_look', 'wait', '2'],
    ['09_ridge', 'call', 'StoryDirector.TestTeleport:14.4,5.4,0,11.9'],
    ['10_view', 'wait', '3'],
  ],
  // Start with ?beat=41&at=-24.9,6.3,270 : at the fox's den; through the broken fence to the river bank,
  // up the ivy on the end shed, over its roof and down into the own garden, home to the lamp.
  home: [
    ['02_to_gap', 'move', '0,1,0,3'],
    ['03_bank', 'wait', '2'],
    ['04_look_lamp', 'move', '-1,0,0,1.2'],
    ['05_at_ivy', 'call', 'StoryDirector.TestTeleport:-27,5.45,180'],
    ['06_climb', 'hold', '4'],
    ['07_climb_more', 'hold', '4'],
    ['08_roof', 'hold', '2'],
    ['09_roof_middle', 'call', 'StoryDirector.TestTeleport:-25.2,2.5,90,4.25'],
    ['09_walk_east', 'move', '0,1,0,3'],
    ['10_down', 'wait', '2'],
    ['11_to_lamp', 'call', 'StoryDirector.TestTeleport:-1.3,1.4,90'],
    ['12_home', 'wait', '3'],
  ],
  // Start with ?beat=40&at=-2.6,7.4,270 : in the wild garden behind the fox's start; follow it west, sneaking
  // first, then walking (it should stop and stare), then let it go to its den (test hook).
  fox: [
    ['02_fox_starts', 'wait', '2'],
    ['03_duck', 'duck', ''],
    ['04_sneak', 'move', '0,1,0,5'],
    ['05_stand', 'duck', ''],
    ['06_walk', 'move', '0,1,0,4'],
    ['07_walk_more', 'move', '0,1,1,3'],
    ['08_look', 'wait', '3'],
    ['09_walk_on', 'move', '0,1,0,4'],
    ['10_to_den', 'call', 'Fox.TestToDen'],
    ['11_den', 'wait', '5'],
  ],
  // Start with ?beat=38&at=-0.9,2.0,180,0.8 : on the garden table at night, facing the lamp. Real jumps at
  // diving moths first, then test catches so the beat can finish.
  moths: [
    ['02_night_lamp', 'wait', '2'],
    ['03_dive', 'call', 'Moth_1.TestDive'],
    ['04_jump', 'jump', ''],
    ['05_dive2', 'call', 'Moth_2.TestDive'],
    ['06_jump2', 'jump', ''],
    ['07_dive3', 'call', 'Moth_3.TestDive'],
    ['08_jump3', 'jump', ''],
    ['09_catch_a', 'call', 'Moth_4.TestCatch'],
    ['10_catch_b', 'call', 'Moth_1.TestCatch'],
    ['11_catch_c', 'call', 'Moth_2.TestCatch'],
    ['12_done', 'wait', '3'],
  ],
  // Start with ?beat=6&at=-10,2.5,0 (open lawn): day, evening and night light (SkyMood test hook).
  sky: [
    ['02_day', 'move', '0,1,0,1'],
    ['03_evening', 'call', 'SkyMood.TestMood:Evening'],
    ['04_evening_turn', 'move', '-1,0,0,1'],
    ['05_night', 'call', 'SkyMood.TestMood:Night'],
    ['06_night_walk', 'move', '0,1,0,1.5'],
    ['07_night_turn', 'move', '1,0,0,1.5'],
    ['08_night_still', 'wait', '1'],
  ],
  // Start with ?beat=6&at=-10,2.5,0 (open lawn). Duck and crawl to the right; the shots come while the cat still moves (crawl animation check).
  sneak: [
    ['02_duck', 'duck', ''],
    ['03_crawl', 'call', 'PlayerInput.TestMove:1,0,0,6'],
    ['04_crawl', 'wait', '0.3'],
    ['05_crawl', 'wait', '0.3'],
    ['06_crawl', 'wait', '0.3'],
    ['07_crawl', 'wait', '1.5'],
    ['08_still', 'wait', '4'],
    ['09_stand', 'duck', ''],
  ],
  // Start with ?beat=31&at=0,6.8,90 : inside the flat; run at the basket: Hrozna wakes and throws the cat out.
  flatloud: [
    ['02_run', 'move', '0,1,1,3'],
    ['03_barking', 'wait', '3'],
    ['04_out', 'wait', '3'],
  ],
  // Start with ?beat=29&at=-33.85,-9.5,90 : on the quay, facing the shed pipe; climb back up to the riverhouse roof.
  wayback: [
    ['02_climb_shed', 'hold', '6.5'],
    ['03_on_shed', 'wait', '1'],
    ['04_walk_to_pipe', 'move', '0,1,0,4'],
    ['05_climb_house', 'hold', '7'],
    ['06_on_roof', 'wait', '1.5'],
  ],
  // Start with ?beat=4&at=6.4,2,90 : in the kitchen at the bounty board; then (teleport) pick up two feathers.
  board: [
    ['02_walk_to_board', 'move', '0,1,0,1.5'],
    ['03_read_board', 'wait', '2'],
    ['04_feather', 'call', 'StoryDirector.TestTeleport:-1.4,2.3,0,0.77'],
    ['05_pick', 'move', '0,1,0,1.5'],
    ['06_feather2', 'call', 'StoryDirector.TestTeleport:-19,3.0,0'],
    ['07_pick', 'move', '0,1,0,2'],
    ['08_wait', 'wait', '7'],
    ['09_reload', 'reload', 'https://sanderdebleeckerr.github.io/Draho/'],
  ],
  // Start with ?beat=7&at=-22,0.9,180 : close to the ivy; jump at it, hang on, climb a bit, tap to drop off.
  grab: [
    ['02_jump_at_ivy', 'movejump', '0,1,0,1.2,0.2'],
    ['03_hanging', 'wait', '1.5'],
    ['04_climb_up', 'move', '0,1,0,2'],
    ['05_drop_off', 'jump', ''],
    ['06_landed', 'wait', '1.5'],
  ],
  // Start with ?beat=6&at=-22,3.9,0 : in front of the maple trunk (use with climbhold).
  // Start with ?beat=4&at=-0.5,1.2,270 : on the terrace by the door, looking down the garden.
  garden: [
    ['02_walk', 'move', '0,1,0,3'],
    ['03_run', 'move', '0,1,1,3'],
    ['04_run_more', 'move', '0,1,1,3'],
    ['05_look_back', 'move', '0,-1,0,1'],
  ],
  // Start with ?beat=5&at=-3,3.3,270 : on the terrace edge, a blackbird about 3.5 m ahead.
  birds: [
    ['02_look', 'wait', '1.5'],
    ['03_duck', 'duck', ''],
    ['04_sneak', 'move', '0,1,0,3'],
    ['05_sneak_more', 'move', '0,1,0,3'],
    ['06_sneak_close', 'move', '0,1,0,3.5'],
    ['07_stand', 'duck', ''],
    ['08_run_at_birds', 'move', '0,1,1,1.5'],
    ['09_birds_up', 'wait', '1.5'],
    ['10_wait', 'wait', '4'],
  ],
  // Start with ?beat=2&at=5.9,1.05,90 : in the kitchen, facing the mouse hole by the fridge.
  mouse: [
    ['02_watch', 'wait', '2.5'],
    ['03_duck', 'duck', ''],
    ['04_sneak', 'move', '0,1,0,2.5'],
    ['05_sneak_more', 'move', '0,1,0,2.5'],
    ['06_stand', 'duck', ''],
    ['07_run', 'move', '0,1,1,1'],
    ['08_wait', 'wait', '4'],
  ],
}[scenario];
const send = (method, arg) => page.evaluate(([m, a]) => window.unityInstance.SendMessage('PlayerInput', m, a), [method, arg]);
for (const [name, action, arg] of steps) {
  if (action === 'tap') await tapStick();
  else if (action === 'jump') await send('TestJump', '');
  else if (action === 'duck') await send('TestCrouch', '');
  else if (action === 'call') {
    const [target, value] = arg.split(':'); const [o, m] = target.split('.');
    await page.evaluate(([o, m, v]) => v === undefined ? window.unityInstance.SendMessage(o, m) : window.unityInstance.SendMessage(o, m, v), [o, m, value]);
  }
  else if (action === 'movejump') { // 'x,y,run,seconds,jumpAfter': push the stick, jump while it is still pushed
    const v = arg.split(','); await send('TestMove', v.slice(0, 4).join(',')); await page.waitForTimeout(+v[4] * 1000);
    await send('TestJump', ''); await page.waitForTimeout(Math.max(0, (+v[3] - +v[4])) * 1000);
  }
  else if (action === 'reload') { // the same browser (same save data), the game URL without test arguments
    await page.goto(arg, { waitUntil: 'load', timeout: 120000 });
    await page.waitForFunction(() => window.unityInstance, null, { timeout: 150000 }).catch(() => {});
    await page.waitForTimeout(8000);
  }
  else if (action === 'hold') { await send('TestHold', arg); await page.waitForTimeout(+arg * 1000); }
  else if (action === 'runjump') { await send('TestMove', arg); await page.waitForTimeout(1200); await send('TestJump', ''); await page.waitForTimeout(800); }
  else if (action === 'move') { await send('TestMove', arg); await page.waitForTimeout(+arg.split(',')[3] * 1000); }
  else if (action === 'wait') await page.waitForTimeout(+arg * 1000);
  log.push(`${stamp()} [input] ${action} ${arg}`);
  await page.waitForTimeout(400);
  await shot(name);
}
await page.waitForTimeout(1500);
await shot('99_end');

fs.writeFileSync(`${out}/console.log`, log.join('\n') + '\n');
await browser.close();
console.log(log.filter(l => /STORY|STATE|CAT|SAVE|COINS|COLLECT|TOAST|PERF|shot|input|exception/i.test(l)).join('\n'));
})();
