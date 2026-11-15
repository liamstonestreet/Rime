$ErrorActionPreference = 'Stop'
# Builds checks\CenterChecks.exe (same sources and references as the helper); run it to test.
& "$PSScriptRoot\build.ps1" -Checks
