const http = require('http');
function getJson(url){return new Promise((res,rej)=>{http.get(url,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej)})}
(async()=>{
  const targets = await getJson('http://127.0.0.1:9222/json');
  const page = targets.find(t=>t.type==='page' && t.url.includes('playgame')) || targets.find(t=>t.type==='page');
  console.log('TARGET', page.url);
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let id=0;
  ws.onopen=()=>{ ws.send(JSON.stringify({id:++id,method:'Network.enable',params:{}})); ws.send(JSON.stringify({id:++id,method:'Page.enable',params:{}})); setTimeout(()=>ws.send(JSON.stringify({id:++id,method:'Page.reload',params:{ignoreCache:true}})),500); };
  const reqMap={};
  ws.onmessage=ev=>{const m=JSON.parse(ev.data);
    if(m.method==='Network.requestWillBeSent'){ reqMap[m.params.requestId]=m.params.request.url; }
    if(m.method==='Network.responseReceived'){ const u=m.params.response.url; if(/\.(swf|png)(\?|$)/i.test(u)||u.includes('/flash/')) console.log('RESP',m.params.response.status,u); }
    if(m.method==='Network.loadingFailed'){ const u=reqMap[m.params.requestId]||'?'; console.log('FAIL',m.params.errorText,u); }
  };
  setTimeout(()=>{console.log('DONE'); ws.close(); process.exit(0);},15000);
})().catch(e=>{console.error(e);process.exit(1)});
