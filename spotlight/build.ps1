param([switch]$Checks)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework ('WPF\' + $_)) }
if ($Checks) {
  New-Item -ItemType Directory -Force "$PSScriptRoot\checks" | Out-Null
  $target = @('/target:exe', '/main:NordSpotlight', "/out:$PSScriptRoot\checks\SpotlightChecks.exe")
} else { $target = @('/target:winexe', "/out:$PSScriptRoot\NordSpotlight.exe") }
& (Join-Path $framework 'csc.exe') /nologo @target /platform:x64 /optimize+ /reference:System.Xaml.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll @refs (Join-Path $PSScriptRoot '../shared/GameWindows.cs') (Join-Path $PSScriptRoot 'NordSpotlight.cs') (Join-Path $PSScriptRoot 'SpotlightTheme.cs') (Join-Path $PSScriptRoot '../shared/ThemeTransition.cs') (Join-Path $PSScriptRoot '../shared/WallpaperPalette.cs') (Join-Path $PSScriptRoot 'SearchBridge.cs') (Join-Path $PSScriptRoot 'Calculator.cs') (Join-Path $PSScriptRoot 'SearchProviders.cs') (Join-Path $PSScriptRoot 'WindowSearch.cs') (Join-Path $PSScriptRoot 'WorkspaceBatch.cs') (Join-Path $PSScriptRoot 'WorkspaceBatchChecks.cs') (Join-Path $PSScriptRoot '../shared/BatchAppSettings.cs') (Join-Path $PSScriptRoot 'SpotlightChecks.cs') (Join-Path $PSScriptRoot 'AppShortcutsChecks.cs') (Join-Path $PSScriptRoot 'AppUsageTracker.cs') (Join-Path $PSScriptRoot '../shared/AppCatalog.cs') (Join-Path $PSScriptRoot '../shared/AppShortcuts.cs') (Join-Path $PSScriptRoot '../shared/AppIcons.cs') (Join-Path $PSScriptRoot '../shared/BridgeTokens.cs') (Join-Path $PSScriptRoot '../shared/FocusTheme.cs') (Join-Path $PSScriptRoot '../shared/FrostFile.cs') (Join-Path $PSScriptRoot '../shared/RiceLook.cs') (Join-Path $PSScriptRoot '../shared/ScrollChrome.cs')
if ($LASTEXITCODE -ne 0) { throw 'Spotlight build failed.' }
