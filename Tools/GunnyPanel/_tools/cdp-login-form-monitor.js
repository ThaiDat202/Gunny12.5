const http=require('http'),fs=require('fs');
const port='9222';
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{
 const targets=await getJson('http://127.0.0.1:'+port+'/json');
 const page=targets.find(x=>x.type==='page');
 console.log('PAGE',page.url);
 const ws=new WebSocket(page.webSocketDebuggerUrl);
 let id=0; const pend=new Map();
 function send(m,p){const i=++id;ws.send(JSON.stringify({id:i,method:m,params:p||{}}));return i;}
 function evalExpr(expr){const i=send('Runtime.evaluate',{expression:expr,returnByValue:true});return new Promise(res=>{const t=setInterval(()=>{if(pend.has(i)){clearInterval(t);const r=pend.get(i);res(r&&r.result&&r.result.result?r.result.result.value:null);}},100);});}
 ws.onmessage=async e=>{const m=JSON.parse(e.data);
  if(m.id){pend.set(m.id,m);return;}
  if(m.method==='Network.responseReceived'){
    const u=m.params.response.url||'';
    if(u.includes('login.ashx')){console.log('LOGIN_RESP',m.params.response.status,u);send('Network.getResponseBody',{requestId:m.params.requestId});}
    else if(m.params.response.status>=400) console.log('BAD_RESP',m.params.response.status,u);
  }
  if(m.method==='Network.loadingFinished'){
    const r=Array.from(pend.values()).filter(x=>x.result&&x.result.requestId===m.params.requestId);
    for(const x of r){}
  }
  if(m.method==='Network.loadingFailed') console.log('FAIL',m.params.errorText);
  if(m.method==='Runtime.exceptionThrown') console.log('EXC',JSON.stringify(m.params.exceptionDetails).slice(0,300));
  if(m.method==='Log.entryAdded') console.log('LOG',m.params.entry.level,m.params.entry.text);
 };
 ws.onopen=()=>{
  send('Page.enable');send('Runtime.enable');send('Network.enable');send('Log.enable');
  send('Page.navigate',{url:'http://127.0.0.1/'});
  setTimeout(async()=>{
   console.log('BEFORE',await evalExpr('location.href'));
   console.log('CLICK',await evalExpr(`(function(){var u=document.getElementById('u');var p=document.getElementById('p');if(u)u.value='test02';if(p)p.value='123456';var b=document.getElementById('buttonlgn');if(b){b.click();return 'clicked';}return 'no';})()`));
  },3000);
  setTimeout(()=>{const s=send('Page.captureScreenshot',{format:'png'});setTimeout(()=>{if(pend.has(s)&&pend.get(s).result){fs.writeFileSync('C:\\Gunny\\ToolGunny\\_analysis\\ui24\\monitor-form.png',Buffer.from(pend.get(s).result.data,'base64'));console.log('SAVED');}process.exit(0);},3000);},45000);
 };
 setTimeout(()=>{console.log('TIMEOUT');process.exit(0);},60000);
})().catch(e=>{console.error(e);process.exit(1)});
