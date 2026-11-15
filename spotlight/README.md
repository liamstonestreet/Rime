# Nord Spotlight

Type in Search beside CPU in LiamTesting. Matching apps appear below the field;
Up/Down selects, Enter opens, Escape or leaving the field dismisses it.
Click the magnifying glass or press Ctrl+Space for floating Spotlight.

The bar keeps one authenticated loopback WebSocket connection to the resident
helper. Typing is coalesced for 20ms; arrow navigation sends one selection action.
No process starts, request files, catalog reads, or window repositioning occur on
the typing/navigation hot path. The catalog is loaded once at startup. Reinstall
or restart the helper after refreshing the app catalog.

Both modes use native acrylic with a translucent neutral tint. Background blur
is composed by Windows rather than captured screenshots or a CPU blur loop.
The desktop's transparency settings still determine whether Windows shows blur.

`../rebuild.sh` builds and installs the bar and helper. `install.ps1` refreshes the
Start-app catalog. Build both pieces together: `BridgeKey.cs` and
`../src/lib/spotlight-bridge.ts` contain matching local connection credentials.
The server binds only to 127.0.0.1:6137, accepts token-authenticated WebSockets,
limits message sizes/connections, and launches only catalog app identifiers.
The shortcut's occasional helper invocation still uses its named-event handoff.

Appearance and matching: `NordSpotlight.cs`. Transport: `SearchBridge.cs`.
Bar input: `../src/components/bar/spotlight.tsx`; styling: `../src/frost-bar.css`.
`Filter` is the future slash-command routing point. Slash commands aren't implemented.

`node spotlight/benchmark.cjs` exercises live search/navigation and dismisses its
panel afterward; it launches no apps. It reports handler/round-trip timing,
process identity, catalog reads, and arrow-triggered filter passes.

Motion: 300ms downward reveal and 360ms upward close, with three subtle fading wisps at the top. The actual native window height animates so the acrylic bottom edge moves with the reveal; the contents keep their full layout height and are cropped without reflow. Animation timers run only during transitions; reduced-motion preferences disable the effect. Rapid reopening cancels the close.
