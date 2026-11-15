# /openall and /closeall

**/openall** opens your selected app list. Configure it in **CC → Rice → Spotlight: /openall apps**: expand the section, filter/check installed apps, then **Save app list**. Selection persists in the existing `launch-apps.json`; legacy app names are supported, new selections use installed app IDs, and unrelated settings are preserved. Uninstalled saved entries remain visible and can be unchecked.

Apps already present in the live workspace tree are skipped using existing app-rule process/title matching, or configured/executable process identity where no rule exists. New windows follow the existing GlazeWM app rules, including their enabled/disabled setting. The command does not temporarily rewrite/reload Glaze config or repeatedly refocus workspaces. `/launch` is retained as a compatibility alias; `/openall` is the name shown in command discovery.

**/closeall** queries every GlazeWM workspace tree and iterates the actual managed windows represented in the workspace taskbars. This includes running pinned apps and unpinned taskbar windows across both monitors. Closed pins, background-only processes, and the rice/system UI helpers are excluded. This scope deliberately follows the taskbars rather than only a static startup list.

It now **force-terminates app processes immediately**, once per PID, without save dialogs or close-to-tray behavior. Unsaved changes can be lost, and interrupting a write can damage app data. Shared-process windows (for example multiple browser windows) close together. Before termination it revalidates the window/PID, process name, process creation time and current Windows session. The desktop shell, core system processes, GlazeWM and rice helpers are protected. It does not kill unrelated background-only processes or terminate other sessions. Elevated/inaccessible apps may remain; failures are reported without stopping the rest of the batch. The summary reports termination requests rather than guaranteeing every process has finished exiting. No confirmation dialog is inserted.

For normal PC shutdown, save important work and use Windows' ordinary Shut down action. Windows gives apps shutdown notifications and cleanup opportunities. Running this force-close command before normal shutdown is unnecessary and more abrupt.

If the workspace query fails, the command stops with a visible error; it does not fall back to enumerating arbitrary Windows processes. Checks use synthetic workspace trees and injected launch/close callbacks. No real apps are launched, closed or killed during verification.
