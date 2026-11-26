const $ = selector => document.querySelector(selector);
const api = window.workbench;
let rows = [], lastTabs = '';
const send = (name, value) => api.action(name, value).catch(error => { $('#status').textContent = error.message; });

function keyLabel(code) {
  const names = { ArrowLeft:'←', ArrowUp:'↑', ArrowRight:'→', ArrowDown:'↓', Space:'空格', Escape:'Esc' };
  return names[code] || code.replace(/^Key|^Digit/, '').replace(/^Numpad/, '小键盘 ');
}
function button(label, name, click) {
  const el = document.createElement('button');
  el.textContent = label;
  el.setAttribute('aria-label', name);
  el.addEventListener('click', click);
  return el;
}
function renderRows() {
  const list = $('#steps');
  list.replaceChildren();
  rows.forEach((row, i) => {
    const line = document.createElement('div');
    line.className = 'columns';
    const key = button(keyLabel(row.code), '第 ' + (i + 1) + ' 行按键', () => {});
    key.className = 'key';
    key.title = '点击后直接录入按键。F7 和 F8 为控制热键。';
    key.onfocus = () => send('capture', true);
    key.onblur = () => send('capture', false);
    key.onkeydown = event => {
      if (event.metaKey || event.ctrlKey || event.altKey || event.shiftKey) return;
      event.preventDefault();
      if (!/^(Key[A-Z]|Digit\d|Numpad\d|Space|Enter|Tab|Escape|Arrow(Up|Down|Left|Right)|F([1-6]|9|1[0-2]))$/.test(event.code)) return;
      row.code = event.code;
      key.textContent = keyLabel(row.code);
      send('steps', rows);
    };
    const interval = document.createElement('input');
    interval.type = 'number'; interval.min = '1'; interval.max = '60000'; interval.value = row.ms;
    interval.setAttribute('aria-label', '第 ' + (i + 1) + ' 行间隔');
    interval.oninput = () => {
      if (interval.validity.valid && interval.value !== '') { row.ms = Number(interval.value); send('steps', rows); }
    };
    interval.onblur = () => { interval.value = row.ms; };
    const enable = document.createElement('input');
    enable.type = 'checkbox'; enable.checked = row.enabled;
    enable.setAttribute('aria-label', '启用第 ' + (i + 1) + ' 行');
    enable.onchange = () => { row.enabled = enable.checked; send('steps', rows); };
    const remove = button('×', '删除第 ' + (i + 1) + ' 行', () => { rows.splice(i, 1); renderRows(); send('steps', rows); });
    remove.className = 'remove';
    line.append(key, interval, enable, remove); list.append(line);
  });
  $('#add-key').disabled = rows.length >= 32;
}

function render(state) {
  const tabs = state.ids.join('') + '/' + state.id;
  if (lastTabs !== tabs) {
    lastTabs = tabs;
    const items = state.ids.map(id => {
      const line = document.createElement('div'); line.className = 'tab';
      const tab = button('页面 ' + id, '切换至页面 ' + id, () => send('select', id));
      tab.setAttribute('aria-pressed', String(id === state.id)); line.append(tab);
      if (id !== 'A') {
        const close = button('×', '关闭页面 ' + id, () => send('close', id));
        close.className = 'close'; line.append(close);
      }
      return line;
    });
    $('#tabs').replaceChildren(...items);
  }
  $('#count').textContent = state.ids.length + ' / 4';
  $('#add').disabled = state.creating || state.ids.length >= 4;
  $('#page-label').textContent = state.id ? '页面 ' + state.id : '';
  $('#title').textContent = state.test ? '输入验证' : '造梦西游 3';
  $('#status').textContent = state.running ? '正在页面 ' + state.id + ' 连点' : state.status;
  $('#shortcuts').textContent = state.unavailable.length ? state.unavailable.join(' / ') + ' 全局快捷键不可用' : '';
  $('#toggle span').textContent = state.running ? '停止连点' : '开始连点';
  $('#volume-label').textContent = state.volume === 0 ? '已静音' : state.volume + '%';
  $('#volume').value = state.volume;
  $('#background').checked = state.background;
  document.querySelectorAll('[data-rate]').forEach(el => el.setAttribute('aria-pressed', String(Number(el.dataset.rate) === state.speed)));
}
for (const id of ['add','help','stop','toggle','home','refresh']) $('#' + id).onclick = () => send(id);
for (const el of document.querySelectorAll('[data-rate]')) el.onclick = () => send('speed', Number(el.dataset.rate));
$('#volume').oninput = event => send('volume', Number(event.target.value));
$('#background').onchange = event => send('background', event.target.checked);
$('#add-key').onclick = () => {
  if (rows.length >= 32) return;
  rows.push({ code: 'KeyS', ms: 1, enabled: false });
  renderRows(); send('steps', rows);
  $('#steps .columns:last-child .key').focus();
};
function resize() {
  const { x, y, width, height } = $('#stage').getBoundingClientRect();
  api.bounds({ x, y, width, height });
}
new ResizeObserver(resize).observe($('#stage'));
api.onState(render);
api.action('state').then(state => { rows = state.steps; renderRows(); render(state); resize(); });
