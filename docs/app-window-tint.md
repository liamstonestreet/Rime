# Wallpaper-tinted app windows

In CC > Configure > Appearance > App windows, enable **Wallpaper tint**. The feature is off by default. Install the optional [Translucent Windows](https://windhawk.net/mods/translucent-windows) mod through Windhawk first; **Windhawk setup** opens its page. Then click **Retry**. The current implementation targets standard installed Windhawk, not portable mode or custom per-user configuration scopes.

The integration sets the mod to Mica. **Richer tint** selects Mica Alt. These Windows materials derive their appearance from the desktop wallpaper automatically; there is no continuous registry polling or per-frame mod reload. Windows controls the final tint and timing; this does not promise an exact match to the extracted CC palette or its transition clock. Mod settings reload once when enabling, changing strength/exclusions, or disabling.

Requires Windows 11 22H2 or newer, Windows transparency effects enabled, and energy saver off. Some affected apps may need reopening. Standard Win32/themed surfaces are the main target. Chromium/Electron, Qt, WinUI and hardcoded app surfaces may not change; Signal, Discord, Spotify and browser interiors generally require individual themes. File Explorer's modern WinUI areas require the separate File Explorer Styler configuration described by the mod author. This is not a universal app recoloring switch.

The preset enables the mod's custom theme background rendering for compatibility, disables its global system colors and flyout effects, and leaves its existing process rules intact. It never sets whole-window opacity. Other installed mods can still conflict; disable this option if an app renders incorrectly.

**Excluded apps** accepts executable names separated by commas. Existing custom exclusions are retained, and rice helpers, WebView2 and Lightshot are always excluded. Applying changes may require permission to write the installed Windhawk registry configuration. CC reports permission failures and never elevates itself automatically.

Before each modification the original value is journaled at `%LOCALAPPDATA%\NordRice\window-tint-restore.json`. Turning the option off restores owned values and removes values which were originally absent. Independent edits made in Windhawk are preserved; if they conflict while the option is enabled, CC asks you to turn it off and on to release/reapply the preset. The journal survives CC restarts or partial failures. Closing CC does not turn an enabled preset off. Uninstalling the mod while enabled retains the restore record; reinstall it and disable the option to restore.

Validation uses fake settings/journal stores, covering missing-mod feedback, repeated-update suppression, Mica/Mica Alt selection, exclusions, persisted recovery, external-edit preservation and write-before-journal prevention. No live mod installation, registry write or app background was changed during automated verification. Real rendering and reload behavior still require testing after installation.

References: [Mica](https://learn.microsoft.com/en-us/windows/apps/design/style/mica), [Win32 Mica and Mica Alt](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-mica-win32), and the [mod source and compatibility notes](https://github.com/ramensoftware/windhawk-mods/blob/main/mods/translucent-windows.wh.cpp).
