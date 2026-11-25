const http=require('http'),fs=require('fs');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const t=await getJson('http://127.0.0.1:9223/json');const pages=t.filter(x=>x.type==='page');console.log('TARGETS',pages.map(p=>p.url).join(' | '));const p=pages.find(x=>x.url.includes('8088'))||pages[0];if(!p){console.log('NO PAGE');process.exit(0);}const ws=new WebSocket(p.webSocketDebuggerUrl);let id=0;const pending=new Map();
function send(m,params){const mid=++id;ws.send(JSON.stringify({id:mid,method:m,params:params||{}}));return mid;}
function ev(expr){return new Promise(res=>{const mid=send('Runtime.evaluate',{expression:expr,returnByValue:true});const t=setInterval(()=>{if(pending.has(mid)){clearInterval(t);const r=pending.get(mid);res(r&&r.result&&r.result.result?r.result.result.value:r);}},100);});}
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id)pending.set(m.id,m);};
ws.onopen=async()=>{send('Page.enable');send('Runtime.enable');await new Promise(r=>setTimeout(r,500));
console.log('URL',await ev('location.href'));
console.log('RESOURCES',JSON.stringify(await ev(`performance.getEntriesByType('resource').filter(e=>/2\\.png|4\\.png/.test(e.name)).map(e=>({name:e.name,transferSize:e.transferSize,encodedBodySize:e.encodedBodySize}))`)));
console.log('PLUGIN',await ev(`navigator.plugins[0]?navigator.plugins[0].description:'none'`));
const sid=send('Page.captureScreenshot',{format:'png'});setTimeout(()=>{if(pending.has(sid)){const r=pending.get(sid);fs.writeFileSync(process.argv[2]||'C:\\Gunny\\ToolGunny\\_analysis\\ui24\\current.png',Buffer.from(r.result.data,'base64'));console.log('SAVED');}process.exit(0);},2000);};
setTimeout(()=>process.exit(0),10000);})();
