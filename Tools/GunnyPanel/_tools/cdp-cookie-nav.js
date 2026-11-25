const http = require('http');
const fs = require('fs');
const args = process.argv.slice(2);
const CDP_PORT = process.env.CDP_PORT || '9222';
const sid = args[0], targetUrl = args[1], outPng = args[2];
function getJson(url){return new Promise((res,rej)=>{http.get(url,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej)})}
(async()=>{
  const targets = await getJson('http://127.0.0.1:'+CDP_PORT+'/json');
  const page = targets.find(t=>t.type==='page');
  console.log('TARGET', page.url);
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let id=0; const pending=new Map();
  function send(method,params){const mid=++id; ws.send(JSON.stringify({id:mid,method,params:params||{}})); return mid;}
  ws.onopen=()=>{
    send('Page.enable'); send('Runtime.enable'); send('Network.enable'); send('Log.enable');
    send('Network.setCookie',{name:'ASP.NET_SessionId',value:sid,url:'http://localhost:8088/',domain:'localhost',path:'/',httpOnly:true});
    setTimeout(()=>send('Page.navigate',{url:targetUrl}),600);
  };
  ws.onmessage=ev=>{const m=JSON.parse(ev.data);
    if(m.id){pending.set(m.id,m);return;}
    if(m.method==='Runtime.consoleAPICalled'){const a=(m.params.args||[]).map(x=>x.value!==undefined?x.value:(x.description||x.type));console.log('CONSOLE',m.params.type,a.join(' '));}
    if(m.method==='Runtime.exceptionThrown'){console.log('EXC',JSON.stringify(m.params.exceptionDetails).slice(0,600));}
    if(m.method==='Log.entryAdded'){const e=m.params.entry;console.log('LOG',e.level,e.source,e.text);}
    if(m.method==='Network.responseReceived'){const u=m.params.response.url; if(/2\.png|4\.png|\.swf|config\.xml/.test(u)) console.log('RESP',m.params.response.status,u);}
    if(m.method==='Network.loadingFailed'){console.log('FAIL',m.params.errorText,m.params.requestId);}
  };
  async function evalJs(expr,label){return new Promise(res=>{const mid=send('Runtime.evaluate',{expression:expr,returnByValue:true});const t=setInterval(()=>{if(pending.has(mid)){clearInterval(t);const r=pending.get(mid);console.log('EVAL '+label,JSON.stringify(r.result&&r.result.result?r.result.result.value:r));res();}},100);});}
  setTimeout(async()=>{
    await evalJs('document.location.href','URL');
    await evalJs('(navigator.plugins[0]?navigator.plugins[0].description:"none")','PLUGIN');
    const sidShot=send('Page.captureScreenshot',{format:'png'});
    setTimeout(()=>{ if(pending.has(sidShot)){ const r=pending.get(sidShot); fs.writeFileSync(outPng,Buffer.from(r.result.data,'base64')); console.log('SHOT',outPng);} ws.close(); process.exit(0); },2000);
  },14000);
  setTimeout(()=>{console.log('TIMEOUT');process.exit(0);},30000);
})().catch(e=>{console.error(e);process.exit(1)});
