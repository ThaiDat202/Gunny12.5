const http = require('http');
const fs = require('fs');
const port = process.env.CDP_PORT || '9222';
const [x,y,out] = process.argv.slice(2);
function getJson(url){return new Promise((res,rej)=>{http.get(url,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej)})}
(async()=>{
  const targets = await getJson('http://127.0.0.1:'+port+'/json');
  const page = targets.find(t=>t.type==='page');
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let id=0; const pending=new Map();
  function send(method,params){const mid=++id; ws.send(JSON.stringify({id:mid,method,params:params||{}})); return mid;}
  ws.onopen=()=>{
    send('Page.enable');
    setTimeout(()=>{
      send('Input.dispatchMouseEvent',{type:'mousePressed',x:Number(x),y:Number(y),button:'left',clickCount:1});
      send('Input.dispatchMouseEvent',{type:'mouseReleased',x:Number(x),y:Number(y),button:'left',clickCount:1});
    },500);
    setTimeout(()=>{
      const sid=send('Page.captureScreenshot',{format:'png'});
      setTimeout(()=>{ if(pending.has(sid)){const r=pending.get(sid);fs.writeFileSync(out,Buffer.from(r.result.data,'base64'));console.log('SAVED',out);} ws.close(); process.exit(0); },2500);
    },5000);
  };
  ws.onmessage=ev=>{const m=JSON.parse(ev.data); if(m.id)pending.set(m.id,m);};
  setTimeout(()=>{console.log('TIMEOUT');process.exit(0);},15000);
})().catch(e=>{console.error(e);process.exit(1)});
