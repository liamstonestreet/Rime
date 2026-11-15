# Workspace indicator styles

In **Control Center > Configure > Workspace indicator**, choose:

- **Classic**: the original compact strip of all ten workspaces.
- **Gallery**: a much taller, three-card layout. The current workspace is large and centered; only its immediate previous and next neighbors appear beside it. Cards glide, scale and fade as the workspace changes. Neighbor order wraps between `0` and `1`.
- **Ribbon**: a restrained typographic layout with a large workspace number, its configured label, and a fine position rail. The number slides/crossfades while the marker glides along the rail.

The selection saves in `bar-appearance.json` as `indicatorStyle` (`classic`, `gallery`, `ribbon`). It applies the next time you hold the indicator key; no helper restart is needed. **Classic** remains the default. Workspace names/labels come from the existing `workspaces.json`; all styles use the shared font, wallpaper palette, frost and corner-radius settings.

All styles use the existing indicator key and GlazeWM connection. Change the key under **Configure > Indicator keybinds**. Workspace-number shortcuts are unchanged. Rice's animation speed/style and Windows' animation preference apply to the new transitions. Quick changes replace the current motion, and the view settles after hiding. Styles add no polling loop, keyboard hook or perpetual animation.

Gallery is designed at 790 x 300 logical pixels (compared with Classic's 820 x 134); Ribbon is 600 x 176. They open on the foreground window's monitor, using the existing indicator placement behavior.

Isolated checks cover three-card visibility, wraparound, current-card placement, rapid-switch settling, label updates, Ribbon digit/marker cleanup and clipped viewports. Previews can be rendered without showing a live overlay:

```powershell
.\workspace-indicator\NordWorkspaceIndicator.exe --render-style gallery gallery.png
.\workspace-indicator\NordWorkspaceIndicator.exe --render-style ribbon ribbon.png
```

The App Rules bar button uses a small folded-map wayfinder, with a quiet route and destination dot. Enabled state uses the current accent without a filled background. Spotify and Helium keep the same responsive width whether playing, paused or without a session; idle players show “Nothing playing.”

## Workspace cycling

Alt+] focuses the next configured workspace; Alt+[ focuses the previous one. The installer adds GlazeWM bindings using `oem_close_brackets` / `oem_open_brackets` and `focus --next-workspace` / `focus --prev-workspace`. The configured order is 1,2,3,4,5,6,7,8,9,0, so forward wraps 9 -> 0 -> 1 and backward wraps 1 -> 0 -> 9. Empty workspaces are included, and cycling crosses monitor assignments. The indicator follows the focused workspace, and its Alt-release menu guard covers bracket navigation. Wallpaper browsing remains on its separate hold key (Right Ctrl by default). Re-running install updates one marked binding block rather than duplicating it.
