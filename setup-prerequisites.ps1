# Dot-sourced by install.ps1. Reuse installed applications; install only missing requirements.
function Refresh-SetupPath {
  $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User') + ';' + $env:Path
}
function Find-RiceExecutable([string]$name, [string[]]$candidates) {
  $found = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($found) { return $found.Source }
  return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
function Ensure-RicePrerequisites {
  if (-not [Environment]::Is64BitOperatingSystem -or $env:OS -ne 'Windows_NT') { throw 'nord-rice requires 64-bit Windows 10/11.' }
  if (-not (Test-Path "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe")) { throw '.NET Framework compiler is missing. Enable .NET Framework 4.8 in Windows Features, then rerun this command.' }
  Refresh-SetupPath
  $requirements = @(
    @{Name='npm.cmd'; Id='OpenJS.NodeJS.LTS'; Paths=@("$env:ProgramFiles\nodejs\npm.cmd")},
    @{Name='glazewm.exe'; Id='glzr-io.GlazeWM'; Paths=@("$env:ProgramFiles\glzr.io\GlazeWM\glazewm.exe", "$env:LOCALAPPDATA\Programs\glzr.io\GlazeWM\glazewm.exe")},
    @{Name='zebar.exe'; Id='glzr-io.Zebar'; Paths=@("$env:ProgramFiles\glzr.io\Zebar\zebar.exe", "$env:LOCALAPPDATA\Programs\glzr.io\Zebar\zebar.exe")}
  )
  foreach ($requirement in $requirements) {
    if (Find-RiceExecutable $requirement.Name $requirement.Paths) { Note "$($requirement.Id): already installed"; continue }
    if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) {
      Step 'Installing Windows Package Manager'
      [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
      Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Scope CurrentUser -Force | Out-Null
      Install-Module Microsoft.WinGet.Client -Repository PSGallery -Scope CurrentUser -Force -AllowClobber
      Import-Module Microsoft.WinGet.Client
      Repair-WinGetPackageManager -Force -Latest
      Refresh-SetupPath
      if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) { throw 'Windows blocked installation of WinGet. Install App Installer from https://aka.ms/getwinget, then rerun setup.' }
    }
    Step "Installing $($requirement.Id)"
    & winget.exe install --id $requirement.Id --exact --source winget --accept-source-agreements --accept-package-agreements --silent --disable-interactivity
    if ($LASTEXITCODE -ne 0) { throw "Installation of $($requirement.Id) failed (exit $LASTEXITCODE). Rerun after resolving the Windows installer message." }
    Refresh-SetupPath
    if (-not (Find-RiceExecutable $requirement.Name $requirement.Paths)) { throw "$($requirement.Id) installed but its executable was not found. Open a new PowerShell and rerun setup." }
  }
  # The compiler's npm lookup must also work in this same shell after a fresh Node install.
  $npm = Find-RiceExecutable 'npm.cmd' @("$env:ProgramFiles\nodejs\npm.cmd")
  $env:Path = (Split-Path $npm) + ';' + $env:Path
  $config = Join-Path $env:USERPROFILE '.glzr\glazewm\config.yaml'
  if (-not $SkipGlazeConfig -and -not (Test-Path -LiteralPath $config)) {
    Step 'Creating the initial GlazeWM configuration'
    $exe = Find-RiceExecutable 'glazewm.exe' $requirements[1].Paths
    Start-Process -FilePath $exe -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath $config) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if (-not (Test-Path -LiteralPath $config)) { throw 'GlazeWM did not create its configuration within 20 seconds. Check whether Windows blocked GlazeWM, then rerun setup.' }
  }
}
