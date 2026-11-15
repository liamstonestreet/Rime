# Spotlight

Use Ctrl+Space for the floating launcher, or type in the bar. The same providers and commands work in both.

Type an app name, a calculation (`sqrt(144) + 8`), a conversion (`5 km to mi`), a local file path, or a website address. Ordinary text finds apps, matching running windows and files, with a web-search action underneath. Empty search retains the existing recent-app ordering.

## Commands

| Command | Action |
|---|---|
| `/help` or `/` | Browse available commands; typing filters them |
| `/apps spotify` | Search installed apps |
| `/calc 20% of 150` | Calculate and copy the result with Enter |
| `/calc 32f to c` | Convert units |
| `/files report` | Find files in personal folders |
| `/folders project` | Find folders |
| `/windows discord` | Focus a running window, including other GlazeWM workspaces |
| `/web query` | Search Google in the default browser |
| `/settings sound` | Open a matching Windows Settings page |
| `/copy text` | Copy text |
| `/home`, `/desktop`, `/documents`, `/downloads` | Open a personal folder |
| `/recycle` | Open Recycle Bin |
| `/date`, `/time` | Copy the displayed local date/time |
| `/launch` | Open the apps configured in launch-apps.json |
| `/refresh` | Refresh installed apps, running windows and the file cache |
| `/lock` | Lock the Windows session |

The calculator supports parentheses, `+ - * / ^`, scientific notation, postfix percentages and factorials, `pi`, `e`, implicit multiplication, and `sqrt`, `abs`, `round`, `floor`, `ceil`, `sin`, `cos`, `tan`, `ln`, `log`, `exp`, `min`, `max`, `pow`. Trigonometric functions use radians. `20%` is `0.2`; use `20% of 150` for a percentage of an amount. Conversions cover length, mass, time, temperature, angles, and decimal/binary byte units. Currency conversion is not included.

## Keyboard

- Up/Down chooses a result; Enter opens it or copies a calculation.
- Tab completes a selected command into the search field.
- Ctrl+C copies the selected result when no input text is selected; normal text selection still copies normally.
- Ctrl+Enter searches the current query on the web.
- Escape closes the launcher/dropdown.

Command completion stays synchronized between the native dropdown and the originating bar. Responses from older requests or another monitor do not overwrite the current text.

## Search scope and efficiency

File-name search covers Desktop, Documents and Downloads. It builds a background cache on demand, limited to 50,000 entries, a two-second scanning budget and eight directory levels. Hidden/system entries, reparse points and dependency trees such as node_modules/.git are skipped. A directory enumeration already in progress can take longer if Windows storage is slow. This is a bounded personal-folder search, not a complete disk/content index. The cache expires after two minutes; `/refresh` invalidates it. Direct local paths support `~` and environment variables.

Running windows are refreshed when the launcher opens. GlazeWM provides the window IDs and workspace-aware activation; a native window-list fallback is used if GlazeWM is unavailable. File results use cancellation and query versions so old searches cannot replace newer results. Navigation does not reread the app catalog.

Settings commands use Microsoft's documented [ms-settings URI targets](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings). Web searches only leave the machine after selecting the web action. No typed text is interpreted as a shell command.

## Checks

Build with `powershell -ExecutionPolicy Bypass -File spotlight/build.ps1 -Checks`, then run `spotlight/checks/SpotlightChecks.exe --check`. Tests use temporary fixture files and synthetic results; they do not execute launcher actions or change the clipboard. `--render <png> <query>` and `--render-dropdown <png> <query>` create isolated previews. Spotlight and CC share `shared/ScrollChrome.cs` for their slim, fixed-gutter scrollbars.
