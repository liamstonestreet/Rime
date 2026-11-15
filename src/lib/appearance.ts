// Applies the control center's appearance settings (Rice tab) to this bar. Transparency drives
// --bar-opacity; the wallpaper palette overrides theme variables except in lock-in, which keeps
// its own colors. Frost is the blurred wallpaper, rendered by the control center: each monitor's
// top strip arrives as a data URL, and the bar shows the one under it beneath its tint.
import { toScreen } from './screen';
import {applyTheme, applyFrost, type ThemeTransition} from './theme-transition';
import {edgeMetrics} from './edge-metrics';

export interface Appearance {
  revision?: number;
  bars?: { x: number; y: number; width: number; height: number }[];
  frost?: { x: number; y: number; width: number; height: number; image: string }[] | null;
  fontFamily?: string;
  transparency: number;
  surfaceContrast?: number;
  wallpaperColors: boolean;
  grainOpacity?: number;
  radius?: number;
  edgeStyle?: string;
  frame?: number;
  motion?: number;
  motionStyle?: string;
  palette?: Record<string, string> | null;
  transition?: ThemeTransition | null;
}

const style = document.createElement('style');
let applied = '';
let latestThemeStart=-Infinity;
let latestRevision=-Infinity;

// Font data has its own channel and stylesheet: tint/blur changes never rebuild the font face.
const fontStyle = document.createElement('style');
let lastFont = '';
export function applyFont(font: {family?: string; revision?: number; data?: string | null}) {
  const key = JSON.stringify(font);
  if (key === lastFont) return;
  lastFont = key;
  if (!fontStyle.isConnected) document.head.appendChild(fontStyle);
  const family = CSS.escape(font.family || 'Rubik');
  const embedded = !!font.data && /^data:font\/ttf;base64,[A-Za-z0-9+/=]+$/.test(font.data);
  const selected = `RiceSelected${font.revision || 0}`;
  fontStyle.textContent = embedded ? `@font-face{font-family:"${selected}";src:url(${font.data});font-weight:100 900;font-style:normal;font-display:swap}` : '';
  document.documentElement.style.setProperty('--rice-font', embedded ? `"${selected}", sans-serif` : `"${family}", sans-serif`);
}

export function applyAppearance(appearance: Appearance) {
  if(Number.isFinite(appearance.revision)){
    if(appearance.revision!<latestRevision)return;
    latestRevision=appearance.revision!;
  }else if(latestRevision!==-Infinity)return;
  if(appearance.transition&&appearance.transition.startsAt<latestThemeStart)return;
  if(appearance.transition)latestThemeStart=appearance.transition.startsAt;
  const key = JSON.stringify(appearance);
  if (key === applied) return;
  applied = key;
  if (!style.isConnected) document.head.appendChild(style);
  const opacity = 1 - Math.min(100, Math.max(0, appearance.transparency || 0)) / 100;
  const contrast = Math.min(100, Math.max(0, appearance.surfaceContrast ?? 0));
  const contrastVars = `;--surface-idle:${4 + contrast * .16}%;--surface-hover:${7 + contrast * .20}%;--surface-selected:${10 + contrast * .20}%`;
  const grain = Math.min(1, Math.max(0, appearance.grainOpacity || 0));
  const grainVars = grain ? `;--grain-opacity:${grain};--grain-image:url(${grainTile()});--grain-size:${128 / window.devicePixelRatio}px` : '';
  const origin = toScreen(0, 0), d = window.devicePixelRatio;
  const strip = appearance.frost?.find(s => origin.x >= s.x && origin.x < s.x + s.width && Math.abs(origin.y - s.y) < 64);
  document.documentElement.classList.toggle('frost', !!strip);
  document.documentElement.dataset.edge = ['gradient', 'glow', 'none'].includes(appearance.edgeStyle ?? '') ? appearance.edgeStyle : 'flat';
  const frostVars = strip && /^data:image\/jpeg;base64,[A-Za-z0-9+/=]+$/.test(strip.image)
    ? `;--frost-image:url(${strip.image});--frost-height:${strip.height / d}px` : '';
  applyFrost(frostVars?strip!.image:'',strip?strip.height/d:0,appearance.transition);
  const round = Math.min(200, Math.max(0, appearance.radius ?? 100)) / 100;
  const speed = Math.min(200, Math.max(0, appearance.motion ?? 100)) / 100;
  document.documentElement.dataset.motion = speed > 0 ? 'on' : 'off';
  const motionVars = speed > 0 ? `;--motion-time:${1 / speed}` + (appearance.motionStyle === 'springy' ? ';--motion-ease:cubic-bezier(.34,1.56,.64,1)' : '') : '';
  // Screen frame: the edge line stops at the frame and curves down into the frame's own line, which
  // continues the same circle below the bar (control-center/ScreenFrame.cs: 16px inner corner at 100%).
  const frame = Math.min(24, Math.max(0, appearance.frame ?? 0));
  const bar = appearance.bars?.find(b => origin.x >= b.x && origin.x < b.x + b.width && Math.abs(origin.y - b.y) < 4);
  // At fractional DPI the outer HWND can round UP (63 px at 125%) while
  // Chromium's drawable strip rounds DOWN (62 px). Never move our edge into
  // that extra host pixel: it gets clipped and makes the bar thinner than CC.
  const frameRadius=frame>0?16*round:0;
  const edge = edgeMetrics(window.innerHeight,d,bar?.height,frameRadius);
  const frameVars = `;--frame:${frame}px;--frame-radius:${frameRadius}px;--edge-line:${edge.line}px;--edge-dpr:${d};--edge-snap:${edge.offset}px;--edge-source-height:${edge.sourceHeight}px;--edge-source-bottom:${edge.bottom}px`;
  style.textContent = `:root{--bar-opacity:${opacity};--round:${round}${contrastVars}${motionVars}${grainVars}${frostVars}${frameVars}}`;
  // While matching is enabled, an incomplete reconnect snapshot must retain the last good theme.
  if(!appearance.wallpaperColors)applyTheme(null,null);
  else if(appearance.palette&&Object.keys(appearance.palette).length)
    applyTheme(appearance.palette,appearance.transition?{...appearance.transition,to:appearance.palette}:null);
}

// Film grain tile, one speck per physical pixel. Same generator and seed as the control center's
// (control-center/Grain.cs), so the bar and the panel share one texture.
let tile = '';
function grainTile() {
  if (tile) return tile;
  const size = 128, canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  const context = canvas.getContext('2d')!, image = context.createImageData(size, size);
  let seed = 7;
  for (let i = 0; i < size * size; i++) {
    seed = (seed + 0x6d2b79f5) >>> 0;
    let t = Math.imul(seed ^ (seed >>> 15), seed | 1);
    t = (t + Math.imul(t ^ (t >>> 7), t | 61)) ^ t;
    const v = ((t ^ (t >>> 14)) >>> 0) / 4294967296, white = v > 0.5 ? 255 : 0;
    image.data.set([white, white, white, Math.abs(v - 0.5) * 2 * 255], i * 4);
  }
  context.putImageData(image, 0, 0);
  return (tile = canvas.toDataURL());
}

// While the control center is open over this bar it draws the line itself, bending around the panel;
// the bar hides its own line under the panel so a see-through panel doesn't show it. Physical pixels.
export function applyCenterGap(gap: { left: number; right: number; top: number } | null) {
  const root = document.documentElement, d = window.devicePixelRatio, origin = toScreen(0, 0);
  const mine = !!gap && gap.top >= origin.y && gap.top < origin.y + window.innerHeight * d;
  root.classList.toggle('center-gap', mine);
  if (!mine) return;
  root.style.setProperty('--gap-left', `${(gap.left - origin.x) / d}px`);
  root.style.setProperty('--gap-right', `${(gap.right - origin.x) / d}px`);
}

// Independent of CC's opening, so either panel can close without restoring the other's line.
export function applyNotificationGap(gap:{left:number;right:number;top:number}|null){
 const root=document.documentElement,d=window.devicePixelRatio,origin=toScreen(0,0);
 const mine=!!gap&&gap.top>=origin.y&&gap.top<origin.y+window.innerHeight*d;
 root.classList.toggle('notification-gap',mine);
 if(mine)root.style.setProperty('--notification-gap-right',`${(gap!.right-origin.x)/d}px`);
}
