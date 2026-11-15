import {test} from 'node:test';
import assert from 'node:assert/strict';
import {retainTaskIdentity, taskDropOrder} from '../src/lib/taskbar-identity.ts';
import {taskbarMotion} from '../src/lib/taskbar-motion.ts';
import {createStore, reconcile} from 'solid-js/store';

test('Solid reconciliation preserves existing proxies while updating title, focus and pin state',()=>{
  const [items,setItems]=createStore([{renderKey:'a',key:'window-a',window:{id:'a',title:'Old',hasFocus:false}},{renderKey:'b',key:'window-b',window:{id:'b'}}]);
  const first=items[0],second=items[1];
  setItems(reconcile([{renderKey:'b',key:'window-b',window:{id:'b'}},{renderKey:'a',key:'pin:a',pin:{id:'pin-a'},window:{id:'a',title:'New',hasFocus:true}}],{key:'renderKey'}));
  assert.equal(items[0],second);assert.equal(items[1],first);
  assert.equal(first.window.title,'New');assert.equal(first.window.hasFocus,true);assert.equal(first.pin.id,'pin-a');
});

test('window updates, reorder, pin, close, reopen and unpin retain the button identity', () => {
  let sequence=0;
  const allocate=()=>String(++sequence);
  const window={id:'youtube'}, pin={id:'yt-pin'};
  let items=retainTaskIdentity([{key:'youtube',window},{key:'browser',window:{id:'browser'}}],[],allocate);
  const youtube=items[0].renderKey, browser=items[1].renderKey;
  items=retainTaskIdentity([{key:'browser',window:{id:'browser'}},{key:'youtube',window:{id:'youtube'}}],items,allocate);
  assert.deepEqual(items.map(x=>x.renderKey),[browser,youtube]);
  items=retainTaskIdentity([{key:'pin:yt-pin',pin,window},{key:'browser',window:{id:'browser'}}],items,allocate);
  assert.equal(items[0].renderKey,youtube);
  items=retainTaskIdentity([{key:'pin:yt-pin',pin}],items,allocate);
  assert.equal(items[0].renderKey,youtube);
  items=retainTaskIdentity([{key:'pin:yt-pin',pin,window:{id:'new-youtube'}}],items,allocate);
  assert.equal(items[0].renderKey,youtube);
  items=retainTaskIdentity([{key:'new-youtube',window:{id:'new-youtube'}}],items,allocate);
  assert.equal(items[0].renderKey,youtube);
});

test('exact pin matches take priority over shared-window fallback; each key stays unique',()=>{
  const old=[{key:'pin:p',pin:{id:'p'},window:{id:'w'},renderKey:'existing'}];
  let sequence=0;
  const next=retainTaskIdentity([{key:'w',window:{id:'w'}},{key:'pin:p',pin:{id:'p'}}],old,()=>`new-${++sequence}`);
  assert.equal(next[1].renderKey,'existing');
  assert.equal(new Set(next.map(x=>x.renderKey)).size,2);
  assert.deepEqual(taskDropOrder([{key:'a',center:15},{key:'b',center:51},{key:'c',center:87}],'a',100),['b','c','a']);
  assert.deepEqual(taskDropOrder([{key:'b',center:15},{key:'c',center:51},{key:'a',center:87}],'a',0),['a','b','c']);
});

test('layout motion handles interruption, exit cleanup, reduced motion and disposal', async()=>{
  const active=new Set(), ghosts=new Set();let nodes=[];let reduced=false;let change;
  const root={querySelectorAll:()=>nodes,parentElement:{appendChild:node=>ghosts.add(node)}};
  function node(key,x,state='loose:running') {
    const value={dataset:{taskKey:key,taskState:state},style:{},classList:{add(){}},inert:false,
      getBoundingClientRect:()=>({left:value.x,top:10,width:30,height:30}),x,
      removeAttribute(name){if(name==='data-task-key')delete this.dataset.taskKey;},setAttribute(){},
      remove(){ghosts.delete(this);},querySelector(){return this.button;},
      animate(frames,options){const animation={frames,options,onfinish:null,cancel(){active.delete(animation);}};active.add(animation);return animation;},
      cloneNode(){return node(key,value.x,state);}
    };
    value.button={animate:value.animate};return value;
  }
  globalThis.window={matchMedia:()=>({get matches(){return reduced;},addEventListener(type,fn){change=fn;},removeEventListener(){}})};
  globalThis.document={documentElement:{dataset:{motion:'on'}}};
  globalThis.getComputedStyle=()=>({getPropertyValue:name=>name==='--motion-time'?'1':''});
  globalThis.MutationObserver=class{observe(){}disconnect(){}};
  const motion=taskbarMotion(()=>root);
  const a=node('a',10),b=node('b',46);
  motion.update(new Set(['a','b']),()=>{nodes=[a,b];});await Promise.resolve();
  assert.equal(active.size,2);
  motion.update(new Set(['b','a']),()=>{nodes=[b,a];b.x=10;a.x=46;});await Promise.resolve();
  assert.equal(active.size,2,'old animations must be cancelled during an interrupted reorder');
  motion.update(new Set(['a']),()=>{nodes=[a];a.x=10;a.dataset.taskState='pinned:running';});await Promise.resolve();
  assert.equal(ghosts.size,1);
  for(const animation of [...active])animation.onfinish?.();
  assert.equal(active.size,0);assert.equal(ghosts.size,0);
  motion.update(new Set(['a','b']),()=>{nodes=[a,b];});await Promise.resolve();
  reduced=true;change();assert.equal(active.size,0);
  motion.update(new Set(['a']),()=>{nodes=[a];});await Promise.resolve();assert.equal(active.size,0);
  reduced=false;
  motion.update(new Set(['b']),()=>{nodes=[b];});motion.dispose();await Promise.resolve();
  assert.equal(active.size,0);assert.equal(ghosts.size,0,'dispose must remove pending exit ghosts');
});
