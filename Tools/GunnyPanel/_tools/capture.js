// capture.js - chup screenshot qua Chrome DevTools Protocol (ho tro emulation width < 500px)
// usage: node capture.js <url> <outPng> [width] [height]
const fs = require('fs');
const http = require('http');

const PORT = parseInt(process.env.CDP_PORT || '9333', 10);

function getJson(path, method = 'GET') {
    return new Promise((resolve, reject) => {
        const req = http.request({ host: '127.0.0.1', port: PORT, path, method }, res => {
            let data = '';
            res.on('data', c => (data += c));
            res.on('end', () => {
                try { resolve(JSON.parse(data)); } catch (e) { reject(new Error('bad json: ' + data.slice(0, 200))); }
            });
        });
        req.on('error', reject);
        req.end();
    });
}

async function main() {
    const [url, out, wArg, hArg, scrollArg] = process.argv.slice(2);
    const width = parseInt(wArg || '390', 10);
    const height = parseInt(hArg || '844', 10);
    if (!url || !out) throw new Error('usage: node capture.js <url> <out.png> [w] [h]');

    const target = await getJson('/json/new?' + encodeURIComponent(url), 'PUT');
    const ws = new WebSocket(target.webSocketDebuggerUrl);
    let seq = 0;
    const pending = new Map();

    const send = (method, params = {}) => new Promise((resolve, reject) => {
        const id = ++seq;
        pending.set(id, { resolve, reject });
        ws.send(JSON.stringify({ id, method, params }));
    });

    ws.onmessage = ev => {
        const msg = JSON.parse(ev.data);
        if (msg.id && pending.has(msg.id)) {
            const p = pending.get(msg.id);
            pending.delete(msg.id);
            if (msg.error) p.reject(new Error(JSON.stringify(msg.error)));
            else p.resolve(msg);
        }
    };

    await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });

    await send('Page.enable');
    await send('Runtime.enable');
    await send('Network.enable');

    const cookies = process.env.COOKIES_JSON ? JSON.parse(process.env.COOKIES_JSON) : [];
    for (const cookie of cookies) {
        try {
            await send('Network.setCookie', {
                name: cookie.name,
                value: cookie.value,
                url: new URL(url).origin + '/',
                domain: '127.0.0.1',
                path: '/'
            });
        } catch (e) {
            console.log('cookie-skip(' + cookie.name + '): ' + (e.message || e));
        }
    }

    try {
        await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: width < 500 });
    } catch (e) {
        // Browser cũ (vd 37abc) không hỗ trợ đầy đủ tham số — bỏ qua, dùng kích thước cửa sổ thật.
        console.log('emulation-skipped: ' + (e.message || e));
    }

    await send('Page.navigate', { url });
    await new Promise(r => setTimeout(r, parseInt(process.env.WAIT_MS || '2600', 10)));

    if (scrollArg) {
        const expr = scrollArg === 'bottom'
            ? 'window.scrollTo(0, document.body.scrollHeight)'
            : 'window.scrollTo(0, ' + parseInt(scrollArg, 10) + ')';
        await send('Runtime.evaluate', { expression: expr });
        await new Promise(r => setTimeout(r, 500));
    }

    if (process.env.PRE_JS) {
        await send('Runtime.evaluate', { expression: process.env.PRE_JS });
        await new Promise(r => setTimeout(r, parseInt(process.env.PRE_WAIT_MS || '1200', 10)));
    }

    if (process.env.POST_JS) {
        const res = await send('Runtime.evaluate', {
            expression: process.env.POST_JS,
            returnByValue: true
        });
        console.log('POST_JS=' + JSON.stringify(res.result?.result?.value ?? null));
    }

    const evalRes = await send('Runtime.evaluate', {
        expression: `(function(){
            var w = document.documentElement.clientWidth;
            var sw = document.documentElement.scrollWidth;
            return { title: document.title, clientWidth: w, scrollWidth: sw, overflow: sw > w + 1 };
        })()`,
        returnByValue: true
    });
    console.log(JSON.stringify(evalRes.result?.result?.value ?? evalRes));

    const shot = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
    fs.writeFileSync(out, Buffer.from(shot.result.data, 'base64'));

    try { await getJson('/json/close/' + target.id, 'GET'); } catch { }
    ws.close();
}

main().catch(err => { console.error('ERR', err); process.exit(1); });
