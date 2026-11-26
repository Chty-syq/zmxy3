const assert = require('node:assert/strict');
const { setTimeout: wait } = require('node:timers/promises');

async function until(read, label, timeout = 12000) {
  const start = Date.now();
  let value;
  while (Date.now() - start < timeout) {
    value = await read();
    if (value) return value;
    await wait(40);
  }
  throw new Error('等待超时 · ' + label + ' (' + value + ')');
}
async function run({ win, action, pages, active, testLinks }) {
  const ui = code => win.webContents.executeJavaScript(code);
  const a = active();
  const probe = (page = a) => page.web.executeJavaScript('window.testState?.()');
  await until(() => probe(), '输入验证页');
  assert.deepEqual(await ui("Array.from(document.querySelectorAll('.key'), b => b.textContent)"), ['S','S','J']);
  assert.equal((await action('state')).steps.some(row => row.enabled), false);
  await ui("document.getElementById('add-key').click()");
  await until(async () => (await action('state')).steps.length === 4, '添加按键');
  await ui("document.querySelector('#steps .columns:last-child .key').dispatchEvent(new KeyboardEvent('keydown', {code:'Numpad7', bubbles:true,cancelable:true}))");
  await until(async () => (await action('state')).steps[3].code === 'Numpad7', '录入小键盘');
  await ui("document.querySelector('#steps .columns:last-child .remove').click()");
  await until(async () => (await action('state')).steps.length === 3, '删除按键');
  await action('background', true); // exercise input without activating the user's test window
  await action('toggle');
  assert.equal((await action('state')).running, false);
  const movie = await until(() => a.web.mainFrame.framesInSubtree.find(f => f.url === 'zmxy-test://movie/clock.html'), '跨来源 Flash 子页面');
  const flash = code => movie.executeJavaScript(code);
  await a.web.executeJavaScript("document.querySelector('iframe').scrollIntoView({block:'center'})");
  await until(() => flash("!!document.querySelector('ruffle-embed')?.ruffle().metadata"), 'Ruffle 加载真实 SWF');
  await flash(`window.samples = []; window.sawS = false;
    document.querySelector('ruffle-embed').ruffle().traceObserver = value => {
      if (value === 'true' || value === true) window.sawS = true;
      if (Number.isFinite(Number(value))) samples.push([Number(value), performance.now()]);
      if (samples.length > 500) samples.shift();
    }`);
  for (const speed of [1, 2, 3, 100, 1]) {
    await action('speed', speed);
    await wait(60);
    await flash('samples.length = 0');
    await until(() => flash('samples.length >= 5 && samples.at(-1)[1] - samples[0][1] > 200'), 'ActionScript getTimer');
    const rate = await flash('(samples.at(-1)[0] - samples[0][0]) / (samples.at(-1)[1] - samples[0][1])');
    assert.ok(Math.abs(rate-speed) < speed * 0.16, 'Flash clock ' + speed + '×: ' + rate);
    console.log('PASS SWF getTimer ' + speed + '×: ' + rate.toFixed(2));
  }
  await action('volume', 25);
  await until(() => flash("Math.abs(document.querySelector('ruffle-embed').ruffle().volume - .25) < .001"), 'Flash 音量');
  await action('volume', 0);
  assert.equal(a.web.isAudioMuted(), true);
  await action('volume', 100);

  const steps = [{ code:'Digit1', ms:2, enabled:true }, { code:'KeyQ', ms:1, enabled:false }, { code:'Numpad1', ms:5, enabled:true }];
  await action('steps', steps);
  await a.web.executeJavaScript("document.querySelector('#target').focus()");
  await action('toggle');
  await until(async () => (await probe()).keys >= 6, '真实按键事件');
  await action('stop');
  await until(async () => (await probe()).held === 0, '释放按键');
  const events = (await probe()).events.filter(e => e.type === 'down').slice(0,4);
  assert.deepEqual(events.map(e => [e.code, e.location]), [['Digit1',0],['Numpad1',3],['Digit1',0],['Numpad1',3]]);
  console.log('PASS key order, keypad location, emergency release');

  await action('steps', [{code:'KeyS', ms:30, enabled:true}]);
  if (!win.isFocused()) {
    await a.web.debugger.sendCommand('Emulation.setFocusEmulationEnabled', { enabled:true });
  }
  await a.web.executeJavaScript("document.querySelector('iframe').scrollIntoView({block:'center', behavior:'instant'})");
  const frame = await a.web.executeJavaScript(`(() => {
    const element = document.querySelector('iframe');
    const box = element.getBoundingClientRect();
    return { x: box.left + element.clientLeft, y: box.top + element.clientTop };
  })()`);
  const player = await flash(`(() => {
    const element = document.querySelector('ruffle-embed');
    const canvas = element.shadowRoot.querySelector('canvas');
    canvas.focus({preventScroll:true});
    const box = canvas.getBoundingClientRect();
    return { x: box.left + box.width / 2, y: box.top + box.height / 2 };
  })()`);
  const point = { x: Math.round(frame.x + player.x), y: Math.round(frame.y + player.y) };
  await a.web.debugger.sendCommand('Input.dispatchMouseEvent', {type:'mousePressed', ...point, button:'left', clickCount:1});
  await a.web.debugger.sendCommand('Input.dispatchMouseEvent', {type:'mouseReleased', ...point, button:'left', clickCount:1});
  await action('toggle');
  try {
    await until(() => flash('window.sawS'), 'Flash 子页面接收连点');
  } catch (error) {
    console.error('Flash test focus', point, await a.web.executeJavaScript('document.activeElement.tagName'),
      await flash('[document.hasFocus(), document.activeElement.tagName, document.activeElement.shadowRoot?.activeElement?.tagName, samples.length]'));
    throw error;
  }
  await action('stop');
  console.log('PASS ActionScript Key.isDown inside a cross-origin player');

  await action('speed', 3);
  await a.profile.cookies.set({url:'https://www.4399.com/', name:'integration-only', value:'a', httpOnly:true, secure:true});
  await action('toggle');
  await action('add');
  const b = active();
  assert.equal(b.id, 'B'); assert.equal(a.input.run, null); assert.equal(b.web.isAudioMuted(), true);
  assert.equal((await b.profile.cookies.get({ name:'integration-only' }))[0].value, 'a');
  assert.notEqual(b.profile, a.profile);
  assert.equal((await probe()).held, 0);
  await action('add'); await action('add'); await action('add');
  assert.deepEqual([...pages.keys()], [...'ABCD']);
  await action('close','A'); await action('close','C');
  assert.equal(active().id,'D');
  await action('close','D'); assert.equal(active().id,'A');
  await action('add'); assert.equal(active().id,'C');
  console.log('PASS four pages, session inheritance, mute, page reuse');

  await action('select','A');
  await action('stop'); assert.equal((await action('state')).speed, 1);
  await a.web.executeJavaScript("window.open('https://example.org/automatic')");
  await wait(120); assert.equal(testLinks.length, 0);
  for (const element of ['link-self','link-blank','link-other','link-script']) {
    const { x, y } = await a.web.executeJavaScript("(() => {const e=document.getElementById('" + element + "'); e.scrollIntoView({block:'center'}); const r=e.getBoundingClientRect(); return {x:Math.round(r.x+r.width/2),y:Math.round(r.y+r.height/2)};})()");
    const count = testLinks.length;
    a.web.sendInputEvent({type:'mouseDown',x,y,button:'left',clickCount:1});
    a.web.sendInputEvent({type:'mouseUp',x,y,button:'left',clickCount:1});
    await until(() => testLinks.length === count+1, '手动打开链接 ' + element);
    assert.equal(a.web.getURL(), a.url);
  }
  console.log('PASS external link routing, blocked automatic popup');
  console.log('造梦工坊 macOS 集成检查通过');
}
module.exports = { run };
