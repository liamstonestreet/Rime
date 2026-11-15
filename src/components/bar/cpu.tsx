import { cn, openBtop } from "../../lib/utils";
import { Show, createEffect, createSignal } from "solid-js";
import { useProviders } from "../../lib/providers-context";

function Cpu() {
  const { cpu, glazewm } = useProviders();
  const [cpuSig, setCpuSig] = createSignal(cpu());
  createEffect(() => setCpuSig(cpu()));

  return (
    <Show when={cpuSig()}>
      <div
        class={cn(
          "nord-status h-8 flex group items-center justify-center overflow-hidden gap-2 text-[var(--cpu)] bg-[var(--cpu)]/10 rounded-full pr-3 pl-4 relative cursor-pointer"
        )}
        onClick={() => openBtop(glazewm())}
      >
        <i class="nf nf-oct-cpu text-lg w-5 shrink-0 text-center"></i>
        <div class="nord-status-value relative w-12 h-5 flex items-center justify-center">
        <div class="w-12 h-2 bg-[var(--cpu)]/40 rounded-full relative overflow-hidden group-hover:translate-y-6 group-hover:opacity-0 transition-all duration-300">
          <div
            class="h-full bg-[var(--cpu)] rounded-full"
            style={{
              width: `${cpuSig()!.usage}%`,
            }}
          ></div>
        </div>
        <span class="nord-status-label absolute inset-0 flex items-center justify-center text-sm font-medium tabular-nums transition-all -translate-y-6 duration-300 opacity-0 group-hover:opacity-100 group-hover:translate-y-0">
          {cpuSig()!.usage.toFixed(0)}%
        </span>
        </div>
      </div>
    </Show>
  );
}

export default Cpu;
