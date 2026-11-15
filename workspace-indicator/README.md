# Nord workspace indicator — first draft

Hold **left Alt** to show the overlay, use your normal GlazeWM workspace shortcuts,
then release Alt to fade it away. Right Alt/AltGr does not trigger it.

The overlay captures the foreground window's monitor once when left Alt is pressed.
It stays centered on that screen for the entire hold, including when switching to
workspace 0 on the other screen. It cannot receive clicks or keyboard focus.

The selection slides between 1–9 and 0 in 240ms. The panel expands smoothly from its center over 190ms without overshoot, fading in over 170ms. A brighter tapered cyan highlight starts 65ms into the expansion and travels for 470ms from top center clockwise to bottom center. It widens at the corners, starts fading at the bottom-right turn, and disappears at bottom center. Releasing Alt cancels the highlight and shrinks the panel over 200ms while fading out over 220ms. Typography uses Consolas Bold, matching Nord's actual Windows font fallback. Header labels are omitted.
Windows' reduced-animation preference is respected. Labels are hints, not app rules.

## Build/install from Git Bash

```bash
bash workspace-indicator/rebuild.sh
```

Uses the Windows .NET Framework compiler already installed. No package installation
or background web server. The companion runs alongside Zebar and starts/stops with
GlazeWM. One instance only; it reconnects if GlazeWM restarts. The regular bar rebuild
script does not rebuild this separate C# application.

## Customize

Card labels come from `workspaces.json` in the install folder and update live.
Edit `NordWorkspaceIndicator.cs`: `Names` for cards, `CardWidth`,
`CardStep`, `CardHeight` for layout, the constructor for colors and padding, and
`PinToOriginMonitor` for placement. Rebuild after changes.

It reads left Alt's held state and GlazeWM's read-only localhost WebSocket
endpoint. A keyboard hook recognizes left-Alt + digit shortcuts and replaces their Alt release with an unassigned menu-mask key pair followed by Alt-up, preventing menu focus in the destination app. Other keys pass through; no keystrokes are stored. It never changes workspaces or moves app windows.
While hidden it samples workspace state twice a second; while held, roughly 22 times
a second. The UI animation runs independently of those queries.

To stop it now, end `NordWorkspaceIndicator.exe` in Task Manager. To disable automatic
startup, remove its `shell-exec` entry from GlazeWM's `general.startup_commands`.

## Verification

`NordWorkspaceIndicator.exe --check <output.txt>` tests workspace-0 parsing and a
live GlazeWM query. `--render <preview.png>` renders a deterministic preview of the
actual WPF controls without opening an overlay window.
