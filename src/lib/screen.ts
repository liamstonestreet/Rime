import { currentWidget } from 'zebar';

// Physical screen position of this bar window. window.screenX/screenY are wrong on a secondary
// monitor whose scale differs from the primary (Zebar reports them in primary-monitor DIPs, e.g.
// 2048 instead of 2560 next to a 125% screen), so ask the native window instead.
let origin: { x: number; y: number } | undefined;

export async function refreshBarOrigin() {
  try {
    const p = await currentWidget().tauriWindow.outerPosition();
    origin = { x: p.x, y: p.y };
  } catch {
    origin = undefined;
  }
  return origin;
}

// Converts a point in this page's CSS pixels to physical screen pixels.
export function toScreen(cssX: number, cssY: number) {
  const d = window.devicePixelRatio;
  const o = origin ?? { x: Math.round(window.screenX * d), y: Math.round(window.screenY * d) };
  return { x: o.x + Math.round(cssX * d), y: o.y + Math.round(cssY * d) };
}

// Stable per-bar key (its window origin), used to name the control center anchor file.
export function barKey() {
  const o = toScreen(0, 0);
  return `${o.x}_${o.y}`;
}
