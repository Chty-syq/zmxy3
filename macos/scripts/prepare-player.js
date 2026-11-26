// Adapt the two wasm-bindgen ABIs (modern and MVP) at their clock boundary.
// A changed upstream binding must fail the build rather than ship a broken speed control.
const fs = require('node:fs');
const path = require('node:path');
const packageDir = path.dirname(require.resolve('@ruffle-rs/ruffle/package.json'));
const output = path.join(__dirname, '../.generated/player');
const host = fs.readFileSync(path.join(__dirname, '../player/host.js'), 'utf8');
let source = fs.readFileSync(path.join(packageDir, 'ruffle.js'), 'utf8');
const bindings = [
  ['return A.now()', 'return gameTime(A.now())'],
  ['return a(A).now()', 'return gameTime(a(A).now())'],
  ['A.requestAnimationFrame(e)', 'A.requestAnimationFrame(() => e(gameTime(performance.now())))'],
  ['a(A).requestAnimationFrame(a(e))', 'a(A).requestAnimationFrame(((callback) => () => callback(gameTime(performance.now())))(a(e)))'],
  ['tick_for_background(performance.now())', 'tick_for_background(gameTime(performance.now()))']
];
for (const [original, adapted] of bindings) {
  if (source.split(original).length !== 2) throw new Error('Unexpected Ruffle binding: ' + original);
  source = source.replace(original, adapted);
}
fs.mkdirSync(output, { recursive: true });
fs.writeFileSync(path.join(output, 'ruffle.js'), '(() => {\n' + host + '\n' + source + '\n})();\n');
for (const name of fs.readdirSync(packageDir)) {
  if (name.endsWith('.wasm') || name.startsWith('LICENSE_')) {
    fs.copyFileSync(path.join(packageDir, name), path.join(output, name));
  }
}
fs.writeFileSync(path.join(output, 'NOTICE.txt'), 'Ruffle 0.8.0 · https://github.com/ruffle-rs/ruffle/tree/v0.8.0\nThe JavaScript host adapts clock reads for playback speed.\n');
