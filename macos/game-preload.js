// Sandboxed, isolated preload. Remote documents receive the player, never an IPC bridge.
const { ipcRenderer } = require('electron');
const http = location.protocol === 'https:' || location.protocol === 'http:';
const gameSite = http && /(^|\.)4399\.(com|cn)$/.test(location.hostname);
const fixture = process.argv.includes('--zmxy-test-page') && location.protocol === 'zmxy-test:';

if (gameSite || fixture) {
  if (process.isMainFrame) document.addEventListener('zmxy-player-warm', () => ipcRenderer.send('game:retry-flash'));
  let latest;
  let ready = false;
  function update(settings) {
    if (latest && latest.revision > settings.revision) return;
    latest = settings;
    if (ready) document.dispatchEvent(new CustomEvent('zmxy-player-settings', { detail: JSON.stringify(latest) }));
  }
  ipcRenderer.on('game:settings', (_event, settings) => update(settings));
  ipcRenderer.invoke('game:settings').then(update).catch(() => {});
  function install() {
    if (!document.documentElement) return false;
    const script = document.createElement('script');
    script.src = 'zmxy-player://assets/ruffle.js';
    script.onload = () => {
      ready = true;
      if (latest) update(latest);
    };
    script.onerror = () => ipcRenderer.send('game:player-error');
    document.documentElement.appendChild(script);
    return true;
  }
  if (!install()) {
    const observer = new MutationObserver(() => { if (install()) observer.disconnect(); });
    observer.observe(document, { childList: true });
  }
}

// Normal links leave the workbench. Login forms, JS buttons and redirects stay in the page.
function handleLink(event) {
  if (!event.isTrusted || event.defaultPrevented || event.button > 1) return;
  const link = event.composedPath().find(node => node instanceof HTMLAnchorElement);
  if (!link) return;
  const raw = link.getAttribute('href')?.trim();
  if (!raw || raw.startsWith('#') || !/^https?:$/.test(link.protocol)) return;
  event.preventDefault();
  ipcRenderer.send('game:external', link.href);
}
window.addEventListener('click', handleLink);
window.addEventListener('auxclick', handleLink);
for (const type of ['click', 'auxclick', 'keydown']) {
  window.addEventListener(type, event => {
    if (event.isTrusted && (type !== 'keydown' || event.key === 'Enter' || event.key === ' ')) {
      ipcRenderer.send('game:gesture', true);
      setTimeout(() => ipcRenderer.send('game:gesture', false), 0);
    }
  }, true);
}
