param([Parameter(Mandatory=$true)][ValidateSet('spotify','helium')][string]$Player)
$ErrorActionPreference = 'Stop'
$appPath = if ($Player -eq 'spotify') {
  Join-Path $env:APPDATA 'Spotify\Spotify.exe'
} else {
  Join-Path $env:LOCALAPPDATA 'imput\Helium\Application\chrome.exe'
}
$processName = if ($Player -eq 'spotify') { 'Spotify' } else { 'chrome' }
$handles = @(Get-Process -Name $processName -ErrorAction SilentlyContinue |
  Where-Object { $_.Path -eq $appPath -and $_.MainWindowHandle -ne 0 } |
  ForEach-Object { $_.MainWindowHandle.ToInt64() })
$wm = Join-Path $env:ProgramFiles 'glzr.io\GlazeWM\cli\glazewm.exe'
if (-not (Test-Path $wm)) { $wm = (Get-Command glazewm -ErrorAction Stop).Source }
function Test-ContainsWindow($Container, [string]$WindowId) {
  if ($Container.id -eq $WindowId) { return $true }
  foreach ($child in $Container.children) {
    if (Test-ContainsWindow $child $WindowId) { return $true }
  }
  return $false
}
if ($handles.Count -gt 0) {
  $query = (& $wm query windows | ConvertFrom-Json)
  if (-not $query.success) { throw $query.error }
  $target = $query.data.windows | Where-Object { $handles -contains $_.handle } |
    Sort-Object hasFocus -Descending | Select-Object -First 1
  if ($target) {
    # A window focus alone can leave workspace visibility out of sync.
    # Resolve through split containers and switch the workspace first.
    $workspaceQuery = (& $wm query workspaces | ConvertFrom-Json)
    if (-not $workspaceQuery.success) { throw $workspaceQuery.error }
    $workspace = $workspaceQuery.data.workspaces |
      Where-Object { Test-ContainsWindow $_ $target.id } | Select-Object -First 1
    if (-not $workspace) { throw 'Could not locate the app workspace.' }
    if (-not $workspace.hasFocus -or -not $workspace.isDisplayed) {
      $switched = (& $wm command focus --workspace $workspace.name | ConvertFrom-Json)
      if (-not $switched.success) { throw $switched.error }
    }
    if ($target.state.type -eq 'minimized') {
      $restored = (& $wm command --id $target.id toggle-minimized | ConvertFrom-Json)
      if (-not $restored.success) { throw $restored.error }
    }
    $focused = (& $wm command focus --container-id $target.id | ConvertFrom-Json)
    if (-not $focused.success) { throw $focused.error }
    # Reconcile native cloaking/visibility as well as GlazeWM's focus state.
    $redrawn = (& $wm command wm-redraw | ConvertFrom-Json)
    if (-not $redrawn.success) { throw $redrawn.error }
    exit 0
  }
}
Start-Process -FilePath $appPath
