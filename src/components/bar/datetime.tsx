import { packCommand } from '../../lib/pack';
import { cn } from "../../lib/utils";
import { createSignal, onCleanup, onMount, Show } from "solid-js";
import { useProviders } from '../../lib/providers-context';
import { centerHover, sendCenter, focusMode } from '../../lib/rice-widgets';
import { barKey, refreshBarOrigin, toScreen } from '../../lib/screen';

function Datetime(props: { noIcon?: boolean }) {
  const { glazewm } = useProviders();
  const [now, setNow] = createSignal(new Date());

  onMount(() => {
    let timer: ReturnType<typeof setTimeout>;
    const tick = () => {
      setNow(new Date());
      timer = setTimeout(tick, 60000 - Date.now() % 60000 + 20);
    };
    timer = setTimeout(tick, 60000 - Date.now() % 60000 + 20);
    onCleanup(() => clearTimeout(timer));
  });

  // Registers the clock's physical screen rect with the control center, keyed by this bar window.
  async function openCenter(button: HTMLButtonElement) {
    await refreshBarOrigin();
    const r=button.getBoundingClientRect(), d=window.devicePixelRatio;
    const a=toScreen(r.left,r.top), b=toScreen(r.right,window.innerHeight);
    anchor=barKey();
    void glazewm()?.runCommand(`shell-exec ${packCommand('NordControlCenter.exe')} --register ${anchor} ${Math.round((a.x+b.x)/2)} ${b.y} ${d} ${a.x} ${a.y} ${b.x} ${b.y}`);
  }

  let buttonRef: HTMLButtonElement | undefined, anchor = '';
  onMount(() => {
    const register = () => { if (buttonRef) void openCenter(buttonRef); };
    const warmup = setInterval(() => { if (glazewm()) { register(); clearInterval(warmup); } }, 250);
    // Zebar may still be sizing/docking the window at first registration; register again once settled.
    const settle = setTimeout(register, 2000);
    window.addEventListener('resize', register);
    const sizeObserver=new ResizeObserver(register);if(buttonRef)sizeObserver.observe(buttonRef);
    onCleanup(() => { clearInterval(warmup); clearTimeout(settle); window.removeEventListener('resize', register); sizeObserver.disconnect(); });
  });
  function formattedDateTime() {
    const date = now();
    const weekday = date.toLocaleDateString(undefined, { weekday: 'short' });
    const time = date.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
    return `${weekday} ${date.getDate()} \u00b7 ${time}`;
  }

  return (
    <button ref={buttonRef} type="button" aria-label="Control center"
      onPointerEnter={() => { window.dispatchEvent(new Event('nord:center-open')); if (anchor) centerHover(anchor); }}
      title={now().toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }) + ' \u00b7 Click to pin Control Center'}
      onClick={async e => { e.stopPropagation(); if (!anchor && buttonRef) await openCenter(buttonRef); if (anchor) sendCenter((focusMode().session?'focus-open:':'center-pin:') + anchor); }}
      onDblClick={e=>e.stopPropagation()}
      class={cn(
        "nord-clock-button h-8 flex items-center transition-all duration-300 justify-center overflow-hidden gap-2 rounded-full px-3 relative"
      )}
    >
      <Show when={focusMode().session}><span class="nord-focus-countdown" title={focusMode().goal || 'Focus timer'}>{focusMode().phase==='break'?'Break':'Focus'} {focusMode().waiting?'ready':focusMode().paused?'paused':Math.floor(focusMode().remainingSeconds/60)+':'+(focusMode().remainingSeconds%60).toString().padStart(2,'0')}</span></Show>
      {!props.noIcon && <i class="ti ti-clock text-lg"></i>}
      <div class="text-sm font-medium">
        {formattedDateTime()}
      </div>
    </button>
  );
}

export default Datetime;
