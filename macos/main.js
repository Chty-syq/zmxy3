const { app, BrowserWindow, dialog, globalShortcut, ipcMain, Menu, protocol, screen, shell } = require('electron');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { GamePage, prepareSession, inheritLogin, httpURL } = require('./game');
const { checkRows } = require('./input');

const integration = process.argv.includes('--integration-test') || process.argv.includes('--self-test');
const onlineSmoke = process.argv.includes('--online-smoke-test');
const test = integration || process.argv.includes('--test-page');
app.setName('造梦工坊');
if (integration || onlineSmoke) {
  const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'zmxy-test-'));
  app.setPath('userData', scratch);
  app.once('quit', () => {
    try { fs.rmSync(scratch, { recursive: true, force: true }); }
    catch { /* macOS can reclaim the temporary directory after the helpers exit. */ }
  });
} else if (test) app.setPath('userData', path.join(app.getPath('appData'), '造梦工坊-输入验证'));
if (process.argv.includes('--software-rendering')) app.disableHardwareAcceleration();
if (!process.argv.includes('--enable-web-accessibility')) app.commandLine.appendSwitch('disable-renderer-accessibility');
const scheme = name => ({ scheme: name, privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true } });
protocol.registerSchemesAsPrivileged([scheme('zmxy-player'), ...(test ? [scheme('zmxy-test')] : [])]);

const shellFile = path.join(__dirname, 'shell/index.html');
const pages = new Map();
const steps = ['KeyS', 'KeyS', 'KeyJ'].map(code => ({ code, ms: 1, enabled: false }));
const controls = { speed: 1, volume: 100, background: false, steps };
let win, active, bounds, creating = false, capturing = false, quitting = false;
let revision = 0, unavailable = [], hotkeys = new Set();
const testLinks = [];
if ((integration || onlineSmoke) && process.platform === 'darwin') app.setActivationPolicy('accessory');

function trustedShell(event) {
  return win && event.sender === win.webContents && event.senderFrame === win.webContents.mainFrame
    && event.senderFrame.url === pathToFileURL(shellFile).href;
}
function gameSender(event) {
  return [...pages.values()].find(page => page.web === event.sender);
}
function settings(page) {
  return { revision, speed: controls.speed, volume: page.id === 'A' ? controls.volume / 100 : 0 };
}
function external(value) {
  const url = httpURL(value);
  if (!url) return;
  if (integration) testLinks.push(url.href);
  else shell.openExternal(url.href).catch(error => { if (active) active.status = error.message; notify(); });
}
function snapshot() {
  return { ...controls, ids: [...pages.keys()].sort(), id: active?.id, running: Boolean(active?.input.run),
    status: active?.status || '正在准备页面', creating, unavailable, test };
}
function notify() {
  if (!quitting && win && !win.isDestroyed()) win.webContents.send('shell:state', snapshot());
}
function updatePlayers() {
  revision++;
  for (const page of pages.values()) page.apply(settings(page));
  notify();
}
function place() {
  if (!active || !bounds || !win || win.isDestroyed()) return;
  const [width, height] = win.getContentSize();
  const x = Math.max(0, Math.min(Math.round(bounds.x), width));
  const y = Math.max(0, Math.min(Math.round(bounds.y), height));
  const viewWidth = Math.max(0, Math.min(Math.round(bounds.width), width - x));
  active.view.setBounds({ x, y, width: viewWidth, height: Math.max(0, Math.min(Math.round(bounds.height), height - y)) });
  // The official game expects a desktop viewport of roughly 1000 CSS pixels.
  if (viewWidth > 0 && !test) active.web.setZoomFactor(Math.min(1, viewWidth / 1000));
}
function select(id) {
  const next = pages.get(id);
  if (!next || active === next) return;
  if (active) { active.input.stop(); active.view.setVisible(false); }
  active = next;
  capturing = false;
  place();
  active.view.setVisible(true);
  if (win.isFocused()) active.web.focus();
  notify();
}
async function add() {
  const id = [...'ABCD'].find(letter => !pages.has(letter));
  if (!id || creating) return;
  creating = true;
  const previous = active;
  notify();
  try {
    const profile = await prepareSession(id, test);
    if (previous) await inheritLogin(previous.profile, profile);
    if (quitting) return;
    const page = new GamePage(id, profile, test, notify, external);
    pages.set(id, page);
    win.contentView.addChildView(page.view);
    page.apply(settings(page));
    select(id);
    page.navigate();
  } finally { creating = false; notify(); }
}
function close(id) {
  if (id === 'A' || creating) return;
  const page = pages.get(id);
  if (!page) return;
  if (active === page) select('A');
  pages.delete(id);
  win.contentView.removeChildView(page.view);
  page.close();
  notify();
}
function stopAll(reset) {
  for (const page of pages.values()) page.input.stop();
  if (reset) { controls.speed = 1; updatePlayers(); }
  else notify();
}
function toggle() {
  if (!active) return;
  if (active.input.run) active.input.stop();
  else if (!capturing && (win.isFocused() || controls.background)) {
    if (win.isFocused()) active.web.focus();
    try { active.input.start(controls.steps); active.status = '页面已加载'; }
    catch (error) { active.status = error.message; }
  }
  notify();
}
async function action(name, value) {
  switch (name) {
    case 'state': return snapshot();
    case 'add': await add(); break;
    case 'select': select(value); break;
    case 'close': close(value); break;
    case 'home': active?.navigate(); break;
    case 'refresh': active?.navigate(true); break;
    case 'toggle': toggle(); break;
    case 'stop': stopAll(true); break;
    case 'capture': capturing = value === true; break;
    case 'background':
      controls.background = value === true;
      if (!controls.background && !win.isFocused()) stopAll(false);
      break;
    case 'steps': controls.steps = checkRows(value); break;
    case 'speed':
      if ([1, 2, 3, 100].includes(value)) { controls.speed = value; updatePlayers(); }
      break;
    case 'volume':
      if (Number.isInteger(value) && value >= 0 && value <= 100) { controls.volume = value; updatePlayers(); }
      break;
    case 'help':
      await dialog.showMessageBox(win, { type: 'info', title: '使用说明', message: '造梦工坊 ' + app.getVersion(),
        detail: '新页面继承当前 4399 账号，A 保留声音。关闭或刷新前请先在游戏里保存。\n\n点击按键框直接录入，启用所需行。F7 开始或停止当前页连点，间隔从松开按键算起。运行中的修改在下次启动时生效。\n\n切页会停止连点。F8 停止所有页面连点并恢复 1×。如功能键控制媒体，请按住 fn 再按 F7 或 F8。', buttons: ['好'] });
      break;
  }
  notify();
  return snapshot();
}

ipcMain.handle('shell:action', (event, name, value) => {
  if (!trustedShell(event)) throw new Error('无效控制来源');
  return action(name, value);
});
ipcMain.on('shell:bounds', (event, rect) => {
  if (!trustedShell(event) || !rect || !['x','y','width','height'].every(key => Number.isFinite(rect[key]))) return;
  bounds = rect;
  place();
});
ipcMain.handle('game:settings', event => {
  const page = gameSender(event);
  if (!page) throw new Error('页面已关闭');
  return settings(page);
});
ipcMain.on('game:external', (event, url) => { if (gameSender(event) === active && active) external(url); });
ipcMain.on('game:gesture', (event, down) => {
  const page = gameSender(event);
  if (page === active && page) page.lastGesture = down === true ? Date.now() : 0;
});
ipcMain.on('game:retry-flash', event => {
  const page = gameSender(event);
  if (!page || page.retriedFlash || event.senderFrame !== page.web.mainFrame || page.web.getURL() !== page.url) return;
  page.retriedFlash = true;
  page.web.reload();
});
ipcMain.on('game:player-error', event => {
  const page = gameSender(event);
  if (page) { page.status = '播放器加载失败，请刷新'; notify(); }
});

function registerHotkeys() {
  for (const [key, callback] of [['F7', toggle], ['F8', () => stopAll(true)]]) {
    if (globalShortcut.register(key, callback)) hotkeys.add(key);
    else unavailable.push(key);
  }
}
function localHotkeys(web) {
  web.on('before-input-event', (event, input) => {
    if (input.type !== 'keyDown' || input.isAutoRepeat || hotkeys.has(input.key)) return;
    if (input.key === 'F7' || input.key === 'F8') {
      event.preventDefault();
      if (input.key === 'F7') toggle(); else stopAll(true);
    }
  });
}
app.on('web-contents-created', (_event, web) => localHotkeys(web));

async function start() {
  const work = screen.getPrimaryDisplay().workAreaSize;
  win = new BrowserWindow({ title: '造梦工坊', width: Math.min(work.width, 1460), height: Math.min(work.height, 940),
    minWidth: 850, minHeight: 540, backgroundColor: '#f5f5f0', show: false,
    webPreferences: { preload: path.join(__dirname, 'shell-preload.js'), sandbox: true, contextIsolation: true, nodeIntegration: false } });
  win.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  win.webContents.on('will-navigate', event => event.preventDefault());
  win.on('blur', () => { if (!controls.background) stopAll(false); });
  win.on('resize', place);
  win.on('close', () => {
    quitting = true;
    for (const page of pages.values()) page.close();
    pages.clear();
    globalShortcut.unregisterAll();
  });
  Menu.setApplicationMenu(Menu.buildFromTemplate([
    { role: 'appMenu' }, { role: 'editMenu' }, { role: 'windowMenu' }
  ]));
  if (!integration && !onlineSmoke) registerHotkeys();
  await win.loadFile(shellFile);
  if (integration || onlineSmoke) win.showInactive();
  else win.show();
  await add();
  if (integration) {
    await require('./test/integration').run({ win, action, pages, testLinks, active: () => active });
    app.exit(0);
  } else if (onlineSmoke) {
    await require('./test/online').run(active);
    app.exit(0);
  } else if (process.argv.includes('--performance-capture')) {
    require('./performance').schedule(active, win, app.getPath('userData'));
  }
}
if (app.requestSingleInstanceLock()) {
  app.on('second-instance', () => { if (win) { win.restore(); win.show(); win.focus(); } });
  app.whenReady().then(start).catch(error => {
    console.error(error);
    if (!integration && !onlineSmoke) dialog.showErrorBox('造梦工坊启动失败', error.message);
    app.exit(1);
  });
} else app.quit();
app.on('window-all-closed', () => app.quit());
