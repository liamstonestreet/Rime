import {createProvider,type ProviderConfig} from 'zebar';
import {createSignal,getOwner,onCleanup,type Accessor} from 'solid-js';

// Zebar providers are shared resources: Zebar's stop() removes every listener with the same config
// and shuts the backend feed down. A widget must therefore never own a provider. When a widget moves
// lanes, its old instance is cleaned up and a new one mounts (in either order); if the old one stopped
// "its" provider, the new one would silently lose its data. Widgets lease providers from the registry
// instead, and a provider stops only after its last lease has been released for a short grace period.

/** One running Zebar provider for one config, reference-counted across widget instances. */
class SharedProvider<T> {
 private provider: ReturnType<typeof createProvider> | undefined;
 private refs = 0;
 private stopTimer: ReturnType<typeof setTimeout> | undefined;
 private stopping: Promise<void> | undefined;
 private readonly value = createSignal<T | undefined>(undefined);

 constructor(private readonly config: ProviderConfig, private readonly graceMs: number) {}

 get output(): Accessor<T | undefined> { return this.value[0]; }

 acquire() {
  clearTimeout(this.stopTimer); this.stopTimer = undefined;
  if (this.refs++ === 0 && !this.provider) this.start();
 }

 release() {
  if (this.refs === 0 || --this.refs > 0) return;
  this.stopTimer = setTimeout(() => this.stop(), this.graceMs);
 }

 private start() {
  const create = () => {
   if (this.refs === 0 || this.provider) return;
   const provider = createProvider(this.config as any);
   this.provider = provider;
   this.value[1](() => (provider.output ?? undefined) as T | undefined);
   provider.onOutput(output => {if(this.provider===provider)this.value[1](() => output as T);});
  };
  // A stop still in flight would unlisten this config again, so start after it finishes.
  if (this.stopping) void this.stopping.then(create); else create();
 }

 private stop() {
  if (this.refs > 0 || !this.provider) return;
  const provider = this.provider;
  this.provider = undefined; this.value[1](undefined);
  this.stopping = Promise.resolve(provider.stop()).catch(() => {}).finally(() => { this.stopping = undefined; });
 }
}

/** A widget's hold on a shared provider. Release it exactly once; extra calls are ignored. */
export class ProviderLease<T> {
 private released = false;
 constructor(private readonly entry: SharedProvider<T>) { entry.acquire(); }
 get output(): Accessor<T | undefined> { return this.entry.output; }
 release() { if (this.released) return; this.released = true; this.entry.release(); }
}

/** Hands out leases on shared providers, one provider per distinct config. */
export class ProviderRegistry {
 private readonly entries = new Map<string, SharedProvider<unknown>>();
 constructor(private readonly graceMs = 5000) {}
 lease<T>(config: ProviderConfig): ProviderLease<T> {
  const key = JSON.stringify(config);
  let entry = this.entries.get(key);
  if (!entry) { entry = new SharedProvider<unknown>(config, this.graceMs); this.entries.set(key, entry); }
  return new ProviderLease<T>(entry as SharedProvider<T>);
 }
}

export const providerRegistry = new ProviderRegistry();

/** Use a Zebar provider in a component. The lease is released when the component unmounts or moves. */
export function useProvider<T>(config: ProviderConfig): Accessor<T | undefined> {
 const lease = providerRegistry.lease<T>(config);
 if (getOwner()) onCleanup(() => lease.release());
 return lease.output;
}
