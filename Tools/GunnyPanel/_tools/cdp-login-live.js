const http = require('http');
const fs = require('fs');
const port = process.env.CDP_PORT || '9223';
const sid = process.argv[2];
const target = process.argv[3];
const out = process.argv[4] || 'C:\\Gunny\\ToolGunny\\_analysis\\ui24\\login-debug.png';
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{
  const targets=await getJson('http://127.0.0.1:'+port+'/json');
  const page=targets.find(t=>t.type==='page');
  const ws=new WebSocket(page.webSocketDebuggerUrl);
  let id=0; const pending=new Map();
  function send(m,p){const i=++id;ws.send(JSON.stringify({id:i,method:m,params:p||{}}));return i;}
  ws.onmessage=e=>{const m=JSON.parse(e.data); if(m.id){pending.set(m.id,m);return;}
    if(m.method==='Runtime.consoleAPICalled'){const a=(m.params.args||[]).map(x=>x.value!==undefined?x.value:(x.description||x.type)); console.log('CONSOLE',m.params.type,a.join(' '));}
    if(m.method==='Runtime.exceptionThrown'){console.log('EXC',JSON.stringify(m.params.exceptionDetails).slice(0,800));}
    if(m.method==='Network.loadingFailed'){console.log('FAIL',m.params.errorText,m.params.requestId);}
    if(m.method==='Network.responseReceived'){const u=m.params.response.url; if(m.params.response.status>=400||/\.swf|\.png|config\.xml|login/.test(u)) console.log('RESP',m.params.response.status,u);}
    if(m.method==='Log.entryAdded'){const x=m.params.entry; console.log('LOG',x.level,x.source,x.text);}
  };
  ws.onopen=()=>{
    send('Page.enable');send('Runtime.enable');send('Network.enable');send('Log.enable');
    send('Network.setCookie',{name:'ASP.NET_SessionId',value:sid,url:'http://127.0.0.1/',domain:'127.0.0.1',path:'/',httpOnly:true});
    setTimeout(()=>send('Page.navigate',{url:target}),500);
    setTimeout(()=>send('Runtime.evaluate',{expression:'JSON.stringify({url:location.href,title:document.title})',returnByValue:true}),15000);
    setTimeout(()=>{const sidShot=send('Page.captureScreenshot',{format:'png'}); setTimeout(()=>{if(pending.has(sidShot)){fs.writeFileSync(out,Buffer.from(pending.get(sidShot).result.data,'base64'));console.log('SAVED',out);} process.exit(0);},2000);},60000);
  };
  setTimeout(()=>{console.log('TIMEOUT');process.exit(0);},90000);
})().catch(e=>{console.error(e);process.exit(1)});
