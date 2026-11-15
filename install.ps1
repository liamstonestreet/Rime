<#
.SYNOPSIS
  Builds and installs the nord-rice Zebar bar and its helpers for GlazeWM, in one step.

.DESCRIPTION
  1. Checks prerequisites (GlazeWM, Zebar, Node.js, .NET Framework 4.8 compiler).
  2. Downloads pinned NuGet build dependencies once and verifies their SHA-256.
  3. Creates per-install bridge tokens (kept on reinstall) and builds the bar and helpers.
  4. Installs into the Zebar pack folder, merges the GlazeWM and Zebar settings it needs
     (backing both up first), and restarts the bar.
  Safe to re-run: it updates the install in place and keeps your settings.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install.ps1
#>
[CmdletBinding()]
param(
  # Where the pack is installed. Zebar loads every folder under ~/.glzr/zebar.
  [string]$PackDir = (Join-Path $env:USERPROFILE '.glzr\zebar\nord-rice'),
  # btop4win.exe opened by the CPU/memory widgets. Default: auto-detect, else Task Manager.
  [string]$BtopPath,
  # A previous install folder to copy settings and bridge tokens from (e.g. an older pack).
  [string]$MigrateFrom,
  # Where backups go. Default: ..\backups when it exists (WindowsRice layout), else .\backups.
  [string]$BackupDir,
  # Leave ~/.glzr/glazewm/config.yaml untouched (you then add the entries from the README yourself).
  [switch]$SkipGlazeConfig,
  # Install the Spotify link extension and repair Spicetify after Spotify updates.
  [switch]$Spicetify,
  # Build/register the optional Windows notification listener (per-user package and signing trust).
  [switch]$Notifications,
  # Do not restart Zebar and the helpers at the end.
  [switch]$NoRestart,
  # Neutral defaults on a fresh account; existing settings are always preserved.
  [switch]$CleanDefaults,
  [switch]$SkipPrerequisiteInstall,
  # Recompile every helper, even if its sources are unchanged.
  [switch]$Rebuild
)
$ErrorActionPreference = 'Stop'
$installMutex = New-Object Threading.Mutex($false, 'Local\NordRiceInstall')
$installOwned = $false
try {
  try { $installOwned = $installMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $installOwned = $true }
  if (-not $installOwned) { throw 'Another rice installation is already running.' }
$repo = $PSScriptRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
function Step([string]$message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Note([string]$message) { Write-Host "    $message" -ForegroundColor DarkGray }
function Warn([string]$message) { Write-Host "    ! $message" -ForegroundColor Yellow }
# npm prints warnings on stderr; PowerShell 5.1 would turn those into errors when output is redirected. Judge by exit code instead.
function Invoke-Native([string]$failure, [scriptblock]$command) { $saved = $ErrorActionPreference; $ErrorActionPreference = 'Continue'; try { & $command 2>&1 | ForEach-Object { "$_" } } finally { $ErrorActionPreference = $saved }; if ($LASTEXITCODE) { throw $failure } }
function Write-Utf8([string]$path, [string]$text) { [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false))) }

if (-not $BackupDir) {
  $parent = Join-Path $repo '..\backups'
  $BackupDir = if (Test-Path $parent) { [IO.Path]::GetFullPath($parent) } else { Join-Path $repo 'backups' }
}
$PackDir = [IO.Path]::GetFullPath($PackDir)

# ---------------------------------------------------------------------------------------------
Step 'Checking prerequisites'
if (-not $SkipPrerequisiteInstall) { . (Join-Path $repo 'setup-prerequisites.ps1'); Ensure-RicePrerequisites }
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$missing = @()
if (-not (Test-Path "$fx\csc.exe")) { $missing += '.NET Framework 4.8 (built into Windows 10/11; enable it in Windows Features)' }
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { $missing += 'Node.js LTS:  winget install OpenJS.NodeJS.LTS' }
$glazeCli = @("$env:ProgramFiles\glzr.io\GlazeWM\cli\glazewm.exe") + @(Get-Command glazewm -ErrorAction SilentlyContinue | ForEach-Object Source) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $glazeCli) { $missing += 'GlazeWM:  winget install glzr-io.glazewm' }
$zebarExe = @("$env:ProgramFiles\glzr.io\Zebar\zebar.exe") + @(Get-Command zebar -ErrorAction SilentlyContinue | ForEach-Object Source) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $zebarExe) { $missing += 'Zebar:  winget install glzr-io.zebar' }
$glazeConfig = Join-Path $env:USERPROFILE '.glzr\glazewm\config.yaml'
if (-not $SkipGlazeConfig -and -not (Test-Path $glazeConfig)) { $missing += "GlazeWM config at $glazeConfig (start GlazeWM once to create it)" }
if ($missing.Count) { $missing | ForEach-Object { Warn "Missing: $_" }; throw 'Install the missing prerequisites, then run install.ps1 again.' }
Note "Pack folder: $PackDir"

# ---------------------------------------------------------------------------------------------
Step 'Build dependencies'
# Pinned NuGet packages for the control center build. Downloaded once, verified, then reused.
$deps = @(
  @{ Id = 'NAudio'; Version = '1.10.0'; Dir = 'naudio'; Sha256 = 'E4A80EED41DAD794695F9C114432DE4667D20BDFA80825AA94D159708CF29217' },
  @{ Id = 'Microsoft.NETFramework.ReferenceAssemblies.net48'; Version = '1.0.3'; Dir = 'refs'; Sha256 = '8A7E348538E7EB91351696911689F49E3D4F63F8BAB517432BBE159B8B1104A2' }
)
foreach ($dep in $deps) {
  $target = Join-Path $repo "control-center\$($dep.Dir)"
  if (Test-Path $target) { Note "$($dep.Id) $($dep.Version): present"; continue }
  $download = Join-Path $env:TEMP "$($dep.Id).$($dep.Version).zip"
  Note "Downloading $($dep.Id) $($dep.Version) from nuget.org"
  [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
  Invoke-WebRequest -UseBasicParsing "https://www.nuget.org/api/v2/package/$($dep.Id)/$($dep.Version)" -OutFile $download
  $hash = (Get-FileHash $download -Algorithm SHA256).Hash
  if ($hash -ne $dep.Sha256) { Remove-Item $download -Force; throw "Checksum mismatch for $($dep.Id) ($hash). Refusing to use it." }
  Expand-Archive -LiteralPath $download -DestinationPath $target
  Remove-Item $download -Force
}

# ---------------------------------------------------------------------------------------------
Step 'Bridge tokens and install settings'
# Random per-install keys for the local WebSocket bridges (bar <-> helpers). Kept on reinstall so
# the Spicetify extension keeps working; taken from -MigrateFrom on a first install.
New-Item -ItemType Directory -Force $PackDir | Out-Null
$tokensFile = Join-Path $PackDir 'bridge-tokens.json'
if (-not (Test-Path $tokensFile) -and $MigrateFrom -and (Test-Path (Join-Path $MigrateFrom 'bridge-tokens.json'))) { Copy-Item (Join-Path $MigrateFrom 'bridge-tokens.json') $tokensFile }
$tokens = $null
if (Test-Path $tokensFile) { try { $tokens = Get-Content $tokensFile -Raw | ConvertFrom-Json } catch { } }
$hex64 = '^[0-9a-f]{64}$'
if (-not $tokens -or $tokens.center -notmatch $hex64 -or $tokens.spotlight -notmatch $hex64) {
  $rng = New-Object Security.Cryptography.RNGCryptoServiceProvider
  $newToken = { $bytes = New-Object byte[] 32; $rng.GetBytes($bytes); -join ($bytes | ForEach-Object { $_.ToString('x2') }) }
  $tokens = [pscustomobject]@{ center = (& $newToken); spotlight = (& $newToken) }
  Write-Utf8 $tokensFile (ConvertTo-Json $tokens)
  Note 'Generated new bridge tokens'
} else { Note 'Reusing existing bridge tokens' }

if (-not $BtopPath) {
  $candidates = @(Get-Command btop4win.exe, btop4win -ErrorAction SilentlyContinue | ForEach-Object Source) + @(
    "$env:USERPROFILE\Apps\btop4win\btop4win.exe", "$env:LOCALAPPDATA\Programs\btop4win\btop4win.exe",
    "$env:USERPROFILE\scoop\apps\btop4win\current\btop4win.exe")
  $winget = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
  if (Test-Path $winget) { $candidates += @(Get-ChildItem $winget -Directory -Filter '*btop4win*' | ForEach-Object { Get-ChildItem $_.FullName -Recurse -Filter btop4win.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }) }
  $BtopPath = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if ($BtopPath) { Note "btop4win: $BtopPath" } else { Note 'btop4win not found: CPU/memory widgets will open Task Manager (pass -BtopPath to change)' }
$jsString = { param($value) '"' + ([string]$value).Replace('\', '\\').Replace('"', '\"') + '"' }
Write-Utf8 (Join-Path $repo 'src\lib\install-config.generated.ts') @"
// Generated by install.ps1 for this machine. Do not edit or commit.
export const bridgeTokens = { center: $(& $jsString $tokens.center), spotlight: $(& $jsString $tokens.spotlight) };
export const btopPath = $(& $jsString $BtopPath);
"@

# ---------------------------------------------------------------------------------------------
Step 'Building the bar'
Push-Location $repo
try {
  if (-not (Test-Path 'node_modules')) { Note 'npm ci'; Invoke-Native 'npm ci failed' { npm.cmd ci --no-audit --no-fund } }
  Invoke-Native 'Bar build failed' { npm.cmd run build:glazewm --silent }
} finally { Pop-Location }

Step 'Building the helpers'
# The .NET Framework compiler stamps every build differently, and Smart App Control judges unsigned
# programs per build, so a needless rebuild can turn an allowed helper into a blocked one. Rebuild a
# helper only when its sources change (or with -Rebuild).
foreach ($helper in @(@{dir='control-center';exe='NordControlCenter.exe'}, @{dir='spotlight';exe='NordSpotlight.exe'}, @{dir='workspace-indicator';exe='NordWorkspaceIndicator.exe'}, @{dir='runtime';exe='NordRuntime.exe'})) {
  $folder = Join-Path $repo $helper.dir
  $sources = @(Get-ChildItem $folder -File | Where-Object { $_.Extension -in '.cs', '.ps1', '.xaml' }) + @(Get-ChildItem (Join-Path $repo 'shared') -Filter *.cs -File)
  $sourceStamp = ($sources | Sort-Object FullName | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash }) -join ''
  $stampFile = Join-Path $folder '.build-stamp'
  if (-not $Rebuild -and (Test-Path (Join-Path $folder $helper.exe)) -and (Test-Path $stampFile) -and (Get-Content $stampFile -Raw).Trim() -eq $sourceStamp) { Note "$($helper.dir): unchanged, keeping the existing build"; continue }
  Note "$($helper.dir)\build.ps1"
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $folder 'build.ps1')
  if ($LASTEXITCODE) { throw "$($helper.dir)\build.ps1 failed" }
  Write-Utf8 $stampFile $sourceStamp
}

# ---------------------------------------------------------------------------------------------
Step 'Installing'
# Helpers are single-instance (named mutexes, fixed ports), so stop any running copy first,
# including one started from an older pack folder.
Get-Process NordRuntime -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force; $_.WaitForExit() }
Get-Process NordControlCenter, NordSpotlight, NordWorkspaceIndicator -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force; $_.WaitForExit() }
Start-Sleep -Milliseconds 500

$live = Join-Path $PackDir 'dist\glazewm'
if (Test-Path $live) {
  $backup = Join-Path $BackupDir "bar-installs\nord-rice-before-install-$stamp"
  New-Item -ItemType Directory -Force $backup | Out-Null
  Copy-Item $live (Join-Path $backup 'glazewm') -Recurse
  if (Test-Path (Join-Path $PackDir 'zpack.json')) { Copy-Item (Join-Path $PackDir 'zpack.json') $backup }
  Note "Backed up the previous bar to $backup"
}
# Bar files: copy the new build, then drop anything it no longer has (old hashed bundles).
$built = Join-Path $repo 'dist\glazewm'
New-Item -ItemType Directory -Force $live | Out-Null
Copy-Item (Join-Path $built '*') $live -Recurse -Force
Get-ChildItem $live -Recurse -File | Where-Object { $_.Name -ne 'rice-widgets.json' -and -not (Test-Path (Join-Path $built $_.FullName.Substring($live.Length + 1))) } | Remove-Item -Force
Get-ChildItem $live -Recurse -Directory | Sort-Object FullName -Descending | Where-Object { -not (Get-ChildItem $_.FullName -Force) } | Remove-Item -Force

# Helpers, their assets and scripts.
$files = @{
  'runtime\NordRuntime.exe' = 'NordRuntime.exe'
  'control-center\NordControlCenter.exe' = 'NordControlCenter.exe'; 'control-center\NAudio.dll' = 'NAudio.dll'
  'control-center\control-center-styles.xaml' = 'control-center-styles.xaml'
  'spotlight\NordSpotlight.exe' = 'NordSpotlight.exe'; 'spotlight\launch-apps.ps1' = 'launch-apps.ps1'
  'workspace-indicator\NordWorkspaceIndicator.exe' = 'NordWorkspaceIndicator.exe'
  'open-player.ps1' = 'open-player.ps1'
}
foreach ($entry in $files.GetEnumerator()) { Copy-Item (Join-Path $repo $entry.Key) (Join-Path $PackDir $entry.Value) -Force }
# The control center now writes GlazeWM's workspace config itself; the old script is no longer used.
Remove-Item (Join-Path $PackDir 'workspace-rules.ps1') -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $PackDir 'Fonts') | Out-Null
Copy-Item (Join-Path $repo 'control-center\Fonts\*') (Join-Path $PackDir 'Fonts') -Force

# User settings: keep existing ones, take them from -MigrateFrom, else install the defaults.
# workspaces.json replaced workspace-apps.json: carry an older install's app list into the new
# format (labels and monitors start from the defaults), and keep the old file as .migrated.
$oldApps = Join-Path $PackDir 'workspace-apps.json'
if (-not (Test-Path (Join-Path $PackDir 'workspaces.json')) -and (Test-Path $oldApps)) {
  $legacy = @((Get-Content $oldApps -Raw | ConvertFrom-Json) | ForEach-Object { $_ })
  $model = Get-Content (Join-Path $repo 'workspaces.json') -Raw | ConvertFrom-Json
  foreach ($ws in $model.workspaces) {
    $apps = @($legacy | Where-Object { [string]$_.workspace -eq [string]$ws.name } | ForEach-Object {
      $app = [ordered]@{ id = $_.id; catalogPattern = $_.catalogPattern; processPattern = $_.processPattern }
      if ($_.titlePattern) { $app.titlePattern = $_.titlePattern }
      [pscustomobject]$app
    })
    $ws | Add-Member -NotePropertyName apps -NotePropertyValue $apps -Force
  }
  Write-Utf8 (Join-Path $PackDir 'workspaces.json') (ConvertTo-Json $model -Depth 6)
  Move-Item $oldApps "$oldApps.migrated" -Force
  Note 'Migrated workspace-apps.json into workspaces.json'
}
$settings = @{
  'workspaces.json' = 'workspaces.json'
  'rice-widgets.json' = 'public\rice-widgets.json'; 'control-center-layout.json' = 'control-center\control-center-layout.json'
  'launch-apps.json' = 'spotlight\launch-apps.json'; 'focus-mode.json' = 'control-center\focus-mode.json'
  'wallpapers.json' = 'wallpapers.json'
  'indicator-keys.json' = 'indicator-keys.json'
}
foreach ($entry in $settings.GetEnumerator()) {
  $dest = Join-Path $PackDir $entry.Key
  if (Test-Path $dest) { continue }
  if ($CleanDefaults -and -not $MigrateFrom) {
    if ($entry.Key -eq 'rice-widgets.json') {
      $neutralWidgets=Get-Content (Join-Path $repo $entry.Value) -Raw | ConvertFrom-Json
      $neutralWidgets | Add-Member -NotePropertyName quickActions -NotePropertyValue $false -Force
      Write-Utf8 $dest (ConvertTo-Json $neutralWidgets -Depth 8); continue
    }
    if ($entry.Key -eq 'workspaces.json') {
      $neutral = @{workspaces=@(1..9 | ForEach-Object { @{name="$_";label="$_";apps=@()} }) + @(@{name='0';label='0';apps=@()})}
      Write-Utf8 $dest (ConvertTo-Json $neutral -Depth 6); continue
    }
    if ($entry.Key -eq 'launch-apps.json') { Write-Utf8 $dest '{"apps":[]}'; continue }
    if ($entry.Key -eq 'wallpapers.json') { Write-Utf8 $dest '{"folder":"%USERPROFILE%/Pictures/Wallpapers"}'; continue }
  }
  $old = if ($MigrateFrom) { Join-Path $MigrateFrom $entry.Key } else { $null }
  if ($old -and (Test-Path $old)) { Copy-Item $old $dest; Note "Migrated $($entry.Key)" } else { Copy-Item (Join-Path $repo $entry.Value) $dest }
}
Copy-Item (Join-Path $PackDir 'rice-widgets.json') (Join-Path $live 'rice-widgets.json') -Force

# Spotlight's app catalog comes from the Windows Start menu (no arbitrary commands).
$shellCatalog = New-Object -ComObject Shell.Application
$appFolder = $shellCatalog.Namespace('shell:AppsFolder')
$apps = @(Get-StartApps | Sort-Object Name, AppID -Unique | ForEach-Object {
  $target = ''; try { $item = $appFolder.ParseName($_.AppID); if ($item) { $target = [string]$item.ExtendedProperty('System.Link.TargetParsingPath') } } catch {}
  [pscustomobject]@{ Name=$_.Name; AppID=$_.AppID; Executable=$target }
})
Write-Utf8 (Join-Path $PackDir 'spotlight-apps.json') (ConvertTo-Json -InputObject $apps -Depth 3)
Note "Spotlight catalog: $($apps.Count) apps"

# zpack.json with shell privileges for this install path only (case-insensitive path match).
# Escape only regex metacharacters: .NET's Regex.Escape also escapes spaces, which Zebar's Rust
# regex engine rejects (and Zebar silently ignores a privilege whose pattern fails to compile).
$escape = { param($text) -join ([string]$text).ToCharArray().ForEach({ if ('\^$.*+?()[]{}|'.Contains([string]$_)) { '\' + $_ } else { [string]$_ } }) }
$scriptArgs = '^(?i)' + (& $escape '-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File ')
$pack = Get-Content (Join-Path $repo 'zpack.json') -Raw | ConvertFrom-Json
$widget = $pack.widgets | Where-Object name -eq 'glazewm'
$widget.privileges.shellCommands = @(
  [pscustomobject]@{ program = 'ipconfig.exe'; argsRegex = '^$' },
  [pscustomobject]@{ program = 'powershell.exe'; argsRegex = $scriptArgs + (& $escape (Join-Path $PackDir 'open-player.ps1')) + ' -Player (spotify|helium)$' }
)
Write-Utf8 (Join-Path $PackDir 'zpack.json') (ConvertTo-Json $pack -Depth 20)
if (Test-Path (Join-Path $repo 'resources')) { New-Item -ItemType Directory -Force (Join-Path $PackDir 'resources') | Out-Null; Copy-Item (Join-Path $repo 'resources\*') (Join-Path $PackDir 'resources') -Recurse -Force }

# ---------------------------------------------------------------------------------------------
if ($SkipGlazeConfig) { Step 'Skipping GlazeWM config (-SkipGlazeConfig)' } else {
  Step 'GlazeWM config'
  $original = [IO.File]::ReadAllText($glazeConfig)
  $config = $original
  $q = { param($path) $p = $path.Replace("'", "''"); if ($p -match '\s') { '"' + $p + '"' } else { $p } }
  $runtimeCmd = 'shell-exec ' + (& $q (Join-Path $PackDir 'NordRuntime.exe'))
  $spotlightCmd = 'shell-exec ' + (& $q (Join-Path $PackDir 'NordSpotlight.exe'))
  $indicatorCmd = 'shell-exec ' + (& $q (Join-Path $PackDir 'NordWorkspaceIndicator.exe'))
  # Re-point entries from an older install at this one.
  $config = [regex]::Replace($config, "shell-exec (`"[^`"]*|[^'`"\s,\]]*)NordSpotlight\.exe`"?", { param($m) $spotlightCmd })
  $config = [regex]::Replace($config, "shell-exec (`"[^`"]*|[^'`"\s,\]]*)NordWorkspaceIndicator\.exe`"?", { param($m) $indicatorCmd })
  $config = [regex]::Replace($config, "shell-exec (`"[^`"]*|[^'`"\s,\]]*)NordRuntime\.exe`"?", { param($m) $runtimeCmd })
  $added = @()
  # Start the helpers with GlazeWM, and stop them when it exits.
  $startup = [regex]::Match($config, '(?m)^(\s*startup_commands:\s*\[)(.*)\]\s*$')
  if ($startup.Success) {
    $items = $startup.Groups[2].Value
    $extra = @(); if ($items -notmatch 'NordRuntime\.exe') { $extra += "'$runtimeCmd'" }; if ($items -notmatch 'NordSpotlight\.exe') { $extra += "'$spotlightCmd --background'" }; if ($items -notmatch 'NordWorkspaceIndicator\.exe') { $extra += "'$indicatorCmd'" }
    if ($items -notmatch 'shell-exec zebar') { $extra += "'shell-exec zebar'" }
    if ($extra.Count) { $config = $config.Remove($startup.Groups[2].Index, 0).Insert($startup.Groups[2].Index, (($extra -join ', ') + $(if ($items.Trim()) { ', ' } else { '' }))); $added += 'startup commands' }
  } else { Warn 'startup_commands not found as a [..] list; add the helper start commands from the README by hand.' }
  $shutdown = [regex]::Match($config, '(?m)^(\s*shutdown_commands:\s*\[)(.*)\]\s*$')
  if ($shutdown.Success) {
    $items = $shutdown.Groups[2].Value; $extra = @()
    foreach ($name in 'NordSpotlight', 'NordWorkspaceIndicator', 'NordControlCenter') { if ($items -notmatch "taskkill /IM $name\.exe") { $extra += "'shell-exec taskkill /IM $name.exe /F'" } }
    if ($extra.Count) { $config = $config.Insert($shutdown.Groups[2].Index, (($extra -join ', ') + $(if ($items.Trim()) { ', ' } else { '' }))); $added += 'shutdown commands' }
  } else { Warn 'shutdown_commands not found as a [..] list; add the helper stop commands from the README by hand.' }
  # Keep game exclusions independent of the optional workspace-assignment toggle.
  if (-not $config.Contains('# BEGIN NORD GAME IGNORE RULES')) {
    $rules = [regex]::Match($config, '(?m)^window_rules:\s*\r?\n')
    if ($rules.Success) {
      $nl = if ($config.Contains("`r`n")) { "`r`n" } else { "`n" }
      $gameRules = (Get-Content (Join-Path $repo 'resources\game-window-rules.yaml') -Raw).TrimEnd().Replace("`r`n", "`n").Replace("`n", $nl) + $nl
      $config = $config.Insert($rules.Index + $rules.Length, $gameRules); $added += 'game ignore rules'
    }
  }
  # Upgrade our original game block without replacing user-added game exclusions.
  $gameBlock=[regex]::Match($config,'(?s)# BEGIN NORD GAME IGNORE RULES.*?# END NORD GAME IGNORE RULES')
  if ($gameBlock.Success -and $gameBlock.Value -notmatch 'Overwatch') {
    $updatedBlock=$gameBlock.Value.Replace('^(VALORANT|VALORANT-Win64-Shipping)$','^(Overwatch|VALORANT|VALORANT-Win64-Shipping)$')
    $config=$config.Remove($gameBlock.Index,$gameBlock.Length).Insert($gameBlock.Index,$updatedBlock)
  }
  # Upgrade existing installs too; preserve all other user/game rules.
  if ($config -notmatch '# Minecraft Java \(including Prism instances\) and Bedrock\.') {
    $minecraftSource = Get-Content (Join-Path $repo 'resources\game-window-rules.yaml') -Raw
    $minecraft = [regex]::Match($minecraftSource, '(?s)  # Minecraft Java.*?(?=  # END NORD GAME IGNORE RULES)').Value
    $nl = if ($config.Contains("`r`n")) { "`r`n" } else { "`n" }
    $minecraft = $minecraft.TrimEnd().Replace("`r`n", "`n").Replace("`n", $nl) + $nl
    $config = $config.Replace('  # END NORD GAME IGNORE RULES', $minecraft + '  # END NORD GAME IGNORE RULES')
  }
  # Capture overlays position themselves and must never be tiled or styled by GlazeWM.
  if ($config -notmatch "window_process:\s*\{\s*equals:\s*'Lightshot'\s*\}") {
    $rules = [regex]::Match($config, '(?m)^window_rules:\s*\r?\n')
    if ($rules.Success) {
      $nl = if ($config.Contains("`r`n")) { "`r`n" } else { "`n" }
      $config = $config.Insert($rules.Index + $rules.Length, "  # Lightshot capture overlay (install.ps1)$nl  - commands: ['ignore']$nl    match:$nl      - window_process: { equals: 'Lightshot' }$nl")
      $added += 'Lightshot ignore rule'
    }
  }
  # GlazeWM must not tile the bar's helper windows.
  $ignored = { param($name) $config -match "equals: '$name'" }
  if (-not $config.Contains('# nord-rice helper windows') -and -not ((& $ignored 'NordControlCenter') -and (& $ignored 'NordSpotlight') -and (& $ignored 'NordWorkspaceIndicator'))) {
    $rules = [regex]::Match($config, '(?m)^window_rules:\s*\r?\n')
    if ($rules.Success) {
      $nl = if ($config.Contains("`r`n")) { "`r`n" } else { "`n" }
      $block = "  # nord-rice helper windows (install.ps1)$nl  - commands: ['ignore']$nl    match:$nl      - window_process: { regex: '^(NordSpotlight|NordControlCenter|NordWorkspaceIndicator)$' }$nl"
      $config = $config.Insert($rules.Index + $rules.Length, $block); $added += 'ignore rule'
    } else { Warn 'window_rules: not found; add the ignore rule from the README by hand.' }
  }
  # Ctrl+Space opens floating Spotlight, unless that shortcut is already taken.
  if ($config -notmatch 'NordSpotlight\.exe"?''\]\s*\r?\n\s*bindings') {
    if ($config -match "(?i)'(ctrl|control)\+space'") { Warn 'Ctrl+Space is already bound; not adding the Spotlight shortcut.' }
    else {
      $keys = [regex]::Match($config, '(?m)^keybindings:\s*\r?\n')
      if ($keys.Success) {
        $nl = if ($config.Contains("`r`n")) { "`r`n" } else { "`n" }
        $config = $config.Insert($keys.Index + $keys.Length, "  # nord-rice floating Spotlight (install.ps1)$nl  - commands: ['$spotlightCmd']$nl    bindings: ['ctrl+space']$nl"); $added += 'Ctrl+Space shortcut'
      }
    }
  }
  # Alt+[ / Alt+] cycle through every configured workspace, including empty ones.
  # Keep this as an owned, idempotent block so reinstalling never duplicates the binds.
  $keys = [regex]::Match($config, '(?m)^keybindings:[ \t]*\r?\n')
  if ($keys.Success) {
    $nl = if ($config.Contains("`r`n")) { "`r`n" } else { "`n" }
    $beforeCycling = $config
    $config = [regex]::Replace($config, '(?ms)^  # BEGIN NORD WORKSPACE CYCLING\r?\n.*?^  # END NORD WORKSPACE CYCLING(?:\r?\n)?', '')
    $keys = [regex]::Match($config, '(?m)^keybindings:[ \t]*\r?\n')
    $cycling = (Get-Content (Join-Path $repo 'resources\workspace-cycle-keybindings.yaml') -Raw).TrimEnd().Replace("`r`n", "`n").Replace("`n", $nl) + $nl
    $config = $config.Insert($keys.Index + $keys.Length, $cycling)
    if ($config -ne $beforeCycling) { $added += 'Alt+[ / Alt+] workspace cycling' }
  }
  if ($config -ne $original) {
    $saved = Join-Path $BackupDir "glazewm-configs\config-before-nord-rice-$stamp.yaml"
    New-Item -ItemType Directory -Force (Split-Path $saved) | Out-Null
    Copy-Item $glazeConfig $saved
    Write-Utf8 $glazeConfig $config
    Note "Updated: $(if ($added.Count) { $added -join ', ' } else { 'helper paths' }) (backup: $saved)"
    if (Get-Process glazewm -ErrorAction SilentlyContinue) {
      $result = & $glazeCli command wm-reload-config | ConvertFrom-Json
      if (-not $result.success) { Write-Utf8 $glazeConfig $original; & $glazeCli command wm-reload-config | Out-Null; throw "GlazeWM rejected the config, restored the original: $($result.error)" }
    }
  } else { Note 'Already configured' }
}

# ---------------------------------------------------------------------------------------------
Step 'Session recovery'
Write-Utf8 (Join-Path $PackDir 'runtime-zebar.txt') $zebarExe
# GlazeWM owns startup so every helper runs in the same session and privilege context.
# Remove only our previous early-logon entry; it could start Zebar without the manager.
try {
  $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
  $taskName='Nord Rice Logon - '+$identity.User.Value
  $task=Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
  if ($task -and @($task.Actions | Where-Object { [IO.Path]::GetFileName($_.Execute) -eq 'NordRuntime.exe' }).Count) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction Stop
  }
  $shortcutPath=Join-Path ([Environment]::GetFolderPath('Startup')) 'Nord Rice Runtime.lnk'
  if (Test-Path -LiteralPath $shortcutPath) {
    $shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcutPath)
    if ([IO.Path]::GetFileName($shortcut.TargetPath) -eq 'NordRuntime.exe') { Remove-Item -LiteralPath $shortcutPath -ErrorAction Stop }
  }
  Note 'Recovery starts with GlazeWM; removed the obsolete early-logon launcher'
} catch { Warn "Could not remove the obsolete Nord runtime startup entry: $($_.Exception.Message)" }

if ($Notifications) { try { & (Join-Path $repo 'notifications\install.ps1') -PackDir $PackDir } catch { Warn "Windows notification companion was not installed: $($_.Exception.Message). The bar and local inbox will still start." } }

Step 'Zebar startup' 
$zebarSettings = Join-Path $env:USERPROFILE '.glzr\zebar\settings.json'
$zs = if (Test-Path $zebarSettings) { Get-Content $zebarSettings -Raw | ConvertFrom-Json } else { [pscustomobject]@{ startupConfigs = @() } }
if (-not $zs.PSObject.Properties['startupConfigs']) { $zs | Add-Member startupConfigs @() }
$others = @($zs.startupConfigs | Where-Object { $_.pack -ne $pack.name -and -not ($_.pack -in 'nord', 'quangbhdang.nord' -and $_.widget -eq 'glazewm') })
$replaced = @($zs.startupConfigs).Count - $others.Count
$zs.startupConfigs = @($others) + @([pscustomobject]@{ pack = $pack.name; widget = 'glazewm'; preset = 'default' })
if (Test-Path $zebarSettings) { New-Item -ItemType Directory -Force (Join-Path $BackupDir 'zebar-configs') | Out-Null; Copy-Item $zebarSettings (Join-Path $BackupDir "zebar-configs\settings-before-nord-rice-$stamp.json") }
Write-Utf8 $zebarSettings (ConvertTo-Json $zs -Depth 10)
Note "Zebar starts $($pack.name)/glazewm$(if ($replaced) { " (replaced $replaced older Nord bar entry)" })"
if (@($others).Count) { Note "Other startup widgets kept: $(($others | ForEach-Object { "$($_.pack)/$($_.widget)" }) -join ', ')" }

# ---------------------------------------------------------------------------------------------
$extension = Join-Path $env:APPDATA 'spicetify\Extensions\nord-media-link.js'
$linkScript = (Get-Content (Join-Path $repo 'control-center\nord-media-link.js') -Raw).Replace('__NORD_CENTER_TOKEN__', $tokens.center)
if ($Spicetify) {
  Step 'Spicetify extension'
  $spicetifyCommand = Get-Command spicetify -ErrorAction SilentlyContinue
  $spicetifyExe = if ($spicetifyCommand) { $spicetifyCommand.Source } else { Join-Path $env:LOCALAPPDATA 'spicetify\spicetify.exe' }
  if (-not (Test-Path -LiteralPath $spicetifyExe)) { Warn 'spicetify not found; skipped (https://spicetify.app).' } else {
    New-Item -ItemType Directory -Force (Split-Path $extension) | Out-Null
    Write-Utf8 $extension $linkScript
    & $spicetifyExe config extensions nord-media-link.js | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not register the Spotify metadata extension.' }
    # Spotify updates replace the patched app. Refresh the backup before applying
    # instead of reapplying files from an older Spotify version.
    & $spicetifyExe backup apply | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Spicetify repair failed. Check its output above; the Spotify link connection has not been repaired.' }
    Note 'Installed nord-media-link.js; refreshed Spotify backup and applied. Reopen Spotify to load the extension.'
  }
} elseif (Test-Path $extension) {
  # Keep an existing install in step with the current token (takes effect on the next spicetify apply).
  if ((Get-Content $extension -Raw).Replace("`r`n", "`n") -ne $linkScript.Replace("`r`n", "`n")) { Write-Utf8 $extension $linkScript; Warn 'Updated the Spicetify extension; run "spicetify apply" to load it.' }
}

# ---------------------------------------------------------------------------------------------
if ($NoRestart) { Step 'Done (not restarted: -NoRestart)' } else {
  Step 'Starting'
  Get-Process zebar -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Milliseconds 800
  # Launch through GlazeWM when it runs: the helpers then belong to GlazeWM, not to this shell. A shell
  # inside a kill-on-close job (some terminals and tools) would otherwise take the whole rice down with it.
  $glaze = Get-Command glazewm -ErrorAction SilentlyContinue
  function Launch([string]$exe, [string]$arguments) {
    if ($glaze -and (Get-Process glazewm -ErrorAction SilentlyContinue) -and $exe -notmatch '\s') {
      # The CLI takes the command as separate words; its errors must not skip the fallback below.
      $words = @('command', 'shell-exec', $exe) + @($arguments -split ' ' | Where-Object { $_ })
      $previous = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
      try { $out = & $glaze.Source @words 2>&1 } finally { $ErrorActionPreference = $previous }
      if ($LASTEXITCODE -eq 0 -and "$out" -match '"success":true') { return }
    }
    if ($arguments) { Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -ErrorAction Stop } else { Start-Process $exe -WindowStyle Hidden -ErrorAction Stop }
  }
  # Zebar lives under Program Files (a path with spaces); GlazeWM finds it on PATH by name.
  $zebarLaunch = if ($glaze -and (Get-Command zebar -ErrorAction SilentlyContinue)) { 'zebar' } else { $zebarExe }
  # A blocked helper must not stop the rest of the rice from starting.
  $launches = @(
    { Launch (Join-Path $PackDir 'NordSpotlight.exe') '--background' },
    { Launch (Join-Path $PackDir 'NordWorkspaceIndicator.exe') '' },
    { Launch $zebarLaunch '' },
    { Launch (Join-Path $PackDir 'NordRuntime.exe') '' })
  foreach ($launch in $launches) {
    try { & $launch }
    catch {
      Warn "A helper did not start: $($_.Exception.Message)"
      if ($_.Exception.Message -match 'Application Control') { Warn 'Smart App Control blocked this build. Run install.ps1 -Rebuild to compile a new copy.' }
    }
  }
  Note 'Zebar restarted; the control center starts with the bar.'
}
Write-Host "`nnord-rice is installed in $PackDir" -ForegroundColor Green

} finally { if ($installOwned) { $installMutex.ReleaseMutex() }; $installMutex.Dispose() }
