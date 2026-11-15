import type {SystrayOutput} from 'zebar';
import {useProvider} from '../../lib/provider-registry';
import {createSignal,onCleanup} from 'solid-js';
import {bridgeTokens} from '../../lib/install-config.generated';
import {refreshBarOrigin,toScreen} from '../../lib/screen';
import {sendCenter} from '../../lib/rice-widgets';

// Zebar's systray provider supplies each icon's image and click actions, so the tray works while
// the taskbar is hidden. The bar window is only as tall as the bar, so the control center draws
// the box under this button and reports back which icon was clicked; Zebar then performs the click.
export default function Tray(){
 // Leased from the shared registry, so moving this widget between lanes keeps its feed.
 const output=useProvider<SystrayOutput>({type:'systray'});
 const [open,setOpen]=createSignal(false),[error,setError]=createSignal('');
 let socket:WebSocket|undefined,session='';
 // The tray socket is busy long-polling, so the close request goes over the main connection.
 const close=()=>{if(session)sendCenter('tray-close:'+session);socket?.close();socket=undefined;session='';setOpen(false);};
 onCleanup(close);
 const run=async(id:string,kind:string)=>{
  const tray=output();if(!tray||!tray.icons.some(icon=>icon.id===id))return;
  if(kind==='right')await tray.onRightClick(id);else if(kind==='middle')await tray.onMiddleClick(id);else await tray.onLeftClick(id);
 };
 const toggle=async(button:HTMLButtonElement)=>{
  if(open()){close();return;}
  setOpen(true);setError('');
  await refreshBarOrigin();
  const icons=(output()?.icons??[]).slice(0,64).map(icon=>({id:icon.id,tooltip:icon.tooltip,png:btoa(Array.from(icon.iconBytes,byte=>String.fromCharCode(byte)).join(''))}));
  const rect=button.getBoundingClientRect(),topLeft=toScreen(rect.left,rect.top),bottomRight=toScreen(rect.right,rect.bottom),anchor=toScreen(rect.left+rect.width/2,window.innerHeight);
  const ws=new WebSocket('ws://127.0.0.1:6138/'+bridgeTokens.center);socket=ws;
  ws.onopen=()=>ws.send('tray-open:'+JSON.stringify({x:anchor.x,y:anchor.y,left:topLeft.x,top:topLeft.y,right:bottomRight.x,bottom:bottomRight.y,icons}));
  ws.onmessage=event=>{
   try{
    const data=JSON.parse(event.data);
    if(data.traySession){session=data.traySession;ws.send('tray-wait:'+session);}
    else if(data.trayWaiting)ws.send('tray-wait:'+session);
    else if(data.trayAction){ws.send('tray-wait:'+session);void run(data.trayAction.id,data.trayAction.kind).catch(()=>setError('That tray app did not respond.'));}
    else if(data.trayClosed){session='';close();}
    else if(data.ok===false){setError('Could not open the tray. Try again.');close();}
   }catch{close();}
  };
  ws.onerror=()=>{setError('Control center is reconnecting. Try again.');close();};
  ws.onclose=()=>{if(socket===ws){socket=undefined;session='';setOpen(false);}};
 };
 const count=()=>output()?.icons.length??0;
 const title=()=>error()||(count()?`Tray icons (${count()})`:'No tray icons');
 return <button type="button" class="nord-tray" classList={{open:open()}} title={title()} aria-label={title()} aria-expanded={open()} onDblClick={e=>e.stopPropagation()} onClick={e=>void toggle(e.currentTarget)}>
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="m7 10 5 5 5-5" stroke-linecap="round" stroke-linejoin="round"/></svg>
 </button>;
}
