// Plays the live WebGL build in headless Chromium and saves screenshots + the console log.
// Usage: node Tools/Playtest/playtest.cjs [url] [outDir] [tour|climb|climbhold|mouse|garden|birds]
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
console.log(log.filter(l => /STORY|STATE|CAT|shot|input|exception/i.test(l)).join('\n'));
})();
