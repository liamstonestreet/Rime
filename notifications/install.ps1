param([Parameter(Mandatory=$true)][string]$PackDir,[switch]$BuildOnly)
$ErrorActionPreference='Stop'
$pack= [IO.Path]::GetFullPath($PackDir)
$sdk=Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
if(-not $sdk){throw 'Windows SDK packaging tools are required for the optional notification companion.'}
$signer=Join-Path $sdk.DirectoryName 'signtool.exe'
& (Join-Path $PSScriptRoot 'build.ps1')
$stage=Join-Path $PSScriptRoot 'package'
New-Item -ItemType Directory -Force $stage,(Join-Path $stage 'Assets') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NordNotificationListener.exe') -Destination $stage -Force
[IO.File]::WriteAllText((Join-Path $stage 'bridge-location.txt'),$pack,[Text.UTF8Encoding]::new($false))
[xml]$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw
$days=[int]([DateTime]::UtcNow.Date-[DateTime]'2026-01-01').TotalDays
$version=[Version]("1.0.$days."+[int]([DateTime]::UtcNow.TimeOfDay.TotalSeconds/2))
if(-not $BuildOnly){$old=Get-AppxPackage -Name 'NordRice.Notifications';if($old -and [Version]$old.Version -ge $version){$prior=[Version]$old.Version;$version=if($prior.Revision -lt 65535){[Version]::new($prior.Major,$prior.Minor,$prior.Build,$prior.Revision+1)}else{[Version]::new($prior.Major,$prior.Minor,$prior.Build+1,0)}}}
$manifest.Package.Identity.Version=$version.ToString()
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))
Add-Type -AssemblyName System.Drawing
foreach($asset in @(@{name='Logo44.png';size=44},@{name='Logo150.png';size=150},@{name='StoreLogo.png';size=50})){
 $bitmap=New-Object Drawing.Bitmap($asset.size,$asset.size);$g=[Drawing.Graphics]::FromImage($bitmap);$pen=New-Object Drawing.Pen([Drawing.Color]::FromArgb(136,192,208),2.5)
 try{$g.Clear([Drawing.Color]::FromArgb(35,42,53));$g.SmoothingMode='AntiAlias';$g.ScaleTransform($asset.size/44.0,$asset.size/44.0);$g.DrawArc($pen,12,8,20,25,180,180);$g.DrawLine($pen,12,20,10,32);$g.DrawLine($pen,32,20,34,32);$g.DrawLine($pen,10,32,34,32);$g.DrawArc($pen,18,30,8,8,0,180);$bitmap.Save((Join-Path $stage ('Assets\'+$asset.name)),[Drawing.Imaging.ImageFormat]::Png)}finally{$pen.Dispose();$g.Dispose();$bitmap.Dispose()}
}
$package=Join-Path $PSScriptRoot 'NordRice.Notifications.msix'
& $sdk.FullName pack /o /d $stage /p $package | Out-Host
if($LASTEXITCODE -ne 0){throw 'Notification package validation failed'}
if($BuildOnly){Write-Output "Validated notification package: $package";return}
# Local development distribution: Windows deployment needs this signer in LocalMachine\TrustedPeople.
# The private key stays non-exportable in CurrentUser\My; no PFX is written.
$subject='CN=NordRice Notifications (local)'
$cert=Get-ChildItem Cert:\CurrentUser\My | Where-Object {$_.Subject -eq $subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(7)} | Sort-Object NotAfter -Descending | Select-Object -First 1
if(-not $cert){$cert=New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(2)}
$public=Join-Path $PSScriptRoot 'NordRice.Notifications.cer'
Export-Certificate -Cert $cert -FilePath $public -Force | Out-Null
$trustPath='Cert:\LocalMachine\TrustedPeople\'+$cert.Thumbprint
if(-not (Test-Path -LiteralPath $trustPath)){
 $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
 $principal=New-Object Security.Principal.WindowsPrincipal($identity)
 if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){
  $quotedPublic=$public.Replace("'","''")
  throw "One-time administrator setup required for Windows package trust. Open PowerShell as administrator and run: Import-Certificate -FilePath '$quotedPublic' -CertStoreLocation Cert:\LocalMachine\TrustedPeople . Then rerun the notification installer in your normal user account."
 }
 Import-Certificate -FilePath $public -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
 if(-not (Test-Path -LiteralPath $trustPath)){throw 'Windows did not retain the notification signing certificate in LocalMachine\TrustedPeople.'}
}

& $signer sign /fd SHA256 /s My /sha1 $cert.Thumbprint $package | Out-Host
if($LASTEXITCODE -ne 0){throw 'Notification package signing failed'}
Add-AppxPackage -Path $package -ForceApplicationShutdown
$installed=Get-AppxPackage -Name 'NordRice.Notifications' | Select-Object -First 1
if(-not $installed){throw 'Notification companion was not registered'}
$config=@{appId=$installed.PackageFamilyName+'!Listener'} | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $pack 'notification-companion.json'),$config,[Text.UTF8Encoding]::new($false))
Write-Output 'Notification companion installed for this user. Click the far-left notification bell, then click Connect Windows to grant access.'
