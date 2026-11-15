import {build} from 'vite';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import fs from 'node:fs';
import assert from 'node:assert/strict';
const require=createRequire(import.meta.url);let playwright;
try{playwright=require('playwright');}catch{playwright=require('C:/Users/liams/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const directory=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(directory,'../..'),out=path.join(root,'dist/theme-sync-check');
await build({configFile:false,root,base:'./',build:{outDir:out,rollupOptions:{input:path.join(directory,'index.html')}}});
const browser=await playwright.chromium.launch({headless:true,channel:'msedge'});
try{
 const page=await browser.newPage();await page.emulateMedia({reducedMotion:'no-preference'});
 await page.clock.install({time:new Date('2026-10-31T12:00:00Z')});await page.clock.pauseAt(new Date('2026-10-31T12:00:01Z'));
 await page.route('**/*',route=>{const url=new URL(route.request().url());const file=path.join(out,decodeURIComponent(url.pathname));return route.fulfill({body:fs.readFileSync(file),contentType:file.endsWith('.html')?'text/html':'text/javascript'});});
 await page.goto('http://theme.local/checks/theme-sync/index.html');await page.waitForFunction(()=>!!window.applyTheme);
 await page.evaluate(()=>{window.data={revision:'one',startsAt:Date.now()+180,duration:450,from:{'--edge':'#000000'},to:{'--edge':'#ffffff'}};window.applyTheme(window.data.to,window.data);});
 const value=()=>page.evaluate(()=>({color:getComputedStyle(document.documentElement).getPropertyValue('--edge').trim(),elapsed:Date.now()-window.data.startsAt}));
 assert.equal((await value()).color,'#000000');await page.clock.runFor(405);
 const mid=await value();assert(Math.abs(parseInt(mid.color.slice(1,3),16)-128)<15,JSON.stringify(mid));
 await page.evaluate(()=>window.applyTheme(window.data.to,window.data));await page.clock.runFor(300);assert.equal((await value()).color,'#ffffff');
 await page.evaluate(()=>window.applyTheme({'--edge':'#000000'},{...window.data,revision:'stale',startsAt:window.data.startsAt-1}));assert.equal((await value()).color,'#ffffff');
 await page.evaluate(()=>{window.data={revision:'late',startsAt:Date.now()-225,duration:450,from:{'--edge':'#ffffff'},to:{'--edge':'#000000'}};window.applyTheme(window.data.to,window.data);});
 assert(Math.abs(parseInt((await value()).color.slice(1,3),16)-128)<3);await page.clock.runFor(300);assert.equal((await value()).color,'#000000');
 assert.equal(await page.evaluate(()=>document.documentElement.classList.contains('theme-transition')),false);
 await page.evaluate(()=>{
  const canvas=document.createElement('canvas');canvas.width=canvas.height=2;const context=canvas.getContext('2d');
  context.fillStyle='red';context.fillRect(0,0,2,2);window.frostA=canvas.toDataURL('image/jpeg');context.fillStyle='blue';context.fillRect(0,0,2,2);window.frostB=canvas.toDataURL('image/jpeg');
  window.data={revision:'frost',startsAt:Date.now()+180,duration:450,from:null,to:null};window.applyFrost(window.frostA,128);window.applyFrost(window.frostB,128,window.data);
 });
 assert.equal(await page.locator('.nord-theme-frost').count(),1);assert.equal(await page.locator('.nord-theme-frost').evaluate(el=>el.style.opacity),'1');
 await page.clock.runFor(405);assert(Math.abs(Number(await page.locator('.nord-theme-frost').evaluate(el=>el.style.opacity))-.5)<.06);
 await page.evaluate(()=>{window.data={...window.data,revision:'interrupt-frost',startsAt:Date.now()+100};window.applyFrost(window.frostA,128,window.data);});
 assert.equal(await page.locator('.nord-theme-frost > .nord-theme-frost').count(),1,'Interrupted frost must retain its currently blended image');
 await page.clock.runFor(600);assert.equal(await page.locator('.nord-theme-frost').count(),0,'Completed frost must release all old image layers');
 await page.emulateMedia({reducedMotion:'reduce'});await page.evaluate(()=>{window.data={revision:'reduce',startsAt:Date.now()+180,duration:450,from:{'--edge':'#000000'},to:{'--edge':'#ffffff'}};window.applyTheme(window.data.to,window.data);});assert.equal((await value()).color,'#ffffff');
 console.log('PASS: browser shared start, cosine midpoint, duplicate suppression, stale rejection, late join, final settling and reduced motion.');
}finally{await browser.close();}
