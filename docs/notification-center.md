# Notifications panel

Notifications remains separate from Control Center. Click the Notifications bell widget (initially before the workspaces), to toggle a full-height panel on that monitor. The panel fills the height below Zebar down to the bottom of the display, keeping the bar accessible. Only the notification list scrolls; the header, search, filters, controls and footer remain fixed.

**Win+Shift+N** toggles the panel on the active window's monitor. Change it in **Configure > Keybinds > Notification center**. Press Enter or leave the field to apply; blank disables it. Conflicting shortcuts report an error and retain the previous working binding. Shortcut registration uses its own message-only window and does not change GlazeWM workspace bindings. Games suppress panel activation.

Escape, Close, clicking outside, or activating the bell/shortcut again dismisses the panel. It retains themed tint/frost, matching corners and edge styling, slide-in/out animation, unread counts, filters and notification-card animations. Hover alone no longer opens it. There is no edge gesture or frame-off handle, and the old edge sampler is not built into the runtime. Old edge settings are ignored and removed when appearance settings are next saved.

## Windows collection

Install the companion once from the repository:

```powershell
powershell -ExecutionPolicy Bypass -File .\nord-custom\install.ps1 -Notifications
```

Open the notification panel, choose **Connect Windows**, then **Allow Windows access**. Windows presents its notification-access prompt. The connection status appears near the top. The companion is a per-user MSIX; its signing key stays non-exportable in CurrentUser/My. Its specific public certificate needs LocalMachine/TrustedPeople trust once, requiring administrator access. The installer prints the exact import command if needed; it never adds a root CA or exports a private key. Windows SDK packaging tools are required to build it.

The collector uses the supported Windows notification-listener API. It reads notifications delivered to Windows, including text and app logos, not arbitrary app conversations or sender profile photos. Embedded app logos are bounded, with sender initials as a fallback. There is no remote avatar lookup or Signal database access.

## Inbox and privacy

Search by message/title/source, filter by app or All/Unread/Pinned/History, mark read/unread, pin, archive, restore, copy text or open the source app. Archive/read/pin apply only to this inbox. **Dismiss** removes that specific Windows notification after revalidating its native identity; it does not mark a chat message read in its original app.

History is encrypted locally at `%LOCALAPPDATA%\NordRice\notifications.bin`, retained for 7 days / 500 items, with pinned items protected within the count limit. Existing unreadable history is preserved. Encryption failure has no plaintext fallback. Message contents are not written to diagnostic logs or the bar badge. Communication uses the existing authenticated loopback bridge. No notification data is uploaded.

## Verification

Checks cover full-height strip placement on positive/negative monitor origins, scaling, geometry bounds, shortcut parsing, fixed controls/list sizing, inbox state, bounded retention, lifecycle and rendering of the assembled panel with synthetic messages. Production builds and bar type checks are also run. No real notifications are dismissed and no global shortcut is registered by the automated checks. Actual interaction and shortcut availability require verification after installation.

The bell is configurable through **Widgets and Layout** like other bar widgets: it can be hidden, reordered or moved between lanes. Existing layouts gain it at the far left once; explicit placement and visibility then persist. Moving the bell never changes the panel's left-edge/full-height placement on that monitor. The keyboard shortcut remains available when the bell is hidden.

## Frame attachment

With the screen frame enabled, the panel extends four logical pixels into Zebar's edge and its lower join follows the bottom frame's inner edge. Inward fillets join its right boundary to those horizontal edges; there is no separate top/bottom panel border. The bar's notification mask is independent of CC's mask, so closing either does not restore a line through the other. The native frame clips the covered fill and outline using retained layers rather than rebuilding frost during animation. With the frame off, the panel is flush below Zebar with square attachment corners and only a right outline. Notification content stays inset from the attachment bands.
