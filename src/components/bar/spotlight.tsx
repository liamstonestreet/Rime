import {currentWidget} from 'zebar';
import { createSignal, onCleanup, onMount } from 'solid-js';
import { spotlightUrl } from '../../lib/spotlight-bridge';
import {barKey} from '../../lib/screen';
export default function Spotlight() {
  const [error, setError] = createSignal('');
  const [query, setQuery] = createSignal('');
  let pane!: HTMLDivElement;
  let input!: HTMLInputElement;
  const focusSearch=()=>{input.focus();void currentWidget().tauriWindow.setFocus().then(()=>{input.focus();send("update");}).catch(()=>input.focus());};
  let timer: ReturnType<typeof setTimeout> | undefined;
  let reconnect: ReturnType<typeof setTimeout> | undefined;
  let socket: WebSocket | undefined;
  let disposed = false;
  let retryDelay = 500;
  let queued: string | undefined;
  let requestId = 0;
  const connect = () => {
    if (disposed || socket?.readyState === WebSocket.OPEN || socket?.readyState === WebSocket.CONNECTING) return;
    socket = new WebSocket(spotlightUrl);
    socket.onopen = () => {
      retryDelay = 500; setError('');
      if (queued) { socket!.send(queued); queued = undefined; }
    };
    socket.onmessage = event => {
      try {
        const response=JSON.parse(event.data);
        if(response.completion&&response.source===barKey()&&typeof response.query==='string'){
          setQuery(response.query);input.focus();input.setSelectionRange(response.query.length,response.query.length);
        }else if(response.ok===false)setError('Search request failed');
        else if(response.id===requestId&&['launch','complete'].includes(response.action)&&typeof response.query==='string'){
          setQuery(response.query);input.setSelectionRange(response.query.length,response.query.length);
        }
      } catch { }
    };
    socket.onerror = () => setError('Search helper is reconnecting…');
    socket.onclose = () => {
      if (disposed) return;
      reconnect = setTimeout(connect, retryDelay);
      retryDelay = Math.min(30000, retryDelay * 2);
    };
  };
  const send = (action: string) => {
    clearTimeout(timer);
    const rect = pane.getBoundingClientRect();
    const request = JSON.stringify({ id:++requestId,source:barKey(),action,query:query(),x:rect.left,y:rect.bottom,width:rect.width,scale:window.devicePixelRatio });
    if (socket?.readyState === WebSocket.OPEN) socket.send(request);
    else { queued = request; connect(); }
  };
  // The control center is opening: close the dropdown / floating Spotlight so they don't overlap.
  const closeForCenter = () => { if (document.activeElement === input) input.blur(); send('hide'); };
  onMount(() => { connect(); window.addEventListener('nord:center-open', closeForCenter); });
  onCleanup(() => window.removeEventListener('nord:center-open', closeForCenter));
  onCleanup(() => { disposed = true; clearTimeout(timer); clearTimeout(reconnect); socket?.close(); });
  return <div ref={pane} class="nord-search-button" onPointerDown={e=>{if(!(e.target as HTMLElement).closest("button"))focusSearch();}} title={error() || 'Search apps, files and windows · / for commands · Ctrl+Space for Spotlight'} onDblClick={event => event.stopPropagation()}>
    <button type="button" aria-label="Open floating Spotlight" title="Floating Spotlight · Ctrl+Space"
      style={{ background: 'transparent', border: 'none', padding: '0', color: 'inherit', cursor: 'pointer' }} onClick={() => send('floating')}>
      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <circle cx="10.5" cy="10.5" r="6.5" stroke="currentColor" stroke-width="1.6" />
        <path d="m16 16 4.5 4.5" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" />
      </svg>
    </button>
    <input ref={input} aria-label="Search apps" placeholder="Search anything…" value={query()} maxlength={160}
      onFocus={() => send('update')} onBlur={() => send('hide-dropdown')}
      onInput={event => { setQuery(event.currentTarget.value); clearTimeout(timer); timer = setTimeout(() => send('update'), 20); }}
      onKeyDown={event => {
        event.stopPropagation();
        if (event.key === 'Enter' || event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key==='Tab') {
          event.preventDefault();send(event.key==='Tab'?'complete':event.key==='Enter'?(event.ctrlKey?'web':'launch'):event.key==='ArrowDown'?'down':'up');
        }
        if(event.ctrlKey&&event.key.toLowerCase()==='c'&&event.currentTarget.selectionStart===event.currentTarget.selectionEnd){event.preventDefault();send('copy');}
        if (event.key === 'Escape') { setQuery(''); send('hide-dropdown'); event.currentTarget.blur(); }
      }} />
  </div>;
}
