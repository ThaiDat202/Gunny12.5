const http=require('http');
const fs=require('fs');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const t=await getJson('http://127.0.0.1:9222/json');const p=t.find(x=>x.type==='page');const ws=new WebSocket(p.webSocketDebuggerUrl);let id=0;const pending=new Map();
function send(m,params){const mid=++id;ws.send(JSON.stringify({id:mid,method:m,params:params||{}}));return mid;}
function ev(expr){return new Promise(res=>{const mid=send('Runtime.evaluate',{expression:expr,returnByValue:true,awaitPromise:true});const t=setInterval(()=>{if(pending.has(mid)){clearInterval(t);const r=pending.get(mid);res(r&&r.result&&r.result.result?r.result.result.value:r);}},200);});}
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id)pending.set(m.id,m);};
ws.onopen=async()=>{
 send('Page.enable');send('Runtime.enable');
 send('Page.navigate',{url:'http://localhost:8088/'});
 await new Promise(r=>setTimeout(r,4000));
 let info=await ev(`JSON.stringify({url:location.href, fields:Array.from(document.querySelectorAll('input')).map(e=>({id:e.id,type:e.type,value:e.value})), btn:Array.from(document.querySelectorAll('button')).map(e=>({id:e.id,text:e.innerText}))})`);
 console.log('BEFORE',info);
 await ev(`(function(){var u=document.getElementById('u')||document.querySelector('input[type=text]');var p=document.getElementById('p')||document.querySelector('input[type=password]');if(u)u.value='haha123';if(p)p.value='123456';var b=document.getElementById('buttonlgn')||document.querySelector('button');if(b){b.click();return 'clicked';}return 'nobutton';})()`);
 await new Promise(r=>setTimeout(r,12000));
 console.log('AFTER',await ev(`location.href`));
 send('Page.captureScreenshot',{format:'png'});
 setTimeout(()=>{ const m=Array.from(pending.values()).find(x=>x.id===id); if(m&&m.result){fs.writeFileSync('C:\\Gunny\\ToolGunny\\_analysis\\ui24\\testlogin.png',Buffer.from(m.result.data,'base64'));console.log('SAVED');} process.exit(0); },3000);
};
setTimeout(()=>process.exit(0),25000);})();
