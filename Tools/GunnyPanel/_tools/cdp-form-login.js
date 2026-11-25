const http=require('http'),fs=require('fs');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const t=await getJson('http://127.0.0.1:9222/json');const p=t.find(x=>x.type==='page');const ws=new WebSocket(p.webSocketDebuggerUrl);let id=0;const pend=new Map();
function send(m,params){const i=++id;ws.send(JSON.stringify({id:i,method:m,params:params||{}}));return i;}
function ev(expr){return new Promise(res=>{const i=send('Runtime.evaluate',{expression:expr,returnByValue:true});const t=setInterval(()=>{if(pend.has(i)){clearInterval(t);const r=pend.get(i);res(r&&r.result&&r.result.result?r.result.result.value:r);}},100);});}
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id)pend.set(m.id,m);};
ws.onopen=()=>{send('Page.enable');send('Runtime.enable');send('Page.navigate',{url:'http://127.0.0.1/'});setTimeout(async()=>{
 console.log('BEFORE',await ev('location.href'));
 console.log('FIELDS',await ev(`JSON.stringify(Array.from(document.querySelectorAll('input')).map(e=>({id:e.id,name:e.name,type:e.type,value:e.value})))`));
 console.log('CLICK',await ev(`(function(){var u=document.getElementById('u');var p=document.getElementById('p');if(u)u.value='test02';if(p)p.value='123456';var b=document.getElementById('buttonlgn');if(b){b.click();return 'clicked';}return 'nobutton';})()`));
 setTimeout(async()=>{console.log('AFTER',await ev('location.href'));},12000);
},3500);setTimeout(()=>{const s=send('Page.captureScreenshot',{format:'png'});setTimeout(()=>{if(pend.has(s)){fs.writeFileSync('C:\\Gunny\\ToolGunny\\_analysis\\ui24\\form-login.png',Buffer.from(pend.get(s).result.data,'base64'));console.log('SAVED');}process.exit(0);},2500);},30000);};
setTimeout(()=>process.exit(0),40000);})();
