# Performance pass — 2026-11-10

Source backup: `backups/source-snapshots/performance-20261110-143431` (repository root). This pass preserves configured visuals, refresh rate and shortcuts.

## Changes

- Spotlight loads its catalog and Start Menu shortcuts on a worker while cached results remain usable. Catalog and window refreshes each allow one in-flight operation. Startup dispatches this work after the UI dispatcher starts. Failed reloads retain the last good catalog; first-run catalog failures still discover shortcuts.
- Search history merges and saves no longer hold the ranking lock across disk IO. Ranking can proceed while the background writer serializes and replaces its file.
- Taskbar icon data is pruned against windows across **all** workspaces, avoiding eviction when simply switching workspaces. Duplicate icon replies do not update the reactive signal. Browser and shared native app-icon caches have a 512-entry ceiling.
- Screen frame maintenance uses the material generation counter instead of enumerating and statting every frost JPEG. Zebar process objects used during discovery are disposed.
- Zebar palette interpolation retains a CSS rule and changes its properties, rather than replacing and reparsing an entire stylesheet each frame. Native theme interpolation ignores duplicate rendering callbacks for the same presentation timestamp. Both retain the existing shared transition clock and completion wake-up.

## Validation and limits

Production bar, Control Center, Spotlight and workspace indicator builds; targeted TypeScript checking; native CC/Spotlight suites; browser theme interpolation test; taskbar reconciliation/motion checks with `node --conditions=browser scripts/taskbar-checks.mjs`.

`node scripts/theme-performance-checks.mjs` verifies retained CSS-rule identity, interpolation, final colors, focus-theme precedence and removal of stale overrides in headless Edge. Native suites use synthetic windows/adapters, not live clicks or wallpaper switches. DPAPI cases require a loaded user profile and are skipped in this sandbox.

Pre-install working-set snapshot: CC 367 MiB, Spotlight 143 MiB, workspace indicator 116 MiB, Zebar parent 75 MiB. This excludes WebView2 child processes and is not a system total or measured improvement. Working set varies with usage and Windows memory management. No working-set trimming or forced garbage collections were introduced.

Apply using the normal installer, then compare responsiveness and memory under the same workload: open search repeatedly, type quickly, change workspaces, open/close apps, and switch wallpapers on both monitors. The new build has not been installed or benchmarked against the live desktop from this sandbox; no percentage reduction or 144 Hz frame-time guarantee is claimed.
