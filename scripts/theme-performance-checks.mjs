import fs from 'node:fs';
import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const require=createRequire(import.meta.url);
const ts=require('typescript');
let playwright;try{playwright=require('playwright');}catch{playwright=require('C:/Users/liams/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const source=fs.readFileSync(new URL('../src/lib/theme-transition.ts',import.meta.url),'utf8');
const compiled=ts.transpileModule(source,{compilerOptions:{target:ts.ScriptTarget.ES2020,module:ts.ModuleKind.CommonJS}}).outputText;
const browser=await playwright.chromium.launch({headless:true,channel:'msedge'});
try{
 const page=await browser.newPage();
 await page.setContent('<style>:root{--edge:#123456}html.locked-in{--edge:#abcdef}</style>');
 const result=await page.evaluate(code=>{
  const exports={};new Function('exports',code)(exports);
  let now=1000,callback;Date.now=()=>now;window.requestAnimationFrame=fn=>{callback=fn;return 1;};window.cancelAnimationFrame=()=>{callback=null;};
  exports.applyTheme({'--edge':'#ffffff'},{revision:'one',startsAt:1000,duration:400,from:{'--edge':'#000000'},to:{'--edge':'#ffffff'}});
  const sheet=document.styleSheets[1],rule=sheet.cssRules[0];now=1200;callback();const middle=getComputedStyle(document.documentElement).getPropertyValue('--edge');
  now=1400;callback();const end=getComputedStyle(document.documentElement).getPropertyValue('--edge');
  const retained=rule===sheet.cssRules[0];
  document.documentElement.classList.add('locked-in');const locked=getComputedStyle(document.documentElement).getPropertyValue('--edge');
  document.documentElement.classList.remove('locked-in');exports.applyTheme(null);
  return {middle,end,retained,locked,reset:getComputedStyle(document.documentElement).getPropertyValue('--edge'),rules:sheet.cssRules.length};
 },compiled);
 assert.ok(['#7f7f7f','#808080'].includes(result.middle));
 assert.deepEqual({...result,middle:undefined},{middle:undefined,end:'#ffffff',retained:true,locked:'#abcdef',reset:'#123456',rules:1});
 console.log('PASS: theme interpolation retains one CSS rule, settles exactly, preserves focus colors and clears stale overrides. Headless browser only.');
}finally{await browser.close();}
