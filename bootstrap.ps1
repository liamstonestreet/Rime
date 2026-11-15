[CmdletBinding()]
param(
  [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository='liamstonestreet/Rime',
  [ValidatePattern('^[A-Za-z0-9_.-]+$')][string]$Ref='main',
  [switch]$Notifications,
  [switch]$Spicetify
)
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$root=Join-Path $env:LOCALAPPDATA 'NordRice\source'
New-Item -ItemType Directory -Force $root | Out-Null
# Unique retained source folder avoids overwriting a concurrent or previous installation.
$stage=Join-Path $root ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $stage | Out-Null
$archive=Join-Path $stage 'source.zip'
Write-Host 'Downloading nord-rice source...' -ForegroundColor Cyan
Invoke-WebRequest -UseBasicParsing -Uri "https://api.github.com/repos/$Repository/zipball/$Ref" -OutFile $archive
Expand-Archive -LiteralPath $archive -DestinationPath $stage
$source=Get-ChildItem -LiteralPath $stage -Directory | Select-Object -First 1
$installer=Join-Path $source.FullName 'nord-custom\install.ps1'
if (-not (Test-Path -LiteralPath $installer)) { $installer=Join-Path $source.FullName 'install.ps1' }
if (-not (Test-Path -LiteralPath $installer)) { throw 'The GitHub archive does not contain the nord-rice installer.' }
$arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$installer,'-CleanDefaults')
if($Notifications){$arguments+='-Notifications'}
if($Spicetify){$arguments+='-Spicetify'}
& powershell.exe @arguments
if($LASTEXITCODE -ne 0){throw "Rime installation failed (exit $LASTEXITCODE). Source retained at $($source.FullName) for retry."}
Write-Host "Source retained for updates: $($source.FullName)" -ForegroundColor Green
