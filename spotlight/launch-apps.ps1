$ErrorActionPreference = 'Stop'
$wm = Join-Path $env:ProgramFiles 'glzr.io\GlazeWM\cli\glazewm.exe'
if (-not (Test-Path $wm)) { $wm = (Get-Command glazewm -ErrorAction Stop).Source }
$configPath = Join-Path $env:USERPROFILE '.glzr\glazewm\config.yaml'
$mutex = New-Object Threading.Mutex($false, 'Local\LiamNordWorkspaceRules')
if (-not $mutex.WaitOne(0)) { exit }
$original = $null
try {
  $settings = Get-Content (Join-Path $PSScriptRoot 'launch-apps.json') -Raw | ConvertFrom-Json
  $catalog = @(Get-StartApps)
  $running = @(Get-Process)
  $defaults = @{ Helium='chrome'; Spotify='Spotify'; Signal='Signal'; Discord='Discord'; ChatGPT='ChatGPT' }
  $targets = foreach ($name in $settings.apps) {
    $processName = $defaults[$name]
    if ($settings.processNames -and $settings.processNames.$name) { $processName = $settings.processNames.$name }
    if ($processName -and ($running | Where-Object { $_.ProcessName -eq $processName -and ($name -ne 'Helium' -or $_.Path -like '*\imput\Helium\*') })) { continue }
    $entry = $catalog | Where-Object Name -eq $name | Select-Object -First 1
    if (-not $entry) { throw "App not found in Start catalog: $name" }
    $entry
  }
  if (@($targets).Count -eq 0) { return }
  $workspace = ((& $wm query workspaces | ConvertFrom-Json).data.workspaces | Where-Object hasFocus | Select-Object -First 1).name
  if ($null -eq $workspace) { throw 'Focused workspace unavailable.' }
  $original = [IO.File]::ReadAllText($configPath)
  $temporary = [regex]::Replace($original, '(?ms)(^  # BEGIN LIAM APP ASSIGNMENTS\r?\n)(.*?)(^  # END LIAM APP ASSIGNMENTS)', [Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + ([regex]::Replace($m.Groups[2].Value, ", 'focus --workspace [0-9]+'", '')) + $m.Groups[3].Value })
  if ($temporary -ne $original) {
    [IO.File]::WriteAllText($configPath, $temporary)
    $reply = & $wm command wm-reload-config | ConvertFrom-Json
    if (-not $reply.success) { throw $reply.error }
  }
  foreach ($app in $targets) {
    Start-Process explorer.exe -ArgumentList ('"shell:AppsFolder\' + $app.AppID + '"')
  }
  # Apps can activate themselves asynchronously during startup. Keep the
  # original workspace selected until the configured startup grace period ends.
  $until = [DateTime]::UtcNow.AddSeconds([Math]::Min(60,[Math]::Max(1,$settings.settleSeconds)))
  while ([DateTime]::UtcNow -lt $until) {
    & $wm command focus --workspace $workspace | Out-Null
    Start-Sleep -Milliseconds 250
  }
} catch {
  $_ | Out-String | Set-Content (Join-Path $PSScriptRoot 'launch-error.log')
} finally {
  if ($null -ne $original) {
    [IO.File]::WriteAllText($configPath, $original)
    & $wm command wm-reload-config | Out-Null
  }
  $mutex.ReleaseMutex()
  $mutex.Dispose()
}
