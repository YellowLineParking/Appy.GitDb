@echo off
powershell -NoProfile -ExecutionPolicy Unrestricted -Command ^
$ErrorActionPreference = 'Stop'; ^
if (!(Get-Command NuGet -ErrorAction SilentlyContinue) -and !(Test-Path '%LocalAppData%\NuGet\NuGet.exe')) { ^
	Write-Host 'Downloading NuGet.exe'; ^
	(New-Object system.net.WebClient).DownloadFile('https://dist.nuget.org/win-x86-commandline/latest/nuget.exe', '%LocalAppData%\NuGet\NuGet.exe'); ^
} ^
if (Test-Path '%LocalAppData%\NuGet\NuGet.exe') { ^
	Set-Alias NuGet (Resolve-Path %LocalAppData%\NuGet\NuGet.exe); ^
} ^
Write-Host 'Restoring NuGet packages'; ^
NuGet restore; ^
. '.\Functions.ps1'; ^
$basePath = Resolve-Path .; ^
$packagesPath = 'packages'; ^
$invokeBuildPath = Get-RequiredPackagePath Invoke-Build $basePath\Ylp.GitDb.Server; ^
& $invokeBuildPath\tools\Invoke-Build.ps1 %* -File build.ps1;
exit /b %ERRORLEVEL%
