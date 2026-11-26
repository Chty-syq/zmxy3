const fs = require('node:fs/promises');
const path = require('node:path');
const { performance } = require('node:perf_hooks');
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

async function schedule(page, win, root) {
  await delay(20000);
  const samples = [];
  const started = performance.now();
  try {
    while (!page.web.isDestroyed() && performance.now() - started < 15000) {
      const time = performance.now();
      await page.web.executeJavaScript('void 0');
      samples.push({ elapsedMs: time - started, rendererRoundTripMs: performance.now() - time, foreground: win.isFocused() });
      await delay(100);
    }
    const directory = path.join(root, 'performance');
    await fs.mkdir(directory, { recursive: true });
    await fs.writeFile(path.join(directory, 'page-' + page.id + '-' + Date.now() + '.json'), JSON.stringify(samples));
  } catch (error) { console.error('性能采样中断', error.message); }
}
module.exports = { schedule };
