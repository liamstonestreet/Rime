import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';import {createRequire} from 'node:module';import {build} from 'vite';import assert from 'node:assert/strict';
const require=createRequire(import.meta.url);let playwright;try{playwright=require('playwright');}catch{playwright=require('C:/Users/liams/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const directory=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(directory,'../..'),out=path.join(root,'dist/edge-thickness-check'),images=path.join(root,'control-center/checks/frame-glow');
await build({configFile:false,root,base:'./',build:{outDir:out,rollupOptions:{input:path.join(directory,'thickness.html')}}});fs.mkdirSync(images,{recursive:true});
const browser=await playwright.chromium.launch({headless:true,channel:'msedge'});
try{for(const scale of [1,1.25,1.5,1.75,2]){
 const page=await browser.newPage({viewport:{width:640,height:180},deviceScaleFactor:scale});
 await page.route('http://thickness.local/**',route=>{const file=path.join(out,decodeURIComponent(new URL(route.request().url()).pathname));return route.fulfill({body:fs.readFileSync(file),contentType:file.endsWith('.html')?'text/html':file.endsWith('.css')?'text/css':'text/javascript'});});
 await page.setContent(`<style>html,body{margin:0;background:transparent;overflow:hidden}iframe{display:block;border:0;width:640px;height:${Math.floor(50*scale)/scale}px}</style><iframe src="http://thickness.local/checks/frame-glow/thickness.html"></iframe>`);
 const frame=await page.locator('iframe').elementHandle().then(h=>h.contentFrame());await frame.waitForFunction(()=>!!window.setEdge);
 for(const reported of new Set([Math.floor(50*scale),Math.ceil(50*scale)])){
  const metrics=await frame.evaluate(h=>window.setEdge(h),reported);assert.equal(metrics.height,Math.floor(50*scale));assert.equal(metrics.line*scale,Math.floor(3*scale));
  await page.screenshot({path:path.join(images,`thickness-${scale}-${reported}.png`),omitBackground:true});
 }
 await page.close();
}console.log('PASS: full bar stroke remains inside drawable pixels for floor/ceil host heights at 100/125/150/175/200%. Run CenterChecks for pixel-coverage assertions.');}finally{await browser.close();}
