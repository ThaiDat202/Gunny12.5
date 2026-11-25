const http=require('http'),fs=require('fs');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const t=await getJson('http://127.0.0.1:9222/json');const p=t.find(x=>x.type==='page');const ws=new WebSocket(p.webSocketDebuggerUrl);let id=0;const pend=new Map();
function send(m,params){const i=++id;ws.send(JSON.stringify({id:i,method:m,params:params||{}}));return i;}
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id)pend.set(m.id,m);};
ws.onopen=()=>{send('Page.enable');setTimeout(()=>{const s=send('Page.captureScreenshot',{format:'png'});setTimeout(()=>{if(pend.has(s)){fs.writeFileSync('C:\\Gunny\\ToolGunny\\_analysis\\ui24\\cur37.png',Buffer.from(pend.get(s).result.data,'base64'));console.log('SAVED');}else console.log('NO SCREEN');process.exit(0);},3000);},1000);};
setTimeout(()=>process.exit(0),10000);})();
