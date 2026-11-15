import {build} from 'vite';
import solid from 'vite-plugin-solid';
import tailwind from '@tailwindcss/postcss';
import autoprefixer from 'autoprefixer';
import {fileURLToPath} from 'node:url';
import {createRequire} from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
const require=createRequire(import.meta.url);
let playwright;
try{playwright=require('playwright');}catch{playwright=require('C:/Users/liams/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const directory=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(directory,'../..'),output=path.join(root,'dist/shell-polish-check');
await build({configFile:false,root,base:'./',plugins:[solid()],resolve:{alias:{zebar:path.join(directory,'zebar.ts')}},css:{postcss:{plugins:[tailwind,autoprefixer]}},build:{outDir:output,target:'esnext',rollupOptions:{input:path.join(directory,'index.html')}}});
const browser=await playwright.chromium.launch({headless:true,channel:'msedge'});
try{
 for(const [width,scale] of [[1920,1],[2560,1.25]]){
  const page=await browser.newPage({viewport:{width,height:50},deviceScaleFactor:scale});const errors=[];
  await page.clock.install();
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/*',route=>{const url=new URL(route.request().url());if(url.hostname!=='shell.local')return route.fulfill({body:'',contentType:'text/css'});const file=path.join(output,decodeURIComponent(url.pathname));return route.fulfill({body:fs.readFileSync(file),contentType:file.endsWith('.html')?'text/html':file.endsWith('.css')?'text/css':'text/javascript'});});
  await page.addInitScript(()=>{
   window.shellCalls=[];window.bridgeMessages=[];window.centerSockets=[];window.searchMessages=[];window.searchSockets=[];
   window.WebSocket=class {
    static CONNECTING=0;static OPEN=1;static CLOSED=3;readyState=0;
    constructor(url){this.url=url;if(url.includes(':6138'))window.centerSockets.push(this);else window.searchSockets.push(this);queueMicrotask(()=>{this.readyState=1;this.onopen?.({});});}
    emit(value){this.onmessage?.({data:JSON.stringify(value)});}
    send(message){if(!this.url.includes(':6138')){const request=JSON.parse(message);window.searchMessages.push(request);this.emit({ok:true,id:request.id,action:request.action,query:request.action==='complete'?'/calc ':request.query});return;}window.bridgeMessages.push(message);if(message==='subscribe'){
     this.emit({font:{family:'Segoe UI',revision:0,data:null}});
     this.emit({appearance:{transparency:15,wallpaperColors:false,grainOpacity:0,radius:75,frame:14,motion:100,motionStyle:'smooth',edgeStyle:'flat',bars:[{x:0,y:0,width:Math.round(innerWidth*devicePixelRatio),height:Math.ceil(innerHeight*devicePixelRatio)}]}});
     this.emit({workspaceRules:{action:'status',enabled:true}});this.emit({audio:{name:'Headphones',volume:68,isMuted:false}});
    }}
    close(){this.readyState=3;this.onclose?.({});}
   };
  });
  await page.goto('http://shell.local/checks/shell-polish/index.html');await page.locator('.nord-workspace-button').first().waitFor();
  await page.waitForFunction(()=>window.centerSockets.length&&window.bridgeMessages.includes('subscribe'));
  assert(await page.evaluate(()=>Math.abs(parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--edge-snap'))-(innerHeight-Math.floor(innerHeight*devicePixelRatio)/devicePixelRatio))<.001),'Bar edge must use the physical bar bounds at this scale');
  const appGroup=page.locator('.nord-task-icons.has-apps');await appGroup.waitFor();assert.notEqual(await appGroup.evaluate(el=>getComputedStyle(el).backgroundColor),'rgba(0, 0, 0, 0)','App icons should share a soft group background');
  await appGroup.locator('button').first().click({button:'right'});
  let taskMenu=await page.evaluate(()=>JSON.parse(window.bridgeMessages.filter(m=>m.startsWith('task-context:')).at(-1).slice(13)));
  assert.equal(taskMenu.workspace,'1');assert.equal(taskMenu.id,'sample-app');assert.equal(taskMenu.handle,100);
  await page.evaluate(()=>window.centerSockets[0].emit({workspacePins:[{id:'closed-test',workspace:'1',name:'Closed test',processPattern:'^ClosedTest$'}]}));
  await page.getByRole('button',{name:'Launch Closed test',exact:true}).click({button:'right'});
  taskMenu=await page.evaluate(()=>JSON.parse(window.bridgeMessages.filter(m=>m.startsWith('task-context:')).at(-1).slice(13)));
  assert.equal(taskMenu.pinId,'closed-test');assert.equal(taskMenu.workspace,'1');assert.equal(taskMenu.handle,0);
  const runningTask=appGroup.locator('[data-order-key="app:discord:0"]'),closedTask=appGroup.locator('[data-order-key="pin:closed-test"]');
  const runningBox=await runningTask.boundingBox(),closedBox=await closedTask.boundingBox();
  const focusBefore=await page.evaluate(()=>window.bridgeMessages.filter(m=>m.startsWith('task-focus:')).length);
  await page.mouse.move(runningBox.x+runningBox.width/2,runningBox.y+runningBox.height/2);await page.mouse.down();await page.mouse.move(closedBox.x+2,closedBox.y+closedBox.height/2,{steps:8});await page.mouse.up();
  assert.equal(await page.evaluate(()=>window.bridgeMessages.filter(m=>m.startsWith('task-focus:')).length),focusBefore,'Dragging must not focus the app');  const savedOrder=await page.evaluate(()=>JSON.parse(window.bridgeMessages.filter(m=>m.startsWith('task-order:')).at(-1).slice(11)));
  assert.equal(savedOrder.workspace,'1');assert.deepEqual(savedOrder.keys,['app:discord:0','pin:closed-test']);
  await page.evaluate(order=>window.centerSockets[0].emit({taskOrders:{'1':order}}),savedOrder.keys);
  assert.equal(await appGroup.locator('button').first().getAttribute('data-order-key'),'app:discord:0');
  const orderCount=await page.evaluate(()=>window.bridgeMessages.filter(m=>m.startsWith('task-order:')).length);
  const from=await runningTask.boundingBox(),to=await closedTask.boundingBox();await page.mouse.move(from.x+from.width/2,from.y+from.height/2);await page.mouse.down();await page.mouse.move(to.x+to.width-2,to.y+to.height/2,{steps:8});await page.keyboard.press('Escape');await page.mouse.up();
  assert.equal(await appGroup.locator('button').first().getAttribute('data-order-key'),'app:discord:0','Escape must restore the saved order');assert.equal(await page.evaluate(()=>window.bridgeMessages.filter(m=>m.startsWith('task-order:')).length),orderCount,'Cancelled drag must not save');
  await runningTask.click();assert.equal(await page.evaluate(()=>window.bridgeMessages.filter(m=>m.startsWith('task-focus:')).length),focusBefore+1,'Normal click must still focus');  await page.getByRole('button',{name:'Launch Closed test',exact:true}).click();assert(await page.evaluate(()=>window.bridgeMessages.includes('pin-launch:closed-test')));
  await page.evaluate(()=>window.centerSockets[0].emit({workspacePins:[]}));
  const actions=page.getByRole('group',{name:'Quick actions'});await actions.waitFor();assert.equal(await actions.getByRole('button').count(),3);
  assert.equal(await page.locator('.nord-layout-right .nord-layout-slot').first().getAttribute('data-widget'),'quickActions','Quick actions must migrate into the space before right widgets');
  await page.getByRole('button',{name:"Open today's reflection",exact:true}).click();assert(await page.evaluate(()=>window.bridgeMessages.includes('quick-action:reflection')));
  await page.getByRole('button',{name:'Open GitHub',exact:true}).click();assert(await page.evaluate(()=>window.bridgeMessages.includes('quick-action:github')));
  const discord=page.getByRole('button',{name:'Mute Discord microphone',exact:true});await discord.hover();assert(await page.evaluate(()=>window.bridgeMessages.includes('quick-discord-status')));
  await discord.click();assert(await page.evaluate(()=>window.bridgeMessages.includes('quick-discord-mute')));
  assert(!(await page.evaluate(()=>window.bridgeMessages)).some(m=>m.startsWith('quick-discord-menu:')),'Discord must be a direct toggle, without hover menus');
  await page.evaluate(()=>{window.centerSockets[0].emit({discordVoice:{available:true,muted:true,deafened:false}});window.centerSockets[0].emit({quickAction:{action:'discord',ok:true}});});
  await page.getByRole('button',{name:'Unmute Discord microphone',exact:true}).waitFor();assert.equal(await page.locator('.nord-discord-action').getAttribute('aria-pressed'),'true');assert(await page.locator('.nord-discord-action').evaluate(el=>el.classList.contains('is-muted')));
  await page.clock.runFor(250);assert.equal(await page.locator('.discord-mute-slash').evaluate(el=>{getComputedStyle(el).strokeDashoffset;el.getAnimations().forEach(a=>a.finish());return parseFloat(getComputedStyle(el).strokeDashoffset);}),0,'Mute slash should finish drawing');
  const rules=page.getByRole('button',{name:'Automatic workspace assignments',exact:true});assert.equal(await rules.evaluate(el=>getComputedStyle(el).backgroundColor),'rgba(0, 0, 0, 0)','Wayfinder icon should not have a filled highlight');assert.equal(await rules.locator('svg rect').count(),0,'App rules should use the new folded-map icon');
  const playerWidths=()=>page.locator('.nord-player').evaluateAll(elements=>elements.map(el=>el.getBoundingClientRect().width));
  const activeWidths=await playerWidths();assert.equal(activeWidths.length,2);await page.evaluate(()=>window.fixtureMedia(false));await page.waitForFunction(()=>document.querySelectorAll('.nord-player.has-session').length===0);await page.clock.runFor(500);assert.deepEqual(await playerWidths(),activeWidths,'Spotify and Helium must keep their normal width with no session');assert.equal(await page.getByText('Nothing playing',{exact:true}).count(),2);await page.evaluate(()=>window.fixtureMedia(true));await page.waitForFunction(()=>document.querySelectorAll('.nord-player.has-session').length===2);
  assert.equal(await page.locator('.nord-volume span').textContent(),'68');
  const clock=page.getByRole('button',{name:'Control center',exact:true});await clock.click();
  await page.waitForFunction(()=>window.bridgeMessages.some(m=>m.startsWith('center-pin:')));
  assert(!/\d{2}\/\d{2}\/\d{2}/.test(await clock.textContent()),'Clock should use compact date');
  await page.evaluate(()=>window.centerSockets[0].emit({audio:{name:'Headphones',volume:42,isMuted:true}}));
  await page.getByRole('button',{name:'Unmute',exact:true}).waitFor();assert.equal(await page.locator('.nord-volume span').textContent(),'42');
  await page.getByRole('button',{name:'Unmute',exact:true}).click();assert(await page.evaluate(()=>window.bridgeMessages.includes('audio-mute:0')));
  // Appearance changes must leave the separate font stylesheet intact.
  const surfaceColors=()=>page.locator('.nord-search-button,.nord-task-icons.has-apps').evaluateAll(els=>els.map(el=>getComputedStyle(el).backgroundColor));
  await page.mouse.move(0,0);
  const originalSurfaces=await surfaceColors();
  await page.evaluate(()=>window.centerSockets[0].emit({appearance:{transparency:15,surfaceContrast:30,wallpaperColors:false,radius:75,frame:14,motion:100,edgeStyle:'flat'}}));
  await page.clock.runFor(500);
  assert.equal(await page.evaluate(()=>getComputedStyle(document.documentElement).getPropertyValue('--surface-idle').trim()),'8.8%');
  const liftedSurfaces=await surfaceColors();
  assert.equal(liftedSurfaces.length,2);liftedSurfaces.forEach((color,i)=>assert.notEqual(color,originalSurfaces[i],'Contrast must change search and app-group fills'));
  const hoverWidgets=page.locator('.nord-clock-button,.nord-search-button,.nord-direction,.nord-tray,.nord-power,.nord-volume,.nord-vpn,.workspace-rules-button,.nord-status,.nord-task-icon,.nord-player,.nord-quick-action');
  const hoverCheckStyle=await page.addStyleTag({content:'.nord-frost-bar * {transition:none !important}'});
  assert.equal(await page.locator('.nord-status').count(),2,'Exercise both CPU and memory');
  let sharedHover;
  for(let i=0;i<await hoverWidgets.count();i++){
   const widget=hoverWidgets.nth(i);await widget.hover({force:true});
   const color=await widget.evaluate(el=>getComputedStyle(el).backgroundColor);
   sharedHover??=color;assert.equal(color,sharedHover,'Every widget must share the same contrast-aware hover fill: '+await widget.getAttribute('class'));
  }
  await page.mouse.move(0,0);await page.clock.runFor(500);
  await page.evaluate(()=>window.centerSockets[0].emit({appearance:{transparency:30,wallpaperColors:false,radius:75,frame:14,motion:100,edgeStyle:'flat'}}));
  await page.clock.runFor(500);assert.deepEqual(await surfaceColors(),originalSurfaces,'Missing/zero contrast restores original fills');
  await hoverCheckStyle.evaluate(el=>el.remove());
  assert.equal(await page.evaluate(()=>getComputedStyle(document.documentElement).getPropertyValue('--rice-font').trim()),'"Segoe\\ UI", sans-serif');
  const search=page.getByRole('textbox',{name:'Search apps',exact:true});await search.fill('/cal');await page.clock.runFor(30);await search.press('Tab');await page.waitForFunction(()=>document.querySelector('input[aria-label="Search apps"]').value==='/calc ');
  await page.evaluate(()=>window.searchSockets[0].emit({ok:true,id:0,action:'complete',query:'/stale '}));assert.equal(await search.inputValue(),'/calc ','Old responses must not overwrite the current query');
  await page.evaluate(()=>window.searchSockets[0].emit({completion:true,source:'0_0',query:'/files '}));await page.waitForFunction(()=>document.querySelector('input[aria-label="Search apps"]').value==='/files ');
  await page.evaluate(()=>window.searchSockets[0].emit({completion:true,source:'another-monitor',query:'/wrong '}));assert.equal(await search.inputValue(),'/files ','Completion should affect its originating monitor only');
  await search.fill('sqrt(144)');await search.press('Control+Enter');assert.equal(await page.evaluate(()=>window.searchMessages.at(-1).action),'web');
  await search.press('Escape');
  for(const widget of await page.locator('.nord-status').all()){
    await widget.hover();await page.clock.runFor(350);const box=await widget.locator('.nord-status-value').boundingBox();const label=await widget.locator('.nord-status-label').boundingBox();assert(Math.abs(box.x+box.width/2-label.x-label.width/2)<.5,'Metric hover text must be centered over the meter');
  }
  // Advance virtual time to verify the ten-second heartbeat without waiting in real time.
  const heartbeatsBefore=await page.evaluate(()=>window.bridgeMessages.filter(m=>m==='ping').length);
  await page.clock.runFor(21000);
  const heartbeats=(await page.evaluate(()=>window.bridgeMessages.filter(m=>m==='ping').length))-heartbeatsBefore;
  assert(heartbeats>=2&&heartbeats<=3,`Heartbeat should be ten seconds, not one second (received ${heartbeats})`);
  // Reconnection must subscribe again, with no old per-monitor status polling.
  await page.evaluate(()=>window.centerSockets[0].close());await page.clock.runFor(1100);await page.waitForFunction(()=>window.centerSockets.length===2&&window.bridgeMessages.filter(m=>m==='subscribe').length===2);
  assert(!(await page.evaluate(()=>window.bridgeMessages)).some(m=>['widgets','focus-status','audio-status','vpn-status','appearance'].includes(m)));
  await page.evaluate(()=>window.centerSockets.at(-1).emit({focus:{active:true,themeActive:false,session:true,phase:'work',paused:false,waiting:false,remainingSeconds:1499,goal:'Finish three problems',completedSessions:0,notice:'',endsUtc:'',settings:{disabledWorkspaces:[],fallbackWorkspace:'1',edge:'#315C45',background:'#151816',accent:'#81A990'}}}));
  await page.waitForFunction(()=>document.querySelector('.nord-focus-countdown')?.textContent==='Focus 24:59');
  assert.equal(await page.locator('.nord-focus-countdown').getAttribute('title'),'Finish three problems');
  assert.equal(await page.evaluate(()=>document.documentElement.classList.contains('locked-in')),false,'Gentle focus must keep the current palette');
  await clock.click();assert((await page.evaluate(()=>window.bridgeMessages)).some(m=>m.startsWith('focus-open:')),'Active countdown must open the Focus tab');
  await page.evaluate(()=>{const socket=window.centerSockets.at(-1),focus={active:false,themeActive:false,session:true,phase:'break',paused:false,waiting:true,remainingSeconds:600,goal:'Finish three problems',settings:{disabledWorkspaces:[],fallbackWorkspace:'1',edge:'#315C45',background:'#151816',accent:'#81A990'}};socket.emit({focus});});
  await page.waitForFunction(()=>document.querySelector('.nord-focus-countdown')?.textContent==='Break ready');
  assert.deepEqual(errors,[]);
  await page.mouse.move(0,49);
  const borders=await page.locator('.nord-volume').evaluate(el=>({border:getComputedStyle(el).borderColor,background:getComputedStyle(el).backgroundColor}));
  assert.equal(borders.border,'rgba(0, 0, 0, 0)','Idle volume should not retain a capsule outline');
  await page.screenshot({path:path.join(directory,`bar-${width}-${scale}.png`)});
  // Every monitor uses absolute time rather than starting a fresh animation on arrival.
  await page.clock.pauseAt(new Date(await page.evaluate(()=>Date.now()+1000)));
  await page.evaluate(()=>{
   const startsAt=Date.now()+180;window.themeFixture={revision:'theme-one',startsAt,duration:450,from:{'--edge':'#000000'},to:{'--edge':'#ffffff'}};
   window.centerSockets.at(-1).emit({appearance:{transparency:30,wallpaperColors:true,palette:window.themeFixture.to,transition:window.themeFixture}});
  });
  const edge=()=>page.evaluate(()=>parseInt(getComputedStyle(document.documentElement).getPropertyValue('--edge').trim().slice(1,3),16));
  assert.equal(await edge(),0,'Theme must wait for the common start');
  await page.clock.runFor(405);assert(Math.abs(await edge()-128)<15,'Midpoint must use the common cosine ease: '+JSON.stringify(await page.evaluate(()=>({edge:getComputedStyle(document.documentElement).getPropertyValue('--edge'),elapsed:Date.now()-window.themeFixture.startsAt}))));
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{transparency:30,wallpaperColors:true,palette:window.themeFixture.to,transition:window.themeFixture}}));
  await page.clock.runFor(300);assert.equal(await edge(),255,'Duplicate updates must not restart the transition');
  assert.equal(await page.evaluate(()=>document.documentElement.classList.contains('theme-transition')),false,'Palette animation must stop at completion');
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{transparency:30,wallpaperColors:true,palette:{'--edge':'#000000'},transition:{revision:'stale',startsAt:window.themeFixture.startsAt-100,duration:450,from:null,to:{'--edge':'#000000'}}}}));
  assert.equal(await edge(),255,'Out-of-order snapshots must never restore an old palette');
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{transparency:30,wallpaperColors:true,palette:{'--edge':'#000000'},transition:{revision:'late',startsAt:Date.now()-225,duration:450,from:{'--edge':'#ffffff'},to:{'--edge':'#000000'}}}}));
  assert(Math.abs(await edge()-128)<3,'Late receivers must join at the existing midpoint');
  await page.clock.runFor(300);assert.equal(await edge(),0);
  // The user's current arrangement has a deliberate gap after CC; preserve the player widths.
  // Late initial snapshots and old socket callbacks must never restore the default blue palette.
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{revision:1000,transparency:30,wallpaperColors:true,palette:{'--edge':'#10ad44'},transition:null}}));assert.equal(await edge(),16);
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{revision:999,transparency:30,wallpaperColors:false,palette:null,transition:null}}));assert.equal(await edge(),16,'Late startup snapshot must not clear the newer palette');
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{revision:1001,transparency:30,wallpaperColors:true,palette:null,transition:null}}));assert.equal(await edge(),16,'Temporary missing palette while matching is enabled must retain the last good colors');
  await page.evaluate(()=>{window.oldThemeMeta={revision:'same-descriptor',startsAt:Date.now(),duration:0,from:null,to:{'--edge':'#0000ff'}};window.centerSockets.at(-1).emit({appearance:{revision:1002,transparency:30,wallpaperColors:true,palette:{'--edge':'#10ad44'},transition:window.oldThemeMeta}});});assert.equal(await edge(),16,'Snapshot palette must win over stale transition metadata');
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{revision:1003,transparency:30,wallpaperColors:false,palette:null,transition:window.oldThemeMeta}}));assert.notEqual(await edge(),16,'Explicitly disabling matching must still restore the normal theme');
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{revision:1004,transparency:30,wallpaperColors:true,palette:{'--edge':'#d07030'},transition:window.oldThemeMeta}}));assert.equal(await edge(),208);
  await page.evaluate(()=>window.centerSockets.at(-1).emit({appearance:{revision:1005,transparency:30,wallpaperColors:true,palette:{'--edge':'#10ad44'},transition:window.oldThemeMeta}}));assert.equal(await edge(),16,'Palette correction must apply even when transition ID is unchanged');
  await page.evaluate(()=>window.centerSockets[0].emit({appearance:{revision:99999,transparency:30,wallpaperColors:false,palette:null}}));assert.equal(await edge(),16,'Disconnected socket must not repaint the current bar');
  await page.evaluate(()=>{
   window.centerSockets.at(-1).emit({volume:false,spotify:true,helium:true,tray:true,rules:true,search:true,workspaces:true,direction:false,memory:true,cpu:true,vpn:false,power:true,layout:{left:['workspaces','search','tray'],right:['volume','spotify','helium','memory','cpu','vpn','direction','rules','power']}});
   document.querySelector('.nord-layout-right').scrollLeft=0;
  });
  await page.clock.runFor(500);
  const quickBox=await actions.boundingBox(),clockBox=await clock.boundingBox(),powerBox=await page.getByRole('button',{name:'Open power menu',exact:true}).boundingBox(),playerBox=await page.locator('.nord-player').first().boundingBox();
  assert(quickBox.x>=clockBox.x+clockBox.width&&quickBox.x+quickBox.width<=playerBox.x,'Quick actions must use the gap between CC and media widgets');
  assert(Math.abs((quickBox.x+quickBox.width/2)-(clockBox.x+clockBox.width+playerBox.x)/2)<1,'Quick actions must be centered precisely between CC and the first right widget');
  assert(powerBox.x+powerBox.width<=width,'The added actions must not push Power off-screen in the current layout');
  await page.screenshot({path:path.join(directory,`bar-actions-${width}-${scale}.png`)});
  console.log(`PASS: ${width}px at ${scale*100}%: initial push, live audio/mute, clock pin, separate font, reconnect, idle/full media widths, wayfinder icon, gentle Focus countdown/goal/ready state and routing, no repeated status requests or runtime errors.`);
  await page.close();
 }
}finally{await browser.close();}
