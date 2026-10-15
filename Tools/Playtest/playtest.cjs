// Plays the live WebGL build in headless Chromium and saves screenshots + the console log.
// Usage: node Tools/Playtest/playtest.cjs [url] [outDir]
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
    viewport: { width: 852, height: 393 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true,
    userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
  });
page.on('console', m => log.push(`${stamp()} [${m.type()}] ${m.text()}`));
page.on('pageerror', e => log.push(`${stamp()} [pageerror] ${e.message}`));
page.on('requestfailed', r => log.push(`${stamp()} [requestfailed] ${r.url()} ${r.failure()?.errorText}`));

const shot = async name => { await page.screenshot({ path: `${out}/${name}.png` }); log.push(`${stamp()} [shot] ${name}`); };
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

const script = [
  ['02_walk_north', ['KeyW'], 2500],
  ['03_run_north', ['ShiftLeft', 'KeyW'], 1500],
  ['04_turn_west', ['KeyA'], 1500],
  ['05_run_west', ['ShiftLeft', 'KeyW'], 2500],
  ['06_jump', ['Space'], 120],
  ['07_crouch', ['KeyC'], 120],
  ['08_crouch_walk', ['KeyW'], 1500],
];
for (const [name, keys, ms] of script) {
  await hold(keys, ms);
  await page.waitForTimeout(name === '06_jump' ? 250 : 400);
  await shot(name);
}
await page.keyboard.press('KeyC');
await page.waitForTimeout(3000);
await shot('09_wait_at_door');

fs.writeFileSync(`${out}/console.log`, log.join('\n') + '\n');
await browser.close();
console.log(log.filter(l => /error|warn|info\]|exception/i.test(l)).slice(-40).join('\n'));
})();
