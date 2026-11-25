const http=require('http'),fs=require('fs');
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{
 const targets=await getJson('http://127.0.0.1:9222/json'); const page=targets.find(x=>x.type==='page');
 const ws=new WebSocket(page.webSocketDebuggerUrl); let id=0; const pend=new Map(); let loginRid=null;
 function send(m,p){const i=++id;ws.send(JSON.stringify({id:i,method:m,params:p||{}}));return i;}
 function ev(expr){const i=send('Runtime.evaluate',{expression:expr,returnByValue:true});return new Promise(res=>{const t=setInterval(()=>{if(pend.has(i)){clearInterval(t);const r=pend.get(i);res(r&&r.result&&r.result.result?r.result.result.value:null);}},100);});}
 ws.onmessage=e=>{const m=JSON.parse(e.data);
  if(m.id){pend.set(m.id,m);
    if(m.result&&m.result.body!==undefined) console.log('BODY_RESULT',String(m.result.base64Encoded),String(m.result.body).slice(0,2000));
    return;}
  if(m.method==='Network.responseReceived'&&(m.params.response.url||'').includes('login.ashx')){
    loginRid=m.params.requestId; console.log('LOGIN_RESP',m.params.response.status,m.params.response.url);
  }
  if(m.method==='Network.loadingFinished'&&loginRid&&m.params.requestId===loginRid){
    console.log('LOGIN_FINISHED'); send('Network.getResponseBody',{requestId:loginRid});
  }
  if(m.method==='Network.loadingFailed') console.log('FAIL',m.params.errorText);
  if(m.method==='Log.entryAdded') console.log('LOG',m.params.entry.level,m.params.entry.text);
 };
 ws.onopen=()=>{
  send('Page.enable');send('Runtime.enable');send('Network.enable');send('Log.enable');
  send('Page.navigate',{url:'http://127.0.0.1/'});
  setTimeout(async()=>{console.log('URL',await ev('location.href'));console.log('CLICK',await ev(`(function(){var u=document.getElementById('u');var p=document.getElementById('p');if(u)u.value='test02';if(p)p.value='123456';var b=document.getElementById('buttonlgn');if(b){b.click();return 'ok';}return 'no';})()`));},3000);
  setTimeout(()=>{process.exit(0);},25000);
 };
 setTimeout(()=>process.exit(0),30000);
})().catch(e=>{console.error(e);process.exit(1)});
