const fs = require('node:fs');
const path = require('node:path');
const { app, session, WebContentsView } = require('electron');
const { Repeater } = require('./input');

const gameURL = 'https://www.4399.com/flash/zmhj.htm?g=3';
const fixtureURL = 'zmxy-test://local/test-page.html';
const gameDomain = host => /(^|\.)4399\.(com|cn)$/i.test(host);
function httpURL(value) {
  try {
    const url = new URL(value);
    return ['https:', 'http:'].includes(url.protocol) ? url : null;
  } catch { return null; }
}

function resourcePath(relative) {
  return app.isPackaged ? path.join(process.resourcesPath, relative)
    : (relative === 'test-page.html' ? path.join(__dirname, '..', relative)
      : path.join(__dirname, '.generated', relative));
}

async function prepareSession(id, test) {
  const profile = session.fromPartition('persist:page-' + id);
  if (!profile.protocol.isProtocolHandled('zmxy-player')) {
    const root = resourcePath('player');
    const assets = new Set(fs.readdirSync(root).filter(name => name === 'ruffle.js' || name.endsWith('.wasm')));
    profile.protocol.handle('zmxy-player', async request => {
      const url = new URL(request.url);
      const name = url.pathname.slice(1);
      if (url.host !== 'assets' || !assets.has(name)) return new Response(null, { status: 404 });
      const data = await fs.promises.readFile(path.join(root, name));
      return new Response(data, { headers: {
        'Content-Type': name.endsWith('.wasm') ? 'application/wasm' : 'application/javascript',
        'Access-Control-Allow-Origin': '*', 'Cross-Origin-Resource-Policy': 'cross-origin'
      } });
    });
    if (test) require('./test/fixture').install(profile, resourcePath('test-page.html'));
    profile.setPermissionRequestHandler((_wc, _permission, callback) => callback(false));
    profile.setPermissionCheckHandler(() => false);
    profile.on('will-download', event => event.preventDefault());
    await profile.setProxy({ mode: 'direct' });
  }
  return profile;
}

async function inheritLogin(source, target) {
  const firstParty = cookie => gameDomain(cookie.domain.replace(/^\./, ''));
  const address = cookie => (cookie.secure ? 'https://' : 'http://') + cookie.domain.replace(/^\./, '') + cookie.path;
  const [from, before] = await Promise.all([source.cookies.get({}), target.cookies.get({})]);
  async function replace(old, next) {
    for (const cookie of old.filter(firstParty)) await target.cookies.remove(address(cookie), cookie.name);
    for (const cookie of next.filter(firstParty)) {
      const { name, value, path, secure, httpOnly, sameSite, expirationDate } = cookie;
      const details = { url: address(cookie), name, value, path, secure, httpOnly, sameSite };
      if (!cookie.hostOnly) details.domain = cookie.domain;
      if (!cookie.session) details.expirationDate = expirationDate;
      await target.cookies.set(details);
    }
  }
  try {
    await replace(before, from);
    await target.cookies.flushStore();
  } catch (error) {
    await replace(await target.cookies.get({}), before);
    await target.cookies.flushStore();
    throw error;
  }
}

class GamePage {
  constructor(id, profile, test, notify, external) {
    this.id = id;
    this.profile = profile;
    this.url = test ? fixtureURL : gameURL;
    this.retriedFlash = false;
    this.view = new WebContentsView({ webPreferences: {
      session: profile, sandbox: true, contextIsolation: true, nodeIntegration: false,
      // This enables the sandboxed preload inside the official cross-origin game iframe.
      nodeIntegrationInSubFrames: true, preload: path.join(__dirname, 'game-preload.js'),
      additionalArguments: test ? ['--zmxy-test-page'] : [], backgroundThrottling: false,
      autoplayPolicy: 'no-user-gesture-required'
    } });
    this.web = this.view.webContents;
    this.status = '正在加载';
    this.input = new Repeater(this.web, error => { this.status = error.message; notify(); });
    this.web.setAudioMuted(id !== 'A');
    this.web.setWindowOpenHandler(({ url }) => {
      if (this.lastGesture && Date.now() - this.lastGesture < 700 && httpURL(url)) {
        this.lastGesture = 0;
        external(url);
      }
      return { action: 'deny' };
    });
    const guard = event => {
      if (!event.isMainFrame) return;
      const url = httpURL(event.url);
      if (!(url && gameDomain(url.hostname)) && !(test && event.url === fixtureURL)) event.preventDefault();
    };
    this.web.on('will-frame-navigate', guard);
    this.web.on('will-redirect', guard);
    this.web.on('did-start-navigation', event => {
      if (event.isMainFrame && !event.isSameDocument) {
        this.input.stop();
        this.status = '正在加载';
        notify();
      }
    });
    this.web.on('did-finish-load', () => { this.status = '页面已加载'; notify(); });
    this.web.on('did-fail-load', (_event, code, description, _url, isMainFrame) => {
      if (isMainFrame && code !== -3) { this.status = '加载失败 · ' + description; notify(); }
    });
    this.web.on('render-process-gone', () => { this.input.stop(); this.status = '页面进程已退出，请刷新'; notify(); });
  }

  navigate(refresh = false) {
    this.retriedFlash = false;
    this.input.stop();
    if (refresh && !this.web.isCrashed()) this.web.reload();
    else this.web.loadURL(this.url).catch(() => {}); // did-fail-load reports the error in the shell
  }

  apply(settings) {
    this.web.setAudioMuted(settings.volume === 0);
    for (const frame of this.web.mainFrame.framesInSubtree) {
      if (!frame.detached) frame.send('game:settings', settings);
    }
  }

  close() {
    this.input.stop();
    this.web.close();
  }
}
module.exports = { GamePage, gameURL, fixtureURL, prepareSession, inheritLogin, httpURL };
