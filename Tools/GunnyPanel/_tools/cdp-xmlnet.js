const http=require('http');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const t=await getJson('http://127.0.0.1:9222/json');const p=t.find(x=>x.type==='page');const ws=new WebSocket(p.webSocketDebuggerUrl);let id=0;const req={};
ws.onopen=()=>{ws.send(JSON.stringify({id:++id,method:'Network.enable'}));setTimeout(()=>ws.send(JSON.stringify({id:++id,method:'Page.reload',params:{ignoreCache:true}})),300);};
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.method==='Network.requestWillBeSent'){req[m.params.requestId]=m.params.request.url;}if(m.method==='Network.responseReceived'){const u=m.params.response.url;if(u.includes('ddthall')||u.includes('.xml'))console.log(m.params.response.status,u);}if(m.method==='Network.loadingFailed'){const u=req[m.params.requestId]||'';if(u.includes('ddthall')||u.includes('.xml'))console.log('FAIL',u);}};
setTimeout(()=>process.exit(0),12000);})();
