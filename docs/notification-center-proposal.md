# Notifications tab proposal

Status: first implementation delivered; see [implementation and setup](notification-center.md). Current order: Menu, Audio, Notifications, Configure, Focus. Remaining features below are the roadmap, not a claim of completed functionality.

## Experience

A single inbox for Windows app notifications and shell activity: focus/break completion, calendar reminders and actionable rice errors. Keep calendar browsing in Menu, playback in Audio, and notification preferences in Configure. The Notifications tab contains the inbox and a compact preferences shortcut.

Use the same frosted surface, palette, typography, thin scrollbar, corner radius and motion settings as the rest of CC. Header: **Notifications**, unread count, quiet-mode toggle and overflow menu. Below it: search and **All / Unread / Pinned / History** filters, with an app/source filter. The feed groups cards by app and conversation when reliable identifiers exist, then by Today / Yesterday / Older. Each group shows app icon, name, count and collapse control. Cards show title, wrapped message, timestamp, optional image and supported actions. Newest first by default; inserting a notification must preserve the reader's scroll position.

An optional bell in the bar opens this tab. Show one subtle unread dot/count on both monitors from the same shared state. Incoming items never take focus, switch workspaces or move the cursor. Optional transient banners appear on the chosen monitor, with timeout paused while hovered; avoid duplicate custom and native banners.

## Features and semantics

| Area | Expected behavior |
|---|---|
| Reading | Expand long messages and groups; relative times with exact timestamp tooltip; keyboard navigation, visible focus and screen-reader labels. Opening the tab alone does not mark every item read. |
| Search and filters | Search app, sender, title and body; combine source, date, unread and pinned filters; counts and a clear-filters action. |
| Actions | Open source app; deep-link to the exact item only when a trustworthy action is available; copy message/link; action buttons and inline reply for integrations that explicitly support them. Do not display nonfunctional actions. |
| Read state | Mark read/unread individually, by group or all. These are local inbox states, distinct from reading a message in Discord/Signal/etc. |
| Dismissal | Dismiss one card or a group, and clear displayed notifications. Respect the active filter: clearing filtered results must not remove unseen sources. Distinguish removing a native notification from archiving local history. |
| Undo | Undo local archive/clear for a short period. Do not imply an already removed Windows notification can be recreated or a remote action undone. |
| Follow-up | Pin important items and snooze for presets or a chosen time. A snoozed reminder is local; it does not alter the original app's message or task. |
| Quiet mode | Off / timed / scheduled quiet hours, optional Focus-session coupling and priority-app exceptions. Queue a quiet-mode digest instead of releasing a burst of banners afterward. Clearly distinguish shell quiet mode from Windows Do not disturb. |
| Per-app preferences | Show/hide from inbox, badge, custom banner, sound, priority, preview and grouping. Open Windows notification settings for OS-level controls that the shell cannot change reliably. |
| Privacy | Hide sensitive previews on lock; optional hidden previews until expanded; per-app history opt-out. Keep data local, encrypt persisted sensitive text with Windows protection, and offer delete-all-history. Never retain credential prompts or one-time codes by default if detectable. |
| History | Configurable retention, suggested default 7 days / 500 items; bounded image cache; survive helper restarts; prune safely. History starts when the collector is enabled, not before. |
| Shell sources | Focus/break timers, explicit calendar reminders with configurable lead times, and actionable errors such as failed lock-screen sync. Deduplicate retries; quiet repeated technical errors into one expandable card. Routine successes should normally remain silent. |
| Resilience | Permission-denied/revoked, source-disconnected, loading, empty inbox and no-search-results states. Show source status without repeatedly prompting for permission. Reconnect and synchronize without replaying old items as new. |

## Windows integration boundary

Microsoft's [notification-listener guide](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener) documents the manifest capability, explicit permission, text extraction, notification-change synchronization and removal. The [listener API](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.management.usernotificationlistener) exposes reading/removal and change events; it does not provide a general API for replaying every third-party toast action or sending arbitrary inline replies.

Therefore the first implementation step should be a small compatibility prototype on this PC: prove access from an appropriately manifested companion, receive real changes, handle permission revocation and dismiss a synthetic test notification. Our current loose .NET Framework executable installation should not be assumed sufficient; validate whether a packaged companion is necessary before changing distribution. Keep the existing CC executable and user settings intact.

Only notifications actually delivered to Windows are candidates for the Windows feed. An app's private in-app messages or its disabled notifications cannot be recovered automatically. This is not an account-level message sync service.

Native action/reply support is a later, source-specific enhancement. The baseline can offer a clearly labelled **Open app** action rather than pretending to open the exact conversation. Calendar Tasks require the existing Tasks-capable connection; private iCal feeds contain calendar events only.

Native toast suppression, global Windows Do not disturb, and source-level priority settings need separate capability validation. Local filtering must never be presented as muting Windows itself. Notifications should coexist with native Windows delivery until replacement behavior is explicitly configured and verified.

## Ownership and performance

One application-owned notification service merges provider snapshots, deduplicates IDs and owns read/archive/snooze state. An optional Windows companion owns OS access; shell providers publish typed events locally. A settings/store owner handles encrypted atomic persistence and retention. The WPF tab is created lazily and uses a virtualized list; the bar receives only badge changes.

Prefer notification-change events with coalesced delta synchronization, not screenshot/UI Automation scanning or constant polling. Bound the collection, image bytes and bridge queues. Hidden UI has no animation loop. Both monitors consume one service and store; a dismissal on one updates the other. Schedule snoozes and quiet-hour boundaries with timers for the next deadline rather than per-item polling.

## Delivery order

1. Prove Windows access, permission and dismissal with a disposable prototype; no production clear-all test.
2. Add the service, lazy tab, grouped feed, badge, local read state, search, per-item dismissal and bounded history. Cover stale IDs, revocation, reconnect and both-monitor consistency with fake providers.
3. Add shell events, snooze, priority/quiet-mode rules, Focus integration, privacy and per-app preferences.
4. Add optional banners and individually verified source actions/replies. Measure idle CPU/memory and notification bursts before enabling by default.

Recommendation: deliver the reliable inbox first, then add actions as each source's capabilities are proven. Do not ship an empty tab before a notification source works.
