param(
  # Build the isolated WPF checks (checks\CenterChecks.exe) instead of the helper.
  [switch]$Checks,
  # Ignore any Windows SDK and use the WinRT metadata built into Windows (tests that path).
  [switch]$SystemWinMetadata
)
$ErrorActionPreference = 'Stop'
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$naudio = "$PSScriptRoot\naudio\lib\net35\NAudio.dll"
$facadeDir = "$PSScriptRoot\refs\build\.NETFramework\v4.8\Facades"
if (!(Test-Path $naudio) -or !(Test-Path $facadeDir)) { throw 'Build dependencies missing (naudio\, refs\). Run install.ps1 once to download them.' }
Copy-Item $naudio $PSScriptRoot -Force

$refs = @('WindowsBase','PresentationCore','PresentationFramework','UIAutomationClient','UIAutomationTypes') | ForEach-Object { '/reference:' + (Join-Path $fx "WPF\$_.dll") }
$facades = Get-ChildItem "$facadeDir\*.dll" | ForEach-Object { '/reference:' + $_.FullName }
# WinRT metadata for the media-session APIs: a Windows SDK's Windows.winmd when installed, otherwise
# the per-namespace .winmd files that every Windows 10/11 install ships in System32\WinMetadata.
$sdk = if ($SystemWinMetadata) { $null } else {
  Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\UnionMetadata\*\Windows.winmd" -ErrorAction SilentlyContinue |
    Where-Object { $_.Directory.Name -match '^\d+(\.\d+)+$' } | Sort-Object { [version]$_.Directory.Name } -Descending | Select-Object -First 1 }
$winmd = @(if ($sdk) { '/reference:' + $sdk.FullName } else { Get-ChildItem "$env:WINDIR\System32\WinMetadata\*.winmd" | ForEach-Object { '/reference:' + $_.FullName } })

$sources = @('NotificationShortcut','NotificationPopup','GamePresentation','AppWindowTint','NotificationInbox','NotificationCenter','NotificationsView','NordControlCenter','ControlCenterApp','AppServices','PanelPlacementController','PanelFrameGeometry','PanelAnimationController','ControlCenterTab','QuickActions','DiscordVoice','CalendarView','CalendarData','GoogleCalendar','CalendarConnections','CalendarFeedDialog','IcalData','CalendarChecks','MediaCard','MediaBridge','BarBridgeServer','BridgeConnection','BridgeHub','BridgeCommandRouter','BridgeApplicationCommands','MediaMetadataStore','PowerMenu','PowerMenuController','PowerActions','TaskActions','FocusMode','FocusSession','FocusView','IndicatorKeyControl','ControlCenterTheme','BarAudio','AudioController','AudioPeakSampler','AudioPage','WindowsAudioEndpointService','BarState','MicControl','VolumeMixer','WorkspacePins','TaskPins','RiceLayout','VpnStatus','TrayPopup','WorkspaceConfig','WorkspaceLaunchPolicy','WorkspaceLaunchRouter','NordMenu','BarAppearance','AppearanceSettings','AppearanceSettingsStore','ThemeCoordinator','WindowsThemeHost','WallpaperLookPreparer','AppearancePublisher','WallpaperTheme','CenterBackdrop','Grain','WallpaperSwitcher','WallpaperCoordinator','WindowsDesktopWallpaperRenderer','WallpaperFadeAnimator','WallpaperPicker','WallpaperVariants','HoldKey','FrostMaterial','GlazeBorders','GlazeTab','FocusBorder','ScreenFrame','ScaledDecode','EdgeGlow') | ForEach-Object { "$PSScriptRoot\$_.cs" }
$sources += "$PSScriptRoot\..\shared\GameWindows.cs"
$sources += "$PSScriptRoot\..\shared\BridgeTokens.cs"
 $sources += "$PSScriptRoot\DisplayCadence.cs"
 $sources += "$PSScriptRoot\StereoBalance.cs"
 $sources += "$PSScriptRoot\LockScreenWallpaper.cs"
 $sources += "$PSScriptRoot\LockScreenService.cs"
 $sources += "$PSScriptRoot\LockScreenImageBuilder.cs"
 $sources += "$PSScriptRoot\WallpaperLibrary.cs"
 $sources += "$PSScriptRoot\WallpaperBrowserStateStore.cs"
$sources += "$PSScriptRoot\..\shared\IndicatorKeys.cs"
$sources += "$PSScriptRoot\IcalChecks.cs"
$sources += "$PSScriptRoot\FocusChecks.cs"
$sources += "$PSScriptRoot\WallpaperChecks.cs"
$sources += "$PSScriptRoot\..\shared\NotificationItem.cs"
$sources += "$PSScriptRoot\..\shared\NotificationActivation.cs"
$sources += "$PSScriptRoot\..\shared\FocusTheme.cs"
$sources += "$PSScriptRoot\..\shared\WallpaperPalette.cs"
$sources += "$PSScriptRoot\..\shared\ThemeTransition.cs"
$sources += "$PSScriptRoot\..\shared\FrostFile.cs"
$sources += "$PSScriptRoot\..\shared\RiceLook.cs"
$sources += "$PSScriptRoot\..\shared\ScrollChrome.cs"
$sources += "$PSScriptRoot\..\shared\AppCatalog.cs"
$sources += "$PSScriptRoot\..\shared\AppShortcuts.cs"
$sources += "$PSScriptRoot\..\shared\BatchAppSettings.cs"
$sources += "$PSScriptRoot\BatchAppsView.cs"
$sources += "$PSScriptRoot\..\shared\AppIcons.cs"
if ($Checks) {
  $sources += "$PSScriptRoot\AppWindowTintChecks.cs"
  $sources += "$PSScriptRoot\NotificationChecks.cs"
  $sources += "$PSScriptRoot\AppServicesChecks.cs"
  $sources += "$PSScriptRoot\StereoBalanceChecks.cs"
  $sources += "$PSScriptRoot\PanelPlacementChecks.cs"
  $sources += "$PSScriptRoot\PanelAnimationChecks.cs"
  $sources += "$PSScriptRoot\AudioControllerChecks.cs"
  $sources += "$PSScriptRoot\BridgeOrderingChecks.cs"
  $sources += "$PSScriptRoot\PowerActionChecks.cs"
  $sources += "$PSScriptRoot\BridgeProtocolChecks.cs"
  $sources += "$PSScriptRoot\WallpaperCategoryChecks.cs"
  $sources += "$PSScriptRoot\LockScreenChecks.cs"
  $sources += "$PSScriptRoot\MediaVisualChecks.cs"
  $sources += "$PSScriptRoot\TaskPinChecks.cs"
  $sources += "$PSScriptRoot\WorkspaceLaunchChecks.cs"
  $sources += "$PSScriptRoot\QuickActionChecks.cs"
  $sources += "$PSScriptRoot\ThemeTransitionChecks.cs"
  $sources += "$PSScriptRoot\ThemeCoordinatorChecks.cs"
  $sources += "$PSScriptRoot\FocusBorderChecks.cs"
  $sources += "$PSScriptRoot\FrameGlowChecks.cs"
  $sources += "$PSScriptRoot\WallpaperFadeChecks.cs"
  New-Item -ItemType Directory -Force "$PSScriptRoot\checks" | Out-Null
  Copy-Item $naudio "$PSScriptRoot\checks\NAudio.dll" -Force
  Copy-Item "$PSScriptRoot\control-center-styles.xaml" "$PSScriptRoot\checks\control-center-styles.xaml" -Force
  Copy-Item "$PSScriptRoot\control-center-layout.json" "$PSScriptRoot\checks\control-center-layout.json" -Force
  $target = @('/target:exe', '/main:CenterChecks', "/out:$PSScriptRoot\checks\CenterChecks.exe"); $sources += "$PSScriptRoot\CenterChecks.cs"
} else {
  $target = @('/target:winexe', "/out:$PSScriptRoot\NordControlCenter.exe")
}
& "$fx\csc.exe" @facades /nologo /nowarn:1701 /platform:x64 /optimize+ @target /reference:System.Drawing.dll /reference:System.Security.dll /reference:System.Net.Http.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll /reference:System.Web.Extensions.dll "/reference:$PSScriptRoot\NAudio.dll" "/reference:$fx\System.Runtime.WindowsRuntime.dll" @winmd @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Control center build failed' }
