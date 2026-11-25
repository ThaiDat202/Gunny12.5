const http=require('http'),fs=require('fs');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const t=await getJson('http://127.0.0.1:9222/json');const p=t.find(x=>x.type==='page');const ws=new WebSocket(p.webSocketDebuggerUrl);let id=0;const pending=new Map();
function send(m,params){const mid=++id;ws.send(JSON.stringify({id:mid,method:m,params:params||{}}));return mid;}
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id)pending.set(m.id,m);};
ws.onopen=()=>{send('Page.enable');setTimeout(()=>send('Page.reload',{ignoreCache:true}),500);setTimeout(()=>{const sid=send('Page.captureScreenshot',{format:'png'});setTimeout(()=>{if(pending.has(sid)){fs.writeFileSync(process.argv[2],Buffer.from(pending.get(sid).result.data,'base64'));console.log('SAVED');}process.exit(0);},2000);},35000);};
setTimeout(()=>process.exit(0),45000);})();
