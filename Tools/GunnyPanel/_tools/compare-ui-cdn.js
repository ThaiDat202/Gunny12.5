const fs = require('fs');
const path = require('path');
const http = require('http');

const LOCAL = 'C:\\Gunny\\Gunny12.6\\WebSite\\Flash\\ui\\vietnam';
const BASE = 'http://gunny.vcdn.vn/flash/ui/vietnam/';

function walk(dir, out) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out); else out.push(p);
  }
}
function head(url) {
  return new Promise(resolve => {
    const u = new URL(url);
    const req = http.request({ method: 'HEAD', hostname: u.hostname, path: u.pathname, timeout: 15000 }, res => {
      resolve({ status: res.statusCode, len: parseInt(res.headers['content-length'] || '-1', 10) });
    });
    req.on('timeout', () => { req.destroy(); resolve({ status: 0, len: -1 }); });
    req.on('error', () => resolve({ status: -1, len: -1 }));
    req.end();
  });
}
(async () => {
  const files = [];
  walk(LOCAL, files);
  const rels = files.map(f => path.relative(LOCAL, f).replace(/\\/g, '/'));
  const results = [];
  let idx = 0;
  async function worker() {
    while (idx < rels.length) {
      const i = idx++;
      const rel = rels[i];
      const localSize = fs.statSync(path.join(LOCAL, rel)).size;
      const url = BASE + encodeURI(rel);
      const h = await head(url);
      if (h.status === 200 && h.len !== localSize) results.push({ rel, local: localSize, cdn: h.len });
      else if (h.status !== 200) results.push({ rel, local: localSize, cdn: h.status });
    }
  }
  await Promise.all(Array.from({ length: 16 }, worker));
  results.sort((a, b) => String(a.rel).localeCompare(String(b.rel)));
  fs.writeFileSync('C:\\Gunny\\ToolGunny\\_analysis\\ui24\\ui-compare.json', JSON.stringify({ total: rels.length, diff: results }, null, 2));
  console.log('total', rels.length, 'diff/missing', results.length);
  for (const r of results.slice(0, 120)) console.log(r);
})();
