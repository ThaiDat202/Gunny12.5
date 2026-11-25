// CDP client for 37abc (Chrome 60) - capture console/log/network while reloading test page
const http = require('http');

function getJson(url) {
  return new Promise((resolve, reject) => {
    http.get(url, res => {
      let b = '';
      res.on('data', d => b += d);
      res.on('end', () => { try { resolve(JSON.parse(b)); } catch (e) { reject(e); } });
    }).on('error', reject);
  });
}

(async () => {
  const targets = await getJson('http://127.0.0.1:9222/json');
  const target = targets.find(t => t.type === 'page' && t.url.includes('playgame.aspx')) || targets.find(t => t.type === 'page');
  if (!target) { console.error('NO TARGET'); process.exit(1); }
  console.log('TARGET', target.id, target.url);
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  let id = 0;
  const pending = new Map();
  const events = [];
  ws.onopen = () => {
    console.log('WS OPEN');
    send('Runtime.enable');
    send('Log.enable');
    send('Network.enable');
    send('Page.enable');
    setTimeout(() => send('Page.reload', { ignoreCache: true }), 800);
  };
  function send(method, params) {
    const mid = ++id;
    ws.send(JSON.stringify({ id: mid, method, params: params || {} }));
    return mid;
  }
  ws.onmessage = ev => {
    const m = JSON.parse(ev.data);
    if (m.id) { pending.set(m.id, m); return; }
    const method = m.method || '';
    if (method === 'Runtime.consoleAPICalled') {
      const args = (m.params.args || []).map(a => a.value !== undefined ? String(a.value) : (a.description || a.type));
      console.log('[CONSOLE-' + m.params.type + ']', args.join(' '));
    } else if (method === 'Runtime.exceptionThrown') {
      const d = m.params.exceptionDetails || {};
      console.log('[EXCEPTION]', d.text || '', d.exception ? (d.exception.description || d.exception.value) : '', d.url || '', d.lineNumber, d.columnNumber);
    } else if (method === 'Log.entryAdded') {
      const e = m.params.entry || {};
      console.log('[LOG-' + e.level + ']', e.source, e.text, e.url || '');
    } else if (method === 'Network.loadingFailed') {
      console.log('[NET-FAIL]', m.params.type, m.params.errorText, m.params.requestId);
    } else if (method === 'Network.responseReceived') {
      const r = m.params.response || {};
      if ((r.url || '').includes('2.png') || (r.url || '').includes('4.png') || (r.url || '').endsWith('.swf'))
        console.log('[NET-OK]', r.status, r.url);
    } else if (method === 'Page.loadEventFired') {
      console.log('[PAGE-LOADED]');
    }
    if (method) events.push(m);
  };
  ws.onerror = e => console.error('WS ERR', e.message || e);
  ws.onclose = () => console.log('WS CLOSE');
  // evaluate diagnostics before and after
  async function evalJs(expr, label) {
    return new Promise(resolve => {
      const mid = send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: false });
      const timer = setInterval(() => {
        if (pending.has(mid)) {
          clearInterval(timer);
          const r = pending.get(mid); pending.delete(mid);
          console.log('[EVAL ' + label + ']', JSON.stringify(r.result && r.result.result ? r.result.result.value : r));
          resolve();
        }
      }, 100);
    });
  }
  setTimeout(async () => {
    await evalJs('navigator.userAgent', 'UA');
    await evalJs('(navigator.plugins && navigator.plugins.length) ? (navigator.plugins[0].name + " || " + navigator.plugins[0].description) : "no plugins"', 'PLUGIN0');
    await evalJs('Array.prototype.map.call(navigator.plugins, function(p){return p.name+" :: "+p.description}).join("\\n")', 'PLUGINS');
    await evalJs('(function(){var e=document.getElementById("7road-ddt-game")||document.querySelector("embed"); if(!e)return "no embed"; var s=""; for(var i=0;i<e.attributes.length;i++){s+=e.attributes[i].name+"="+e.attributes[i].value+"\\n"} return s;})()', 'EMBED');
  }, 6000);
  setTimeout(() => {
    console.log('DONE events=' + events.length);
    ws.close();
    process.exit(0);
  }, 22000);
})().catch(e => { console.error(e); process.exit(1); });
