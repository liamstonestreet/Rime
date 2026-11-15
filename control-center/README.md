# Nord control center

## Rice → Widgets & layout
Visibility and arrangement are staged until Apply, with Discard and Reset layout. Left/right placement and ordering are stored with widget visibility in `rice-widgets.json`. The clock and workspace taskbar anchors stay fixed. See [apps and layout](../docs/apps-and-layout.md) for behavior, configuration, and launcher research.

## Current-media links
`nord-media-link.js` is installed as a Spicetify extension. It reports only the current track/episode URL and title to the control center on loopback port 6138, on changes and every 15 seconds. It does not send data to an internet service. The helper matches the title against Windows media metadata and expires stale data after 45 seconds. Browser links use the matching window's exposed address field; unavailable addresses report a visible failure instead of claiming to copy.

The WebSocket token must match in `MediaBridge.cs`, the Spicetify extension, and `../src/lib/rice-widgets.ts`. Generate a new matching local token when packaging for another user.

## Build/check
`./build.ps1` builds the helper. `./check.ps1` builds isolated WPF component checks in `checks/`; run `checks/CenterChecks.exe`. These test staged toggles, Apply persistence, re-enabling a widget and exact media-title matching. They do not alter live settings or the clipboard.

## Tab heights
Each tab fits its content up to half the current monitor's physical resolution height, converted to WPF units at that monitor's scale. Taller tabs scroll with their scrollbar inside the right gutter. `control-center-layout.json` controls `transitionMs`; legacy fixed height values are ignored. Reopening or moving between monitor anchors recalculates the selected tab's height.

## Power menu
The far-right bar button opens `NordControlCenter.exe --power x y`. All monitors receive a dimmed, progressively blurred desktop snapshot; the clicked monitor shows Shutdown, Restart, Sleep and Screensaver. Escape/background click dismisses. The blur and button fade share a 220 ms ease-out transition. The background is frozen while open. Shutdown/restart do not force-close applications. Screensaver uses the Windows-configured `.scr` or the built-in blank screensaver. `--preview` disables power actions for testing.

## Bar edge

Glow uses a full corner/side source before clipping at the bar window, and a closed top source for the native frame blur. WPF's 15px blur radius and single-width stroke match Chromium's 5px-sigma drop shadow. Bar edge geometry paints in physical-pixel coordinates; cached native bar bounds supply its bottom-edge correction per monitor. This avoids bright arc endpoints and fractional-scale joins. Control Center uses the same native glow strength.

For cross-renderer visual checks, run `node checks/frame-glow/check.mjs` from the pack source root before the native component checks. The latter compare 24 corner configurations (both sides, 100/125/150/200% scaling, 0/100/200% radius, flat/glow) with a continuous WPF reference and save joined previews under `control-center/checks/frame-glow/`. These render synthetic surfaces and do not alter the live desktop.
The bar draws a 3px `#6B86A3` bottom line (same as the Alt indicator outline) in `src/frost-bar.css`, pinned to the window's real bottom. The control center window sits 4 DIPs higher (`Edge`) and draws one shape (`BuildShell`) that is the line, the 24px fillets and the panel outline/fill. While closed or opening, its depth follows the panel (`UpdateShape`), so the line bends down around the panel and returns flat on close. Points update via `CompositionTarget.Rendering` only during an animation (`TrackShape`), then unhook. Anchor bottoms snap to the real Zebar window edge (`SnapToBar`), because the clock's reported bottom is rounded.

## Hover and idle cost
While hidden the helper does not poll the cursor at 60 Hz. Hovering the clock sends `center-hover:<left>:<top>` over the bridge (the anchor's file key), and the helper opens immediately (measured 15-31 ms). The cursor poll remains as a fallback only: 1 Hz while a bar is connected (detected from its once-a-second `widgets` request), 10 Hz otherwise. While open it polls at 60 Hz to detect leaving. The media-card audio meter runs only while visible, and audio devices refresh after the panel opens instead of every 10 s in the background. Measured hidden idle: before 1,531 ms CPU and ~480 wake-ups/s per 20 s; after 0 ms and ~10/s.

## Monitors and visibility
The helper is per-monitor DPI aware (`Program.Main`), so the panel and power menu render at each monitor's own scale. Bars register with `--register <barX>_<barY> <centerX> <bottom> <scale> <left> <top> <right> <bottom>` in physical pixels, using Tauri's `outerPosition()` (`src/lib/screen.ts`): `window.screenX` is wrong on a secondary monitor whose scale differs from the primary. The panel is placed with `SetWindowPos` in physical pixels. It opens only when the window under the cursor is a Zebar bar, so a full-screen app covering the clock never triggers it.

## App volume mixer
The Audio tab includes an app mixer below the device and microphone controls. It shows audio sessions on the default playback device, grouped by process name (so multiple browser sessions share a row), with independent volume and mute controls. Active sessions appear automatically; inactive sessions remain for two minutes after last being observed active, provided the session still exists. This includes audio apps on other workspaces. Apps that have never opened an active audio session are omitted.

Rows refresh once a second while the mixer is visible. Slider writes are coalesced at 250 ms intervals and flushed when hiding; commands target the exact device and session identifiers represented by the row. Group volume shows the highest session volume; moving its slider sets all sessions in the group to that level. A group reads muted only when all its sessions are muted. Master volume and stereo balance remain endpoint controls.

`checks/CenterChecks.exe` exercises retention, command coalescing, device/session scoping, ended-session removal and the empty state using synthetic sessions without changing live app audio. The isolated visual preview is `checks/volume-mixer.png`.

## Screen-frame seams and Lightshot
Screen-frame windows allow WPF to process per-monitor DPI changes, then reapply their physical monitor bounds after the suggested-rectangle resize. This keeps corner radius, frame thickness and the bar join at the destination monitor's scale. Lightshot is excluded from the custom focus border; install.ps1 ensures its GlazeWM ignore rule exists.

For Windhawk, run `scripts/setup/exclude-lightshot-windhawk.ps1` from an Administrator PowerShell. It appends Lightshot.exe to the global process exclusion list while preserving existing entries and saving the old value in backups. Restart Windhawk and Lightshot afterward. Alternatively, add Lightshot.exe under Settings > Advanced settings > More advanced settings > Process exclusion list.

## Daily controls and pinning
Audio shows microphone mute independently from master speaker mute. The microphone control targets the default multimedia capture endpoint (the same input selected by this center); an app explicitly using another capture device may use a different microphone. Read failures show a disconnected/unavailable state. Stereo balance and input/output selection are directly available in Audio; Menu contains the expandable week view. Click the bar clock or Menu's Pin button to retain the panel while moving the pointer away. Escape and outside clicks dismiss it.

`BarState` shares status sampling across bars, emits only changed channels and provides complete state on subscription. Its heartbeat replaces the old repeated per-bar status commands. `BarAppearance.FontSnapshot` owns a separately cached font payload; ordinary appearance snapshots contain no font bytes. Isolated checks disable external appearance/config synchronization.

## Audio tab
Audio sits between Menu and Rice. It groups media first, then master volume, stereo balance, output/input selection, microphone mute, and app volume. Audio refreshes run while this tab is visible; the media meter and app mixer sleep on the other tabs. Menu holds the clock and calendar. Focus is a sixth, lazy-built tab with custom work/break cycles and an optional goal. The panel width is 584 DIPs; every tab keeps the monitor-specific half-height cap and shared fixed scrollbar gutter. Spotlight uses the same scrollbar implementation in shared/ScrollChrome.cs.

## Calendar and Timeline

Menu uses CalendarView for 21 selectable dates (previous/current/next weeks) and a day timeline. CalendarConnections combines private iCal feeds (no Google Cloud, up to 20 named calendars) and optional GoogleCalendarSource read-only Calendar/dated Tasks data through desktop PKCE OAuth. Private links and credentials use Windows DPAPI outside the pack. CalendarFeedDialog manages masked links; IcalData expands supported recurrence rules and exceptions with explicit notices for unsupported series. IcalChecks covers five-calendar merging, feed validation, recurrence/timezone boundaries and mocked HTTP; it never requests live Google feeds. See [setup and synchronization details](../docs/google-calendar.md). CalendarChecks uses a fake source during component tests; no Google account or live APIs are accessed.

## Focus and indicator keybinds

Rice also selects Classic, Gallery or Ribbon for the workspace indicator. See [indicator styles](../docs/workspace-indicators.md). The saved selection takes effect at the next indicator open.

The separate Wallpaper indicator row selects Classic, Showcase or Filmstrip. See [wallpaper styles](../docs/wallpaper-indicators.md). It uses the same hold/release/cancel behavior, with nearby-card rendering and monitor fitting for the new layouts.
See [Focus and keybinds](../docs/focus-and-keybinds.md). FocusView is built on first use, with its UI timer active only while visible. FocusSession owns pure work/break transitions; FocusMode owns persistence and reversible window hiding, with no process killing or new GlazeWM patches. IndicatorKeyControl in Rice edits the shared indicator-keys.json; both helpers watch atomic changes without idle polling.
