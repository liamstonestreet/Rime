import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);let playwright;try{playwright=require('playwright');}catch{playwright=require('C:/Users/liams/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const css=fs.readFileSync(new URL('../../src/frost-bar.css',import.meta.url),'utf8');
const output=fileURLToPath(new URL('../../control-center/checks/frame-glow/',import.meta.url));fs.mkdirSync(output,{recursive:true});
const browser=await playwright.chromium.launch({headless:true,channel:'msedge'});
try{for(const scale of [1,1.25,1.5,2]){
 const page=await browser.newPage({viewport:{width:640,height:180},deviceScaleFactor:scale});
 const height=Math.floor(50*scale)/scale;
 await page.setContent(`<style>html,body{margin:0;background:transparent;overflow:hidden}iframe{display:block;border:0;width:640px;height:${height}px}</style><iframe></iframe>`);
 const frame=await page.locator('iframe').elementHandle().then(handle=>handle.contentFrame());
 for(const radius of [0,16,32])for(const edge of ['flat','glow']){
  await frame.setContent(`<style>${css}\n:root{--edge:#f18fb5;--edge-dpr:${scale};--edge-snap:${50-height}px;--frame:14px;--frame-radius:${radius}px;--edge-line:${Math.floor(3*scale)/scale}px}html,body{margin:0;background:transparent;overflow:hidden}.nord-frost-bar{background:transparent!important}</style><div class="nord-frost-bar"><svg class="nord-horizontal-edge"><defs><linearGradient id="nord-horizontal-gradient" x1="0%" x2="100%"><stop offset="0%"/><stop offset="100%"/></linearGradient></defs><rect/></svg></div>`);
  await frame.evaluate(edge=>document.documentElement.dataset.edge=edge,edge);
  await page.screenshot({path:path.join(output,`bar-${edge}-${radius}-${scale}.png`),omitBackground:true});
 }
 await page.close();
}console.log('PASS: Chromium frame-corner sources rendered at 100/125/150/200% with square, normal and large radii.');}
finally{await browser.close();}
