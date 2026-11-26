const specialKeys = new Map([
  ['Space', [' ', 32]], ['Tab', ['Tab', 9]], ['Enter', ['Enter', 13]], ['Escape', ['Escape', 27]],
  ['ArrowLeft', ['ArrowLeft', 37]], ['ArrowUp', ['ArrowUp', 38]],
  ['ArrowRight', ['ArrowRight', 39]], ['ArrowDown', ['ArrowDown', 40]]
]);

function keyEvent(code) {
  let key, virtualKey;
  const keypad = /^Numpad[0-9]$/.test(code);
  if (/^Key[A-Z]$/.test(code)) {
    key = code.slice(3).toLowerCase();
    virtualKey = code.charCodeAt(3);
  } else if (/^Digit[0-9]$/.test(code) || keypad) {
    key = code.slice(-1);
    virtualKey = Number(key) + (keypad ? 96 : 48);
  } else if (/^F([1-6]|9|1[0-2])$/.test(code)) {
    key = code;
    virtualKey = 111 + Number(code.slice(1));
  } else if (specialKeys.has(code)) {
    [key, virtualKey] = specialKeys.get(code);
  } else {
    throw new Error('不支持此按键。F7 和 F8 为控制热键。');
  }
  return { key, code, windowsVirtualKeyCode: virtualKey, isKeypad: keypad, location: keypad ? 3 : 0 };
}

function checkRows(rows) {
  if (!Array.isArray(rows) || rows.length > 32) throw new Error('最多设置 32 行按键');
  return rows.map(row => {
    keyEvent(row.code);
    if (typeof row.enabled !== 'boolean' || !Number.isInteger(row.ms) || row.ms < 1 || row.ms > 60000) {
      throw new Error('间隔需为 1～60000 毫秒');
    }
    return { code: row.code, ms: row.ms, enabled: row.enabled };
  });
}

// Each dispatch is addressed to this webContents, even when the window is inactive.
// CDP preserves the physical code and keypad location, including on keyboards without Num Lock.
class Repeater {
  constructor(contents, onError) {
    this.contents = contents;
    this.onError = onError;
    this.run = null;
  }
  send(type, key) {
    return this.contents.debugger.sendCommand('Input.dispatchKeyEvent', { type, ...key });
  }
  start(rows) {
    const steps = checkRows(rows).filter(row => row.enabled);
    if (!steps.length) throw new Error('请至少启用一行按键');
    if (!this.contents.debugger.isAttached()) this.contents.debugger.attach('1.3');
    const run = { held: null, timer: null };
    this.run = run;
    const next = async index => {
      if (this.run !== run) return;
      try {
        const row = steps[index];
        const key = keyEvent(row.code);
        run.held = key;
        await this.send('rawKeyDown', key);
        if (this.run !== run) return;
        run.timer = setTimeout(async () => {
          if (this.run !== run) return;
          try {
            const release = this.send('keyUp', key);
            run.held = null;
            await release;
            if (this.run !== run) return;
            run.timer = setTimeout(() => next((index + 1) % steps.length), row.ms);
          } catch (error) { fail(error); }
        }, Math.min(30, row.ms));
      } catch (error) { fail(error); }
    };
    const fail = error => {
      if (this.run !== run) return;
      this.stop();
      this.onError(error);
    };
    next(0);
  }
  stop() {
    const run = this.run;
    this.run = null;
    if (!run) return Promise.resolve();
    clearTimeout(run.timer);
    // Commands on one debugger connection are ordered. Queue keyUp before detaching the view.
    if (run.held && !this.contents.isDestroyed() && this.contents.debugger.isAttached()) {
      return this.send('keyUp', run.held).catch(() => {});
    }
    return Promise.resolve();
  }
}
module.exports = { Repeater, checkRows, keyEvent };
