# Session recovery

The installer adds `Nord Rice Runtime.lnk` to the current user's Startup folder. The small native `NordRuntime.exe` checks every five seconds while GlazeWM is running and restores missing Zebar, indicator, Spotlight, and control-center processes. It waits after resume, stops recovery during suspend/logoff, and does not restart healthy processes. It uses the installed folder, so moving the source repository has no effect. `runtime.log` records restarts. Closing Zebar alone is temporary while recovery is enabled; exit `NordRuntime.exe` first for maintenance. Remove the Startup shortcut to disable recovery on future logins. GlazeWM's own sign-in startup remains required.

The bar volume slider uses the authenticated control-center bridge, avoiding Zebar's native audio provider. Audio endpoint handles are acquired and disposed for each sample; samples are shared across monitor requests for 500 ms. Missing audio devices disable the slider until an endpoint returns.

The Alt indicator uses physical keyboard edges for visibility. Synthetic Alt releases from workspace shortcuts and the age of idle workspace data must not close a held gesture. It reinstalls its keyboard hook after resume/unlock. Workspace changes are event-driven, with a 500 ms safety refresh while held.
