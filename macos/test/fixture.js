const fs = require('node:fs');

// A two-frame Flash movie. Each frame runs the AVM1 GetTime and Trace actions.
// The observed values come from Flash bytecode inside WASM, not from shell JS.
function clockMovie() {
  const tag = (id, bytes = []) => {
    const header = Buffer.alloc(2);
    header.writeUInt16LE((id << 6) | bytes.length);
    return Buffer.concat([header, Buffer.from(bytes)]);
  };
  const pushString = text => [0x96, text.length + 2, 0, 0, ...Buffer.from(text), 0];
  const pushInt = n => [0x96, 5, 0, 7, n, 0, 0, 0];
  const actions = tag(12, [
    0x34, 0x26,  // trace(getTimer())
    ...pushInt(83), ...pushInt(1), ...pushString('Key'), 0x1c,
    ...pushString('isDown'), 0x52, 0x26, 0 // trace(Key.isDown(83))
  ]);
  const body = Buffer.concat([Buffer.from([0x60, 0x00, 0x3e, 0x80, 0x00, 0x0f, 0xa0, 0, 30, 2, 0]),
    actions, tag(1), actions, tag(1), tag(0)]);
  const header = Buffer.from([0x46, 0x57, 0x53, 7, 0, 0, 0, 0]);
  header.writeUInt32LE(header.length + body.length, 4);
  return Buffer.concat([header, body]);
}
function install(profile, testFile) {
  profile.protocol.handle('zmxy-test', request => {
    const url = new URL(request.url);
    if (url.href === 'zmxy-test://local/test-page.html') {
      const html = fs.readFileSync(testFile, 'utf8').replace('</html>',
        '<iframe title="Flash 时钟验证" width="140" height="32" src="zmxy-test://movie/clock.html"></iframe></html>');
      return new Response(html, { headers: { 'Content-Type': 'text/html; charset=utf-8' } });
    }
    if (url.href === 'zmxy-test://movie/clock.html') {
      return new Response('<!doctype html><meta charset=utf-8><embed id="movie" type="application/x-shockwave-flash" src="clock.swf" width="128" height="30">', { headers: { 'Content-Type': 'text/html' } });
    }
    if (url.href === 'zmxy-test://movie/clock.swf') {
      return new Response(clockMovie(), { headers: { 'Content-Type': 'application/x-shockwave-flash' } });
    }
    return new Response(null, { status: 404 });
  });
}
module.exports = { install };
