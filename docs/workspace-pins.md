# Workspace taskbar pins

Right-click a running taskbar icon and choose **Pin to this workspace**. Its icon stays in that workspace's taskbar after the app closes. Click the closed icon to open it in that workspace; an already running app is focused instead of launched again.

If the app already belongs to another workspace, the menu offers **Move pin here (from workspace X)**. This moves its existing app rule, preserving process/title matching such as Helium's browser-specific rule. Manual window moves remain allowed. **Unpin from this workspace** hides the persistent icon and keeps the placement rule. Closed pins also have an **Open app** and unpin menu.

Pins use the existing `workspaces.json` and app-rule generator. Automatic placement for launches outside the taskbar follows the App Rules on/off setting. Pin clicks target the workspace displayed by the clicked monitor. Pinning does not start a background app-relaunch service or add applications to Windows startup.

Edits preserve custom JSON properties, save atomically, and back up the previous file to `workspaces.json.before-taskbar-pins.bak`. Existing entries remain pinned by default; `pinned: false` hides a pin without deleting its rule. New entries store their installed app ID, or an executable path for portable apps. A missing/unidentifiable launcher is shown as unavailable rather than pinning a shared runtime host.

No extra idle polling is added. Pin updates broadcast to every bar immediately; existing file watchers also handle manual edits.

Drag taskbar icons horizontally to reorder them. Drag across neighboring icons to move into their position. Release inside the strip to save; release outside it or press Escape to cancel. Pointer capture handles dragging directly, without WebView native drag-and-drop. Normal clicks still focus/open apps, and right-click menus are unchanged. Order saves per workspace in `taskbar-order.json` and updates every bar. Pins retain their IDs; ordinary running apps use process names and window slots, so restarting an app retains its place. Individual document identity across restarts is not tracked. Ordering never moves windows or changes their app rules.

Control Center defaults to 759 logical pixels wide (1.3 times its former 584). Change **Control Center width** in Rice; it saves with appearance settings and fits within the active monitor. Custom thick focus borders are temporarily disabled, including their event hooks, because workspace/cloaking changes can leave the overlay without a current target. Standard GlazeWM/Windows borders and the screen frame remain available.
