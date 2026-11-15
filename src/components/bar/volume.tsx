import { createSignal } from 'solid-js';
import {barAudio,changeBarAudio} from '../../lib/rice-widgets';
import './volume.css';

export default function Volume() {
  const [draft, setDraft] = createSignal<number | null>(null);
  const [error, setError] = createSignal('');
  const device = barAudio;
  const volume = () => draft() ?? device()?.volume ?? 0;
  return <div class="nord-volume" classList={{'is-muted':!!device()?.isMuted}} title={error() || device()?.name || 'Volume'} onDblClick={e => e.stopPropagation()}>
    <button aria-label={device()?.isMuted ? 'Unmute' : 'Mute'} disabled={!device()} onClick={() => setError(changeBarAudio('mute:'+(device()?.isMuted?'0':'1'))?'':'Audio helper disconnected')}>
      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M11 5 6 9H3v6h3l5 4z"/>{device()?.isMuted ? <path d="m16 9 5 6m0-6-5 6"/> : <path d="M15 8a6 6 0 0 1 0 8m3-11a10 10 0 0 1 0 14"/>}</svg>
    </button>
    <input aria-label="Output volume" type="range" min="0" max="100" step="1" value={volume()} disabled={!device()} onInput={e => setDraft(Number(e.currentTarget.value))} onChange={async e => { const value=Number(e.currentTarget.value); try { if(!changeBarAudio('volume:'+value))throw new Error('Disconnected'); setError(''); } catch { setError('Could not change volume'); } finally { setDraft(null); } }}/>
    <span>{Math.round(volume())}</span>
  </div>;
}
