# Easy installation

From an extracted source folder, run this single command in normal **64-bit Windows PowerShell**:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -CleanDefaults
```

If publishing only the `nord-custom` folder as the repository root, use `install.ps1 -CleanDefaults` instead. The root `setup.ps1` is the friendly entry point for the complete source tree.

Setup detects existing applications and installs missing Node.js LTS, GlazeWM and Zebar through WinGet. If WinGet is missing, it attempts Microsoft's `Microsoft.WinGet.Client` repair flow. Windows may display UAC prompts for application installation. Network access and permission to install applications are required; organizational policies and unsupported Windows editions can block setup. Git, Visual Studio, Spotify, Discord, Obsidian and Windhawk are not required. Windows must supply .NET Framework 4.8 and its 64-bit compiler.

Setup starts GlazeWM once if needed to generate its default configuration, then merges the rice into that config with backups. Existing rice settings are preserved. On a new account, workspaces have no assigned apps, `/openall` starts empty, wallpapers use the bundled `presets/wallpapers` library, and personal quick actions are disabled. Configure → Wallpaper library lets you choose another folder, open the current folder to add images, or return to the presets. Existing wallpaper directories are preserved on updates.

Optional switches: `-Spicetify` requires an existing Spotify/Spicetify installation; `-Notifications` installs the Windows notification companion and may need signing trust and notification-access approval. These integrations are not installed silently as core dependencies. The local inbox works without Windows notification access.

## GitHub one-command distribution

Repository: `https://github.com/liamstonestreet/Rime`. The following command becomes usable once `bootstrap.ps1` and the source are published to `main`:

```powershell
irm https://raw.githubusercontent.com/liamstonestreet/Rime/main/bootstrap.ps1 | iex
```

The bootstrap downloads a GitHub source ZIP, retains it under `%LOCALAPPDATA%\NordRice\source`, and runs setup with clean defaults. Git is unnecessary. Prefer a tested release tag or commit SHA for `-Ref` and the script URL rather than a moving branch. Private repositories need authenticated distribution, which this bootstrap does not implement.

For updates, rerun the same command. Existing settings and bridge tokens are retained. Before publishing, include only the intended source tree; do not upload the parent folder's backups, personal configuration archives, credentials or generated signing material. A release archive and the bootstrap URL still need to be published before the remote command is usable.

## Verification status

Scripts parse under Windows PowerShell 5.1. Executable lookup and clean-default generation have local smoke checks. No application installs, certificate changes, or fresh Windows VM end-to-end run were performed during development. A clean Windows VM test is required before labeling a GitHub release as tested.

WinGet recovery follows [Microsoft's documented repair flow](https://learn.microsoft.com/windows/package-manager/winget/troubleshooting). Initial configuration generation follows [GlazeWM's startup behavior](https://github.com/glzr-io/glazewm/blob/main/README.md).
