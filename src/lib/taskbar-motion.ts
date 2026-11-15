// Animate layout changes, never a permanent render loop. Slots move independently
// of button hover/press effects, and departing buttons cannot receive input.
export function taskbarMotion(root: () => HTMLElement) {
  const animations = new Map<HTMLElement, Animation>();
  const ghosts = new Set<HTMLElement>();
  let before: Map<string, {node: HTMLElement; left: number; top: number; state: string}> | undefined;
  let queued = false, disposed = false;
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
  const slots = () => Array.from(root().querySelectorAll<HTMLElement>(':scope > [data-task-key]'));
  function cancel(node: HTMLElement) { animations.get(node)?.cancel(); animations.delete(node); }
  function clear() { for (const animation of animations.values()) animation.cancel(); animations.clear(); for (const ghost of ghosts) ghost.remove(); ghosts.clear(); }
  function settings() {
    const style = getComputedStyle(root());
    return {duration: Math.min(600, 210 * (parseFloat(style.getPropertyValue('--motion-time')) || 1)), easing: style.getPropertyValue('--motion-ease').trim() || 'cubic-bezier(.2,.8,.2,1)'};
  }
  const enabled = () => document.documentElement.dataset.motion !== 'off' && !reduced.matches;
  function animate(node: HTMLElement, frames: Keyframe[], duration: number, easing: string, done?: () => void) {
    cancel(node);
    const animation = node.animate(frames, {duration, easing});
    animations.set(node, animation);
    animation.onfinish = () => { if (animations.get(node) === animation) { animations.delete(node); animation.cancel(); done?.(); } };
  }
  function update(nextKeys: Set<string>, mutate: () => void) {
    if (disposed) return;
    if (!enabled()) { clear(); mutate(); return; }
    const element = root(), options = settings();
    if (!before) before = new Map(slots().map(node => {
      const rect = node.getBoundingClientRect();
      return [node.dataset.taskKey!, {node, left: rect.left, top: rect.top, state: node.dataset.taskState ?? ''}];
    }));
    // Clone only exiting slots outside Solid's managed list and its scroll clip.
    // Remaining icons close the gap while the removed icon fades in place.
    for (const node of slots()) if (!nextKeys.has(node.dataset.taskKey!)) {
      const rect = node.getBoundingClientRect();
      const ghost = node.cloneNode(true) as HTMLElement;
      ghost.removeAttribute('data-task-key'); ghost.classList.add('is-task-ghost');
      ghost.setAttribute('aria-hidden', 'true'); ghost.inert = true;
      Object.assign(ghost.style, {position:'fixed', left:rect.left+'px', top:rect.top+'px', width:rect.width+'px', height:rect.height+'px'});
      element.parentElement!.appendChild(ghost); ghosts.add(ghost);
      animate(ghost, [{opacity:1, transform:'scale(1)'}, {opacity:0, transform:'translateY(5px) scale(.72)'}], options.duration * .8, options.easing, () => { ghost.remove(); ghosts.delete(ghost); });
    }
    mutate();
    if (queued) return;
    queued = true;
    queueMicrotask(() => {
      queued = false;
      const previous = before; before = undefined;
      if (disposed || !previous) return;
      if (!enabled()) { clear(); return; }
      const current = slots();
      // Read every destination before starting any new animations.
      for (const node of current) cancel(node);
      const moves = current.map(node => ({node, rect:node.getBoundingClientRect(), old:previous.get(node.dataset.taskKey!)}));
      for (const {node, rect, old} of moves) {
        if (!old) animate(node, [{opacity:0, transform:'translateY(6px) scale(.7)'}, {opacity:1, transform:'translateY(0) scale(1)'}], options.duration, options.easing);
        else {
          const x = old.left - rect.left, y = old.top - rect.top;
          if (Math.abs(x) > .5 || Math.abs(y) > .5) animate(node, [{transform:`translate(${x}px,${y}px)`}, {transform:'translate(0,0)'}], options.duration, options.easing);
          if (old.state !== node.dataset.taskState) {
            const button = node.querySelector<HTMLElement>('button');
            if (button) animate(button, [{transform:'scale(1)'}, {transform:'scale(.87)', offset:.35}, {transform:'scale(1)'}], options.duration, options.easing);
          }
        }
      }
    });
  }
  const changed = () => { if (!enabled()) clear(); };
  reduced.addEventListener('change', changed);
  const observer = new MutationObserver(changed);
  observer.observe(document.documentElement, {attributes:true, attributeFilter:['data-motion']});
  return {update, dispose() { disposed = true; before = undefined; clear(); observer.disconnect(); reduced.removeEventListener('change', changed); }};
}
