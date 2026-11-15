import { cn, openBtop } from "../../lib/utils";
import { Show, createEffect, createSignal } from "solid-js";
import { useProviders } from "../../lib/providers-context";

function Memory() {
  const { memory, glazewm } = useProviders();
  const [memorySig, setMemorySig] = createSignal(memory());
  createEffect(() => setMemorySig(memory()));

  return (
    <Show when={memorySig()}>
      <div
        class={cn(
          "nord-status h-8 flex group items-center justify-center overflow-hidden gap-2 text-[var(--memory)] bg-[var(--memory)]/10 rounded-full pr-3 pl-4 relative cursor-pointer"
        )}
        onClick={() => openBtop(glazewm())}
      >
        <i class="nf nf-fa-memory text-lg w-5 shrink-0 text-center"></i>
        <div class="nord-status-value relative w-12 h-5 flex items-center justify-center">
        <div class="w-12 h-2 bg-[var(--memory)]/40 rounded-full relative overflow-hidden group-hover:translate-y-6 group-hover:opacity-0 transition-all duration-300">
          <div
            class="h-full bg-[var(--memory)] rounded-full"
            style={{
              width: `${
                (memorySig()!.usedMemory / memorySig()!.totalMemory) * 100
              }%`,
            }}
          ></div>
        </div>
        <span class="nord-status-label absolute inset-0 flex items-center justify-center text-sm font-medium tabular-nums transition-all -translate-y-6 duration-300 opacity-0 group-hover:opacity-100 group-hover:translate-y-0">
          {(memorySig()!.usedMemory / 1000000000).toFixed(0)}GB
        </span>
        </div>
      </div>
    </Show>
  );
}

export default Memory;
