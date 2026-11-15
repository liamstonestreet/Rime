interface TaskIdentity { key: string; window?: {id: string}; pin?: {id: string} }
// A pin changing into a running window (or back) keeps its button. Pin/unpin of
// a running app also reuses that button rather than flashing two copies.
export function retainTaskIdentity<T extends TaskIdentity>(next: T[], previous: (T & {renderKey: string})[], allocate: () => string) {
  const used = new Set<string>();
  const exact = next.map(item => previous.find(old => old.key === item.key));
  for (const old of exact) if (old) used.add(old.renderKey);
  return next.map((item, index) => {
    const old = exact[index] ?? previous.find(old => !used.has(old.renderKey) && (
      !!item.pin && old.pin?.id === item.pin.id || !!item.window && old.window?.id === item.window.id
    ));
    if (old) used.add(old.renderKey);
    return {...item, renderKey:old?.renderKey ?? allocate()};
  });
}

// Use resting layout coordinates, not animated rectangles: otherwise moving
// neighbors can continually change drag thresholds and make icons oscillate.
export function taskDropOrder(nodes: {key: string; center: number}[], dragged: string, x: number) {
  const others = nodes.filter(node => node.key !== dragged), keys = others.map(node => node.key);
  keys.splice(others.filter(node => x > node.center).length, 0, dragged);
  return keys;
}
