const http=require('http');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const t=await getJson('http://127.0.0.1:9222/json');const p=t.find(x=>x.type==='page');const ws=new WebSocket(p.webSocketDebuggerUrl);let id=0;const pending=new Map();
function send(m,params){const mid=++id;ws.send(JSON.stringify({id:mid,method:m,params:params||{}}));return mid;}
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id)pending.set(m.id,m);};
ws.onopen=()=>{send('Page.enable');send('Runtime.enable');setTimeout(()=>{
 const mid=send('Runtime.evaluate',{expression:`JSON.stringify({url:location.href, inputs:Array.from(document.querySelectorAll('input')).map((e,i)=>({i,id:e.id,name:e.name,type:e.type,value:e.value})), buttons:Array.from(document.querySelectorAll('button,input[type=submit],a')).slice(0,30).map((e,i)=>({i,tag:e.tagName,id:e.id,text:(e.innerText||e.value||'').trim(),href:e.href||''}))})`,returnByValue:true});
 setTimeout(()=>{const r=pending.get(mid);console.log(JSON.stringify(r&&r.result&&r.result.result?r.result.result.value:r).slice(0,3000));process.exit(0);},1000);
},500);};setTimeout(()=>process.exit(0),8000);})();
