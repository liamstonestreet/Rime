import { Accessor, JSX, createContext, useContext } from "solid-js";
import type * as zebar from "zebar";
import { useProvider } from "./provider-registry";

export interface ProvidersValue {
  cpu: Accessor<zebar.CpuOutput | undefined>;
  memory: Accessor<zebar.MemoryOutput | undefined>;
  media: Accessor<zebar.MediaOutput | undefined>;
  glazewm: Accessor<zebar.GlazeWmOutput | undefined>;
}

const configs: Record<keyof ProvidersValue, zebar.ProviderConfig> = {
  cpu: { type: "cpu" },
  memory: { type: "memory" },
  media: { type: "media", refreshInterval: 1000 } as zebar.ProviderConfig,
  glazewm: { type: "glazewm" },
};

export const ProvidersContext = createContext<true>();

export function ProvidersProvider(props: { children: JSX.Element }) {
  return <ProvidersContext.Provider value={true}>{props.children}</ProvidersContext.Provider>;
}

/**
 * Providers for the calling widget. Each one is leased from the shared registry the first time the
 * widget reads it (e.g. `const { cpu } = useProviders()`) and released when the widget unmounts or
 * moves, so hidden widgets stop their feeds and moved widgets never lose them. New widgets get this
 * automatically; a widget needing another provider type calls useProvider() from provider-registry.
 */
export function useProviders(): ProvidersValue {
  const ctx = useContext(ProvidersContext);
  if (!ctx) throw new Error("useProviders must be used within ProvidersProvider");
  const value = {} as ProvidersValue;
  const leased: Partial<Record<keyof ProvidersValue, Accessor<any>>> = {};
  for (const key of Object.keys(configs) as (keyof ProvidersValue)[]) {
    Object.defineProperty(value, key, {
      enumerable: true,
      get() {
        if (!leased[key]) leased[key] = useProvider(configs[key]);
        return leased[key];
      },
    });
  }
  return value;
}
