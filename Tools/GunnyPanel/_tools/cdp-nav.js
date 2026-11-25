const http = require('http');
function getJson(url){return new Promise((res,rej)=>{http.get(url,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej)})}
(async()=>{
  const targets = await getJson('http://127.0.0.1:9222/json');
  const page = targets.find(t=>t.type==='page' && t.url.includes('playgame')) || targets.find(t=>t.type==='page');
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let id=0; const pending=new Map();
  function send(method,params){const mid=++id; ws.send(JSON.stringify({id:mid,method,params:params||{}})); return mid;}
  ws.onopen=()=>{ console.log('OPEN',page.url); send('Page.enable'); send('Runtime.enable'); send('Page.navigate',{url:'http://localhost:8088/playgame.aspx?user=test01&key=9934a906-bcad-41e4-8e6e-20201a2f5ec8'}); };
  ws.onmessage=ev=>{const m=JSON.parse(ev.data); if(m.id){pending.set(m.id,m); return;} if(m.method==='Page.loadEventFired'){console.log('LOADED'); setTimeout(done,8000);} if(m.method==='Runtime.exceptionThrown'){console.log('EXC',JSON.stringify(m.params.exceptionDetails).slice(0,500));} if(m.method==='Runtime.consoleAPICalled'){const a=(m.params.args||[]).map(x=>x.value!==undefined?x.value:(x.description||x.type)); console.log('CONSOLE',m.params.type,a.join(' '));}};
  async function evalJs(expr,label){return new Promise(res=>{const mid=send('Runtime.evaluate',{expression:expr,returnByValue:true}); const t=setInterval(()=>{if(pending.has(mid)){clearInterval(t);const r=pending.get(mid);pending.delete(mid);console.log('EVAL '+label,JSON.stringify(r.result&&r.result.result?r.result.result.value:r));res();}},100);});}
  async function done(){
    await evalJs('document.location.href','URL');
    await evalJs('document.title','TITLE');
    await evalJs('(navigator.plugins[0]?navigator.plugins[0].description:"none")','PLUGIN');
    const sid=send('Page.captureScreenshot',{format:'png'});
    setTimeout(()=>{ if(pending.has(sid)){ const r=pending.get(sid); require('fs').writeFileSync('C:\\Gunny\\ToolGunny\\GunnyPanel\\_tools\\screenshots\\37abc-game-flash32.png',Buffer.from(r.result.data,'base64')); console.log('SHOT saved'); } ws.close(); process.exit(0); },1500);
  }
  setTimeout(()=>{console.log('TIMEOUT'); process.exit(0);},25000);
})().catch(e=>{console.error(e);process.exit(1)});
