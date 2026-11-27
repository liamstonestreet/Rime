param([switch]$Probe)
$ErrorActionPreference='Stop'
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$facadeRoot=Join-Path $PSScriptRoot '..\control-center\refs\build\.NETFramework\v4.8\Facades'
$refs=@(Get-ChildItem "$env:WINDIR\System32\WinMetadata\*.winmd" | ForEach-Object {'/reference:'+ $_.FullName})
$refs+=@(Get-ChildItem "$facadeRoot\*.dll" | ForEach-Object {'/reference:'+ $_.FullName})
$refs+=@('WindowsBase','PresentationCore','PresentationFramework') | ForEach-Object {'/reference:'+ (Join-Path $fx "WPF\$_.dll")}
$source=if($Probe){'NotificationProbe.cs'}else{'NordNotificationListener.cs'}
$name=if($Probe){'NotificationProbe.exe'}else{'NordNotificationListener.exe'}
$target=if($Probe){'/target:exe'}else{'/target:winexe'}
. "$PSScriptRoot\..\shared\Invoke-Csc.ps1"
Invoke-Csc "$fx\csc.exe" (@('/nologo','/nowarn:1701',$target,'/platform:x64','/optimize+',"/out:$PSScriptRoot\$name",'/reference:System.Xaml.dll','/reference:System.Web.Extensions.dll',"/reference:$fx\System.Runtime.WindowsRuntime.dll") + @($refs) + @((Join-Path $PSScriptRoot $source),(Join-Path $PSScriptRoot '..\shared\NotificationItem.cs')))
if($LASTEXITCODE -ne 0){throw 'Notification companion build failed'}
