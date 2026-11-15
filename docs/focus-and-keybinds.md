# Focus sessions and indicator keys

## Focus

Open **CC > Focus**. Set an optional goal, work minutes, break minutes and the apps whose windows you want hidden. Choose 25/45/60 minutes or type any whole work/break duration from 1 to 1440 minutes. Empty the app field for a timer-only session. App names are process names (as shown in Task Manager), separated by commas; `.exe` is optional.

**Start focus** starts a work period. **Pause/Resume**, **+5 min**, **Take a break / Start work**, and **End** control the session. A thin ring shows remaining time. Completed work periods are counted within the current session. Duration/app settings are locked until you end the session; the goal stays with the whole work/break cycle.

With **Start the break automatically** enabled, finishing work immediately starts your custom break. Otherwise, the break waits for you to start it. After every completed break, the next work period waits for an explicit start. The bar clock displays the current work/break countdown, paused state, or ready reminder on every monitor; click it during a session to open and pin the Focus tab. The goal appears in the badge tooltip. There are no forced full-screen prompts or sounds.

Selected app windows are hidden only during running work. Pausing, starting a break or ending restores them without activating them; resuming work hides them again. New windows belonging to those apps are also hidden during work. Apps keep running: this does not mute their audio, stop downloads, kill processes, or prevent launches. Workspaces and your current palette stay unchanged. Elevated apps may resist hiding. Existing app-block/workspace-lock settings from the old Focus mode are ignored by new sessions.

Starting a session saves its duration defaults and app list in `focus-mode.json`. The phase, optional goal, deadline/paused remainder, session count and recovery journal persist in `focus-state.json`. Window identities are journalled **before** hiding, with handle, PID and process start identity; restoration rejects stale/reused identities. Paused sessions survive helper restarts without consuming paused time. Running sessions use an absolute UTC deadline. If a deadline was missed during shutdown/sleep, one phase transition is applied on recovery, rather than crediting imaginary cycles. An old hard-focus session's owned GlazeWM edits are restored on migration, preserving unrelated edits.

Keep the helper running for timely transitions/restoration. If stopped, its next start recovers journalled windows/session state. End the session before uninstalling; keep the state file while windows are hidden.

The sixth tab is created only on first use. Its UI timer runs once a second only while visible. The session engine runs once a second while a session or window recovery is pending, with no idle session timer. Bar updates share the existing changed-state bridge across monitors.

## Indicator keybinds

Open **CC > Rice > Indicator keybinds**. Pick a single hold key for each indicator and press **Apply keybinds**:

- Workspace indicator: **Left Alt** by default. It displays the current workspace while held.
- Wallpaper indicator: **Right Ctrl** by default. Hold it and use **[ / ]** to browse. Release applies; Escape cancels. Other shortcuts cancel browsing and pass through.

Choices include left/right Alt, Ctrl and Shift, F1–F24, and Disabled. These are hold keys, not multi-key shortcut chords; keys otherwise continue to reach applications. Assign different keys to the two indicators. Function keys can retain application-specific actions, so modifier keys are usually the more useful choice.

Bindings persist in `indicator-keys.json` and apply without restarting either helper. Rebinding a held key closes its old gesture. **Defaults** stages Left Alt / Right Ctrl; **Apply keybinds** saves them. Disabling an indicator does not alter the other one. Workspace-number shortcuts remain configured in GlazeWM. Wallpaper tap shortcuts/folder remain in `wallpapers.json`; its former `hold` field is superseded by the shared indicator settings.

The installer keeps saved keybinds on reinstall. Existing installs without the new settings file receive Left Alt / Right Ctrl.

## Checks

Native checks cover the work/break state machine, custom timing, pause/restart serialization, late recovery, ring formatting, theme preservation, lazy UI/hidden timer behavior, and mocked window restoration/journalling. The indicator's isolated key checks cover physical/injected/raw releases and rebinding. Mock window checks do not hide real desktop windows or kill processes. All five tabs retain stable scroll gutters and the per-monitor max-height setting (50% by default). Widgets & layout now sits directly above Keybinds in Rice.
