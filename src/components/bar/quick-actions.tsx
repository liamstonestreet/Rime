import {Show,createSignal,createEffect,onMount,onCleanup} from 'solid-js';
import {sendCenter,quickActionResult,discordVoice,bridgeConnected} from '../../lib/rice-widgets';


export default function QuickActions(){
 const [error,setError]=createSignal('');const [busy,setBusy]=createSignal(false);
 createEffect(()=>{if(quickActionResult()?.action==='discord')setBusy(false);});
 createEffect(()=>{if(bridgeConnected())sendCenter('quick-discord-status');else setBusy(false);});
 onMount(()=>{const refresh=()=>{if(bridgeConnected()&&!document.hidden&&!busy())sendCenter('quick-discord-status');};const timer=setInterval(refresh,2500);document.addEventListener('visibilitychange',refresh);window.addEventListener('focus',refresh);onCleanup(()=>{clearInterval(timer);document.removeEventListener('visibilitychange',refresh);window.removeEventListener('focus',refresh);});});
 const run=(action:'reflection'|'github')=>{setError('');if(!sendCenter('quick-action:'+action))setError('Control center is reconnecting. Try again.');};
 const toggleMute=()=>{if(busy())return;setError('');setBusy(true);if(!sendCenter('quick-discord-mute')){setBusy(false);setError('Control center is reconnecting. Try again.');}};
 const muteLabel=()=>discordVoice()?.muted===true?'Unmute Discord microphone':'Mute Discord microphone';
 const title=(action:string,fallback:string)=>error()||(quickActionResult()?.action===action&&quickActionResult()?.error)||fallback;
 return <div class="nord-quick-actions" role="group" aria-label="Quick actions">
  <button class="nord-quick-action" type="button" aria-label="Open today's reflection" title={title('reflection',"Create or open today's Obsidian reflection")} onDblClick={e=>e.stopPropagation()} onClick={()=>run('reflection')}>
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" aria-hidden="true"><path d="m13 2 7 7-3 11-7 2-6-8 3-9Z"/><path d="m13 2-3 9 7 9M4 14l6-3 10-2M10 11v11" opacity=".65"/></svg>
  </button>
  <button class="nord-quick-action nord-discord-action" type="button" aria-label={muteLabel()} aria-pressed={discordVoice()?.muted===true} aria-busy={busy()} title={title('discord',discordVoice()?.error||muteLabel())} classList={{'is-muted':discordVoice()?.muted===true}} onMouseEnter={()=>{if(!busy())sendCenter('quick-discord-status');}} onClick={toggleMute} onDblClick={e=>e.stopPropagation()}>
   <svg class="discord-toggle-icon" viewBox="0 0 26 26" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m8 5 1-2m7 2-1-2M7 5l-3 1-2 12 4 2 2-3m9-12 3 1 2 12-4 2-2-3M7 16c3 2 7 2 10 0M7 5c3-1 7-1 10 0"/><circle cx="8.5" cy="11.5" r="1.3" fill="currentColor" stroke="none"/><circle cx="15.5" cy="11.5" r="1.3" fill="currentColor" stroke="none"/><g class="discord-mic" transform="translate(15 14)"><circle cx="4.5" cy="5" r="6.5" fill="var(--nord-0)" stroke="none"/><rect x="3" y="0" width="3" height="6" rx="1.5"/><path d="M1 4v1a3.5 3.5 0 0 0 7 0V4M4.5 8.5V11M2.5 11h4"/></g><path class="discord-mute-slash" pathLength="1" d="M3 3l20 20"/></svg>
  </button>
  <button class="nord-quick-action" type="button" aria-label="Open GitHub" title={title('github','Open GitHub in your default browser')} onDblClick={e=>e.stopPropagation()} onClick={()=>run('github')}>
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 21v-4c-4 1-6-1-6-3m18-3c0-3-1-5-3-6 0-1 0-2-1-3-2 0-3 1-4 2h-2C10 3 9 2 7 2 6 3 6 4 6 5c-2 1-3 3-3 6 0 4 3 6 6 6m6 4v-4c4-1 6-3 6-6"/></svg>
  </button>

 </div>;
}
