export interface ThemeTransition {
  revision: string;
  startsAt: number;
  duration: number;
  from: Record<string,string> | null;
  to: Record<string,string> | null;
}
const sheet=document.createElement('style');
let frame=0,revision='',latestStart=-Infinity;
let frostImage='',frostHeight=0,frostFrame=0;
let frostLayer:HTMLDivElement|null=null;
export function applyFrost(image:string,height:number,transition?:ThemeTransition|null){
  if(image===frostImage&&height===frostHeight)return;
  const interrupted=frostLayer?.cloneNode(true) as HTMLDivElement|undefined;
  cancelAnimationFrame(frostFrame);frostLayer?.remove();
  const previous=frostImage,previousHeight=frostHeight;frostImage=image;frostHeight=height;
  if(!previous||!image||!transition||transition.duration<=0||Date.now()>=transition.startsAt+transition.duration||matchMedia('(prefers-reduced-motion: reduce)').matches)return;
  const bar=document.querySelector('.nord-frost-bar');if(!bar)return;
  frostLayer=document.createElement('div');frostLayer.className='nord-theme-frost';frostLayer.setAttribute('aria-hidden','true');
  frostLayer.style.background=`linear-gradient(color-mix(in srgb, var(--nord-0) calc(var(--bar-opacity) * 100%), transparent),color-mix(in srgb, var(--nord-0) calc(var(--bar-opacity) * 100%), transparent)),url(${previous}) 0 0/100vw ${previousHeight}px no-repeat`;
  if(interrupted)frostLayer.appendChild(interrupted);
  bar.prepend(frostLayer);
  const layer=frostLayer;
  const tick=()=>{const t=themeProgress(transition.startsAt,transition.duration,Date.now());layer.style.opacity=String(1-t);if(t>=1){layer.remove();if(frostLayer===layer)frostLayer=null;frostFrame=0;}else frostFrame=requestAnimationFrame(tick);};tick();
}
const valid=(palette:Record<string,string>|null)=>Object.fromEntries(Object.entries(palette??{}).filter(([k,v])=>/^--[a-z0-9-]+$/.test(k)&&/^#[0-9a-f]{6}$/i.test(v)));
export function themeProgress(startsAt:number,duration:number,now:number){
  if(duration<=0)return 1;
  const t=Math.max(0,Math.min(1,(now-startsAt)/duration));return .5-.5*Math.cos(Math.PI*t);
}
export function applyTheme(palette:Record<string,string>|null|undefined,transition?:ThemeTransition|null){
  if(transition&&Number.isFinite(transition.startsAt)&&transition.startsAt<latestStart)return;
  const key=JSON.stringify([transition?.revision??null,palette??null]);
  if(key===revision)return;
  revision=key;cancelAnimationFrame(frame);frame=0;
  if(!sheet.isConnected)document.head.appendChild(sheet);
  // Keep one CSS rule alive throughout the transition rather than reparsing a stylesheet every frame.
  if(!sheet.sheet?.cssRules.length)sheet.textContent='html:not(.locked-in){}';
  const style=(sheet.sheet!.cssRules[0] as CSSStyleRule).style;
  const to=valid(palette??null);
  const paint=(colors:Record<string,string>)=>{for(const name of Array.from(style))if(!(name in colors))style.removeProperty(name);for(const [name,value] of Object.entries(colors))if(style.getPropertyValue(name)!==value)style.setProperty(name,value);};
  if(!transition||!Number.isFinite(transition.startsAt)||transition.duration<0||transition.duration>5000||transition.startsAt>Date.now()+5000){paint(to);document.documentElement.classList.remove('theme-transition');return;}
  latestStart=transition.startsAt;
  const from=valid(transition.from),target=valid(transition.to);
  // Read the original CSS palette, without our previous overrides. This never paints an intermediate frame.
  style.cssText='';const defaults=getComputedStyle(document.documentElement);
  const keys=[...new Set([...Object.keys(from),...Object.keys(target)])];
  const rgb=(value:string)=>[1,3,5].map(i=>parseInt(value.slice(i,i+2),16));
  const pairs=keys.map(k=>{
    const base=defaults.getPropertyValue(k).trim();const fallback=/^#[0-9a-f]{6}$/i.test(base)?base:'#2e3440';
    return {key:k,a:rgb(from[k]??fallback),b:rgb(target[k]??fallback)};
  });
  const reduced=matchMedia('(prefers-reduced-motion: reduce)').matches;
  const tick=()=>{
    const t=reduced?1:themeProgress(transition.startsAt,transition.duration,Date.now());
    if(t>=1){paint(target);document.documentElement.classList.remove('theme-transition');frame=0;return;}
    document.documentElement.classList.add('theme-transition');
    paint(Object.fromEntries(pairs.map(({key,a,b})=>[key,'#'+a.map((v,i)=>Math.round(v+(b[i]-v)*t).toString(16).padStart(2,'0')).join('')])));
    frame=requestAnimationFrame(tick);
  };tick();
}
