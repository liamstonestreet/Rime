import {createStore,reconcile} from 'solid-js/store';
import {pruneTaskIcons} from '../../lib/rice-widgets';
import {taskbarMotion} from '../../lib/taskbar-motion';
import {retainTaskIdentity,taskDropOrder} from '../../lib/taskbar-identity';
import {toScreen} from '../../lib/screen';
import {createEffect,createMemo,createSignal,For,on,onMount,onCleanup,Show,untrack} from 'solid-js';
import type {Window as GlazeWindow, Workspace, SplitContainer} from 'glazewm';
import {useProviders} from '../../lib/providers-context';
import {taskIcons,requestTaskIcon,bridgeConnected,taskAction,sendCenter,workspacePins,taskOrders,setTaskOrders,launchPin,pinError,workspaceDisabled,WorkspacePin} from '../../lib/rice-widgets';
function collect(workspace:Workspace|undefined){const result:GlazeWindow[]=[];const visit=(node:Workspace|SplitContainer|GlazeWindow)=>{if(node.type==='window')result.push(node as GlazeWindow);else (node as Workspace).children.forEach(visit);};if(workspace)visit(workspace);return result.sort((a,b)=>a.id.localeCompare(b.id));}
function matches(pin:WorkspacePin,w:GlazeWindow){try{return new RegExp(pin.processPattern,'i').test(w.processName.replace(/\.exe$/i,''))&&(!pin.titlePattern||new RegExp(pin.titlePattern,'i').test(w.title));}catch{return false;}}
export default function WorkspaceTasks(){
 const {glazewm}=useProviders();
 const workspaceWindows=createMemo(()=>new Map(glazewm()?.allWorkspaces.map(workspace=>[workspace.name,collect(workspace)])??[]));
 const windows=createMemo(()=>workspaceWindows().get(glazewm()?.displayedWorkspace.name??'')??[]);
 const allWindows=createMemo(()=>Array.from(workspaceWindows().values()).flat());
 createEffect(on(allWindows,next=>{if(glazewm())pruneTaskIcons(next.map(window=>window.handle));}));
 const rawItems=createMemo(()=>{
  const local=windows(),claimed=new Set<string>();
  const pins=workspacePins().filter(p=>p.workspace===glazewm()?.displayedWorkspace.name);
  const pinned=pins.map(pin=>{const window=local.find(w=>matches(pin,w))??allWindows().find(w=>matches(pin,w));if(window)claimed.add(window.id);return {key:'pin:'+pin.id,pin,window,elsewhere:!!window&&!local.some(w=>w.id===window.id)};});
  return [...pinned,...local.filter(w=>!claimed.has(w.id)).map(window=>({key:window.id,pin:undefined,window,elsewhere:false}))];
 });
 const [dragging,setDragging]=createSignal<string|null>(null);
 const [preview,setPreview]=createSignal<string[]|null>(null);
 let dragged=false;let stripElement!:HTMLDivElement;let pointer:{id:number;x:number;y:number;key:string;workspace:string;node:HTMLElement}|undefined;
 const keyedItems=createMemo(()=>{
  const counts=new Map<string,number>();
  return rawItems().map(item=>{const app=item.window?.processName.replace(/\.exe$/i,'').toLowerCase()??'';const ordinal=counts.get(app)??0;counts.set(app,ordinal+1);return {...item,orderKey:item.pin?'pin:'+item.pin.id:'app:'+app+':'+ordinal};});
 });
 const items=createMemo(()=>{
  const keyed=[...keyedItems()];
  const order=preview()??taskOrders()[glazewm()?.displayedWorkspace.name??'']??[];
  const ranks=new Map(order.map((key,index)=>[key,index]));
  return keyed.sort((a,b)=>(ranks.get(a.orderKey)??order.length)-(ranks.get(b.orderKey)??order.length));
 });
 type Item=ReturnType<typeof items>[number]&{renderKey:string};
 const [rendered,setRendered]=createStore<Item[]>([]);
 let nextIdentity=0;
 const motion=taskbarMotion(()=>stripElement);
 onCleanup(()=>motion.dispose());
 createEffect(on(items,next=>{
  const previous=untrack(()=>[...rendered]);
  const resolved=retainTaskIdentity(next,previous,()=>String(++nextIdentity));
  const changed=resolved.length!==previous.length||resolved.some((item,index)=>{const old=previous[index];return !old||old.renderKey!==item.renderKey||!!old.pin!==!!item.pin||!!old.window!==!!item.window;});
  const update=()=>setRendered(reconcile(resolved,{key:'renderKey'}));
  if(changed)motion.update(new Set(resolved.map(item=>item.renderKey)),update);else update();
 }));
 const finishDrag=(save:boolean)=>{const workspace=glazewm()?.displayedWorkspace.name,visible=preview();if(save&&workspace&&visible){const remaining=[...visible],active=new Set(visible);const keys=(taskOrders()[workspace]??[]).map(key=>active.has(key)?remaining.shift()!:key).concat(remaining);setTaskOrders(old=>({...old,[workspace]:keys}));sendCenter('task-order:'+JSON.stringify({workspace,keys}));}setDragging(null);setPreview(null);};
 function endPointer(save:boolean){if(!pointer)return;const {id,node}=pointer;pointer=undefined;finishDrag(save);if(node.hasPointerCapture(id))node.releasePointerCapture(id);}
 const beginPointer=(event:PointerEvent,key:string)=>{if(event.button!==0||!bridgeConnected())return;const workspace=glazewm()?.displayedWorkspace.name;if(!workspace)return;dragged=false;const node=event.currentTarget as HTMLButtonElement;pointer={id:event.pointerId,x:event.clientX,y:event.clientY,key,workspace,node};node.setPointerCapture(event.pointerId);};
 const inside=(x:number,y:number)=>{const box=stripElement.getBoundingClientRect();return x>=box.left&&x<=box.right&&y>=box.top-8&&y<=box.bottom+8;};
 const movePointer=(event:PointerEvent)=>{const active=pointer;if(!active||event.pointerId!==active.id)return;if(!(event.buttons&1)){endPointer(false);return;}if(!dragging()){if(Math.hypot(event.clientX-active.x,event.clientY-active.y)<5)return;dragged=true;active.node=stripElement;stripElement.setPointerCapture(active.id);setDragging(active.key);setPreview(items().map(item=>item.orderKey));}event.preventDefault();if(!inside(event.clientX,event.clientY))return;
  const boundsForDrop=stripElement.getBoundingClientRect();const nodes=Array.from(stripElement.querySelectorAll<HTMLElement>(':scope > [data-task-key]')).map(el=>({key:el.dataset.orderKey!,center:el.offsetLeft+el.offsetWidth/2}));const keys=taskDropOrder(nodes,active.key,event.clientX-boundsForDrop.left+stripElement.scrollLeft);const current=preview();if(!current||keys.length!==current.length||keys.some((key,i)=>key!==current[i]))setPreview(keys);
  const bounds=stripElement.getBoundingClientRect();if(event.clientX>bounds.right-20)stripElement.scrollLeft+=12;else if(event.clientX<bounds.left+20)stripElement.scrollLeft-=12;
 };
 createEffect(on(()=>glazewm()?.displayedWorkspace.name,(workspace,previous)=>{if(workspace===previous)return;endPointer(false);setDragging(null);setPreview(null);}));
 onMount(()=>{const cancel=(event:KeyboardEvent)=>{if(event.key==='Escape'&&pointer){event.preventDefault();endPointer(false);}};const blur=()=>endPointer(false);window.addEventListener('keydown',cancel);window.addEventListener('blur',blur);onCleanup(()=>{endPointer(false);window.removeEventListener('keydown',cancel);window.removeEventListener('blur',blur);});});
 const requested=new Set<number>();const iconRetries=new Map<number,ReturnType<typeof setTimeout>[]>();
 onCleanup(()=>{for(const timers of iconRetries.values())for(const timer of timers)clearTimeout(timer);iconRetries.clear();});
 createEffect(()=>{if(!bridgeConnected()){requested.clear();for(const timers of iconRetries.values())for(const timer of timers)clearTimeout(timer);iconRetries.clear();return;}const active=new Set(keyedItems().flatMap(item=>item.window?[item.window.handle]:[]));for(const handle of requested)if(!active.has(handle)){requested.delete(handle);for(const timer of iconRetries.get(handle)??[])clearTimeout(timer);iconRetries.delete(handle);}for(const item of keyedItems()){const w=item.window;if(w&&!requested.has(w.handle)){requested.add(w.handle);requestTaskIcon(w.handle);iconRetries.set(w.handle,[400,1600].map(delay=>setTimeout(()=>{if(bridgeConnected()&&requested.has(w.handle))requestTaskIcon(w.handle);},delay)));}}});
 const [pending,setPending]=createSignal<Record<string,boolean>>({});const timers=new Set<ReturnType<typeof setTimeout>>();
 onCleanup(()=>timers.forEach(clearTimeout));
 const open=(item:ReturnType<typeof items>[number])=>{const w=item.window;if(w){if(!taskAction('focus',w.id,w.handle))void glazewm()?.runCommand('focus --container-id '+w.id);return;}if(item.pin&&launchPin(item.pin.id)){setPending(p=>({...p,[item.key]:true}));const timer=setTimeout(()=>{setPending(p=>({...p,[item.key]:false}));timers.delete(timer);},1800);timers.add(timer);}};
 const [bounds,setBounds]=createSignal({left:0,width:0});
 // Centre the icons between the right edge of the last visible left-side widget and the clock.
 // The left lane reserves more width than its widgets use, so measure the widgets themselves and
 // re-measure when any of them resize or are toggled.
 onMount(()=>{const lane=document.querySelector('.nord-layout-left'),clock=document.querySelector('.nord-clock-button');
 const measure=()=>{const laneBox=lane?.getBoundingClientRect();let end=laneBox?.left??window.innerWidth*.34;for(const slot of Array.from(lane?.querySelectorAll('.nord-layout-slot')??[]))end=Math.max(end,slot.getBoundingClientRect().right);if(laneBox)end=Math.min(end,laneBox.right);const start=clock?.getBoundingClientRect().left??window.innerWidth*.5;setBounds({left:end,width:Math.max(0,start-end)});};
 const observer=new ResizeObserver(measure),watched=new Set<Element>();const watch=()=>{for(const slot of Array.from(lane?.querySelectorAll('.nord-layout-slot')??[]))if(!watched.has(slot)){watched.add(slot);observer.observe(slot);}measure();};
 const mutations=new MutationObserver(watch);if(lane){mutations.observe(lane,{childList:true,subtree:true});lane.addEventListener('scroll',measure,{passive:true});}if(clock)observer.observe(clock);observer.observe(document.body);window.addEventListener('resize',measure);watch();onCleanup(()=>{observer.disconnect();mutations.disconnect();lane?.removeEventListener('scroll',measure);window.removeEventListener('resize',measure);});});
 return <div class="nord-workspace-tasks" role="toolbar" aria-label="Current workspace apps" style={{left:bounds().left+'px',width:bounds().width+'px'}}><div ref={stripElement} class="nord-task-icons" onDragStart={e=>e.preventDefault()} onPointerMove={movePointer} onPointerUp={e=>{if(pointer?.id===e.pointerId)endPointer(!!dragging()&&inside(e.clientX,e.clientY));}} onPointerCancel={()=>endPointer(false)} onLostPointerCapture={e=>{if(pointer?.node===e.target)endPointer(false);}} classList={{'has-apps':rendered.length>0}} onWheel={e=>{const strip=e.currentTarget;if(strip.scrollWidth>strip.clientWidth&&Math.abs(e.deltaY)>Math.abs(e.deltaX)){strip.scrollLeft+=e.deltaY;e.preventDefault();}}}><For each={rendered}>{item=>{
  const label=()=>item.pin?.name||item.window?.title||item.window?.processName||'App';
  const icon=()=>item.pin?.icon||(item.window?taskIcons()[item.window.handle]:undefined);
  const title=()=>pinError()&&item.pin&&pinError()!.id===item.pin.id?pinError()!.message:(!item.window?'Launch ':item.elsewhere?'Switch to ':'Focus ')+label();
  return <div class="nord-task-slot" data-task-key={item.renderKey} data-order-key={item.orderKey} data-task-state={(item.pin?'pinned':'loose')+':'+(item.window?'running':'closed')}><button type="button" class="nord-task-icon" classList={{'is-dragging':dragging()===item.orderKey,'is-focused':!!item.window?.hasFocus,'is-closed':!item.window,'is-elsewhere':item.elsewhere}} disabled={!!pending()[item.key]||!!item.pin&&workspaceDisabled(item.pin.workspace)} title={title()} aria-label={title()} onDblClick={e=>e.stopPropagation()} draggable={false} onPointerDown={e=>beginPointer(e,item.orderKey)} onClick={e=>{if(!dragged||e.detail===0)open(item);}} onContextMenu={e=>{e.preventDefault();e.stopPropagation();const p=toScreen(e.clientX,e.clientY);sendCenter('task-context:'+JSON.stringify({id:item.window?.id,handle:item.window?.handle??0,pinId:item.pin?.id,workspace:glazewm()?.displayedWorkspace.name,x:p.x,y:p.y}));}}><Show when={icon()} fallback={<span>{label().slice(0,1).toUpperCase()}</span>}><img src={icon()} alt="" draggable={false}/></Show></button></div>;
 }}</For></div></div>;
}
