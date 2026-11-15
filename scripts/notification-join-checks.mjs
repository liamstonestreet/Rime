import fs from 'node:fs';
import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const require=createRequire(import.meta.url);let playwright;try{playwright=require('playwright');}catch{playwright=require('C:/Users/liams/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const css=fs.readFileSync(new URL('../src/frost-bar.css',import.meta.url),'utf8');
const browser=await playwright.chromium.launch({headless:true,channel:'msedge'});
try{const page=await browser.newPage({viewport:{width:1200,height:60}});
 await page.setContent(`<style>${css}:root{--edge-dpr:1.25;--gap-left:700px;--gap-right:900px;--notification-gap-right:504px;}</style><div class="nord-frost-bar nord-arranged"><svg class="nord-horizontal-edge"><rect/></svg></div>`);
 const masks=async classes=>page.evaluate(value=>{document.documentElement.className=value;return {edge:getComputedStyle(document.querySelector('.nord-horizontal-edge')).maskImage,frame:getComputedStyle(document.querySelector('.nord-frost-bar'),'::after').maskImage};},classes);
 const both=await masks('center-gap notification-gap');assert.ok(both.edge.includes('504px')&&both.edge.includes('700px')&&both.edge.includes('900px'),JSON.stringify(both));assert.ok(both.frame.includes('630px')&&both.frame.includes('875px')&&both.frame.includes('1125px'),JSON.stringify(both));
 const cc=await masks('center-gap');assert.ok(cc.edge.includes('700px')&&!cc.edge.includes('504px'));
 const notification=await masks('notification-gap');assert.ok(notification.edge.includes('504px')&&!notification.edge.includes('700px'));
 console.log('PASS: independent CC/notification edge masks compose and restore correctly, including fractional-DPI corner masks. Headless CSS only.');
}finally{await browser.close();}
