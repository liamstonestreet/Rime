const fs = require('fs');
const { performance } = require('perf_hooks');
const url = fs.readFileSync('src/lib/spotlight-bridge.ts','utf8').match(/"(ws:[^"]+)"/)[1];
const socket = new WebSocket(url);
const receive = [];
socket.onmessage = event => receive.shift()?.(JSON.parse(event.data));
const ready = new Promise((resolve,reject) => {socket.onopen=resolve; socket.onerror=reject;});
const timeout = setTimeout(()=>{console.error('Bridge timed out');process.exit(1)},15000);
async function call(action,query='') {
  const start=performance.now();
  const result=await new Promise(resolve=>{receive.push(resolve);socket.send(JSON.stringify({action,query,x:350,y:60,width:360,scale:1.25}));});
  if(!result.ok) throw Error('Request failed');
  return {...result,rtt:performance.now()-start};
}
(async()=>{
 await ready;
 const initial=await call('diagnostics');
 const samples=[];
 try {
   samples.push(await call('update','s'));
   for(let i=0;i<40;i++) { samples.push(await call('update',['sp','spo','spot','spotify'][i%4])); await new Promise(r=>setTimeout(r,5)); }
   await call('update','');
   const beforeNav=await call('diagnostics');
   for(let i=0;i<120;i++) { samples.push(await call(i%2 ? 'up':'down')); await new Promise(r=>setTimeout(r,3)); }
   const final=await call('diagnostics');
   const timings=samples.map(x=>x.ms).sort((a,b)=>a-b), roundtrips=samples.map(x=>x.rtt).sort((a,b)=>a-b);
   console.log(JSON.stringify({initial,final,arrowFilterPasses:final.filters-beforeNav.filters,requests:samples.length,firstOpenHandlerMs:samples[0].ms,firstOpenRoundtripMs:samples[0].rtt,handlerMedianMs:timings[Math.floor(timings.length*.5)],handlerP95Ms:timings[Math.floor(timings.length*.95)],roundtripMedianMs:roundtrips[Math.floor(roundtrips.length*.5)],roundtripP95Ms:roundtrips[Math.floor(roundtrips.length*.95)],sameProcess:initial.pid===final.pid},null,2));
 } finally {await call('hide-dropdown');socket.close();clearTimeout(timeout);}
})().catch(e=>{console.error(e);process.exit(1)});
