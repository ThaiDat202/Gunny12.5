const http=require('http'),fs=require('fs');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const url=process.argv[2]||'http://127.0.0.1/';const out=process.argv[3]||'C:\\Gunny\\ToolGunny\\_analysis\\ui24\\live-login.png';
const t=await getJson('http://127.0.0.1:9222/json');const p=t.find(x=>x.type==='page');const ws=new WebSocket(p.webSocketDebuggerUrl);let id=0;const pending=new Map();
function send(m,params){const mid=++id;ws.send(JSON.stringify({id:mid,method:m,params:params||{}}));return mid;}
function ev(expr){return new Promise(res=>{const mid=send('Runtime.evaluate',{expression:expr,returnByValue:true});const t=setInterval(()=>{if(pending.has(mid)){clearInterval(t);const r=pending.get(mid);res(r);}},200);});}
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id)pending.set(m.id,m);};
ws.onopen=async()=>{send('Page.enable');send('Runtime.enable');send('Page.navigate',{url});await new Promise(r=>setTimeout(r,4000));
console.log('BEFORE',JSON.stringify((await ev('location.href')).result&&(await ev('location.href')).result.result));
await ev(`(function(){var u=document.getElementById('u')||document.querySelector('input[type=text]');var p=document.getElementById('p')||document.querySelector('input[type=password]');if(u)u.value='haha123';if(p)p.value='123456';var b=document.getElementById('buttonlgn')||document.querySelector('button');if(b){b.click();return 'clicked';}return 'nobutton';})()`);
await new Promise(r=>setTimeout(r,18000));
send('Page.captureScreenshot',{format:'png'});setTimeout(()=>{const m=Array.from(pending.values()).find(x=>x.id===id);if(m&&m.result){fs.writeFileSync(out,Buffer.from(m.result.data,'base64'));console.log('SAVED',out);}process.exit(0);},2500);};
setTimeout(()=>process.exit(0),30000);})();
