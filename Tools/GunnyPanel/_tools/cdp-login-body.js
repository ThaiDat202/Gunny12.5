const http=require('http'),fs=require('fs');
const port='9223';
const sid=process.argv[2], target=process.argv[3], out=process.argv[4]||'C:\\Gunny\\ToolGunny\\_analysis\\ui24\\login-body.png';
function getJson(u){return new Promise((res,rej)=>http.get(u,r=>{let b='';r.on('data',d=>b+=d);r.on('end',()=>{try{res(JSON.parse(b))}catch(e){rej(e)}})}).on('error',rej))}
(async()=>{const t=await getJson('http://127.0.0.1:'+port+'/json');const page=t.find(x=>x.type==='page');const ws=new WebSocket(page.webSocketDebuggerUrl);let id=0;const pending=new Map();
function send(m,p){const i=++id;ws.send(JSON.stringify({id:i,method:m,params:p||{}}));return i;}
ws.onmessage=async e=>{const m=JSON.parse(e.data);if(m.id){pending.set(m.id,m);return;}
 if(m.method==='Network.responseReceived'&&m.params.response.url.includes('login.ashx')){
   console.log('LOGIN status',m.params.response.status,m.params.response.url);
   const rid=m.params.requestId;
   send('Network.getResponseBody',{requestId:rid});
   setTimeout(()=>{const b=Array.from(pending.values()).filter(x=>x.result&&x.result.body); if(b.length){console.log('LOGIN BODY',b[b.length-1].result.body.slice(0,1000));}},1000);
 }
 if(m.method==='Network.loadingFailed') console.log('FAIL',m.params.errorText);
 if(m.method==='Runtime.exceptionThrown') console.log('EXC',JSON.stringify(m.params.exceptionDetails).slice(0,500));
 if(m.method==='Log.entryAdded'&&m.params.entry.level==='error') console.log('LOGERR',m.params.entry.text);
};
ws.onopen=()=>{send('Page.enable');send('Runtime.enable');send('Network.enable');send('Log.enable');send('Network.setCookie',{name:'ASP.NET_SessionId',value:sid,url:'http://127.0.0.1/',domain:'127.0.0.1',path:'/',httpOnly:true});setTimeout(()=>send('Page.navigate',{url:target}),500);setTimeout(()=>{const s=send('Page.captureScreenshot',{format:'png'});setTimeout(()=>{if(pending.has(s)){fs.writeFileSync(out,Buffer.from(pending.get(s).result.data,'base64'));console.log('SAVED',out);}process.exit(0);},2500);},60000);};
setTimeout(()=>process.exit(0),75000);})();
