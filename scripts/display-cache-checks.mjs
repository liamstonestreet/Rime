import {test} from 'node:test';
import assert from 'node:assert/strict';
import {savedAppearance,saveDisplay,readDisplay} from '../src/lib/display-cache.ts';
test('display cache preserves settings but discards previous process clocks',()=>{
 const value={revision:200,transition:{startsAt:1234},transparency:40,wallpaperColors:true,frame:13,palette:{accent:'#abcdef'}};
 assert.deepEqual(savedAppearance(value),{transparency:40,wallpaperColors:true,frame:13,palette:{accent:'#abcdef'}});
 assert.equal(value.revision,200);
});
test('storage failures never prevent live startup',()=>{
 Object.defineProperty(globalThis,'localStorage',{configurable:true,get(){throw Error('unavailable');}});
 assert.equal(readDisplay('appearance'),null);assert.doesNotThrow(()=>saveDisplay('appearance',{}));
 const values=new Map();Object.defineProperty(globalThis,'localStorage',{configurable:true,value:{getItem:k=>values.get(k),setItem:(k,v)=>values.set(k,v)}});
 saveDisplay('widgets',{volume:false});assert.deepEqual(readDisplay('widgets'),{volume:false});
 values.set('nord-display-appearance','invalid');assert.equal(readDisplay('appearance'),null);
});
