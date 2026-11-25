const http = require('http');
const fs = require('fs');
const port = process.env.CDP_PORT || '9222';
const out = process.argv[2] || 'C:\\Gunny\\ToolGunny\\_analysis\\ui24\\hall251-shot.png';
const waitMs = parseInt(process.argv[3] || '20000', 10);
function getJson(url){return new Promise((res,rej)=>{http.get(url,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej)})}
(async()=>{
  const targets = await getJson('http://127.0.0.1:'+port+'/json');
  const page = targets.find(t=>t.type==='page');
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let id=0; const pending=new Map();
  function send(method,params){const mid=++id; ws.send(JSON.stringify({id:mid,method,params:params||{}})); return mid;}
  ws.onopen=()=>{ send('Page.enable'); send('Runtime.enable'); };
  ws.onmessage=ev=>{const m=JSON.parse(ev.data); if(m.id)pending.set(m.id,m); if(m.method==='Runtime.consoleAPICalled'){const a=(m.params.args||[]).map(x=>x.value!==undefined?x.value:(x.description||x.type));console.log('CONSOLE',m.params.type,a.join(' '));} if(m.method==='Runtime.exceptionThrown')console.log('EXC',JSON.stringify(m.params.exceptionDetails).slice(0,300));};
  setTimeout(()=>{
    send('Runtime.evaluate',{expression:'document.location.href',returnByValue:true});
    const sid=send('Page.captureScreenshot',{format:'png'});
    setTimeout(()=>{ if(pending.has(sid)){const r=pending.get(sid);fs.writeFileSync(out,Buffer.from(r.result.data,'base64'));console.log('SAVED',out);} ws.close(); process.exit(0); },3000);
  }, waitMs);
  setTimeout(()=>{console.log('TIMEOUT');process.exit(0);}, waitMs+12000);
})().catch(e=>{console.error(e);process.exit(1)});
