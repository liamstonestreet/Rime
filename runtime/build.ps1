$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll ("/out:"+(Join-Path $PSScriptRoot 'NordRuntime.exe')) (Join-Path $PSScriptRoot 'NordRuntime.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime build failed'}
