// This file and the pinned Ruffle distribution share a private closure.
// Only Ruffle's WASM clock imports use this mapping. Page JS and input timers use real time.
let realAnchor = performance.now();
let gameAnchor = realAnchor;
let speed = 1;
let volume = 1;

function gameTime(realTime) {
  return gameAnchor + (realTime - realAnchor) * speed;
}

function applyVolume(element) {
  if (typeof element.ruffle === 'function') element.ruffle().volume = volume;
  else if (element instanceof HTMLMediaElement) element.volume = volume;
}

document.addEventListener('zmxy-player-settings', event => {
  const settings = JSON.parse(event.detail);
  if (![1, 2, 3, 100].includes(settings.speed)) return;
  if (!Number.isFinite(settings.volume) || settings.volume < 0 || settings.volume > 1) return;
  const now = performance.now();
  gameAnchor = gameTime(now);
  realAnchor = now;
  speed = settings.speed;
  volume = settings.volume;
  document.querySelectorAll('ruffle-embed, ruffle-object, ruffle-player, audio, video').forEach(applyVolume);
});
document.addEventListener('loadedmetadata', event => {
  const element = event.target;
  // Ruffle may hold its core lock while it delivers this notification.
  queueMicrotask(() => applyVolume(element));
}, true);
window.RufflePlayer = window.RufflePlayer || {};
window.RufflePlayer.config = {
  ...window.RufflePlayer.config,
  publicPath: 'zmxy-player://assets/',
  autoplay: 'on',
  unmuteOverlay: 'hidden'
};

// The collection page tests a separate, tiny SWF for about one second. On a cold
// WASM compile its own fallback can replace the game just before that SWF is ready.
// Retry the normal entry once, with a warm player, when that real self-test succeeds.
if (window === window.top && location.hostname === 'www.4399.com' && location.pathname === '/flash/zmhj.htm') {
  function checkOfficialPlayer() {
    const deadline = performance.now() + 20000;
    function check() {
      if (window.flashenabled || performance.now() >= deadline) return;
      const hint = document.querySelector('#bestposition iframe[src^="/loadimg/blockflashtip_"]');
      const probe = document.getElementById('testplayer1');
      if (hint && typeof probe?.checkflash === 'function') {
        try {
          // 4399 tests for a successful call; the callback has no required return value.
          probe.checkflash();
          document.dispatchEvent(new Event('zmxy-player-warm'));
          return;
        } catch { /* Wait for the test movie's first frame. */ }
      }
      setTimeout(check, 200);
    }
    check();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', checkOfficialPlayer, { once:true });
  else checkOfficialPlayer();
}
