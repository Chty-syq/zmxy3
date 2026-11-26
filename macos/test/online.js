// An anonymous smoke test of the actual official entry. Only inspect player metadata.
const { setTimeout: wait } = require('node:timers/promises');

async function run(page) {
  const end = Date.now() + 60000;
  let observed = '等待游戏子页面';
  while (Date.now() < end) {
    for (const frame of page.web.mainFrame.framesInSubtree) {
      if (!/^https:\/\/[^/]+\.4399\.com\/4399swf\//.test(frame.url)) continue;
      observed = '已进入官方游戏子页面，等待 SWF';
      try {
        const metadata = await frame.executeJavaScript("document.querySelector('ruffle-embed,ruffle-object')?.ruffle()?.metadata ?? null");
        if (metadata?.width && metadata?.height) {
          // Let the collection page finish its own delayed Flash check. A detached
          // movie that was replaced by the compatibility banner must not pass.
          await wait(1800);
          const visible = await page.web.mainFrame.executeJavaScript("Boolean(document.querySelector('#bestposition #flash23')?.getClientRects().length)");
          if (frame.detached || !visible) continue;
          console.log('PASS 4399 大闹天庭篇 SWF', JSON.stringify(metadata));
          return;
        }
      } catch { /* A redirect can replace this frame. */ }
    }
    await wait(200);
  }
  const frames = page.web.mainFrame.framesInSubtree.map(frame => frame.url.split('?')[0]);
  throw new Error(observed + ' · ' + frames.join(' '));
}
module.exports = { run };
