@echo off
powershell -NoProfile -ExecutionPolicy Unrestricted -Command ^
$ErrorActionPreference = 'Stop'; ^
if (!(Get-Command NuGet -ErrorAction SilentlyContinue) -and (!(Test-Path '%LocalAppData%\NuGet\NuGet.exe') -or !(get-item '%LocalAppData%\NuGet\nuget.exe').VersionInfo.FileVersion.StartsWith('4.1'))) { ^
	Write-Host 'Downloading NuGet.exe'; ^
	(New-Object system.net.WebClient).DownloadFile('https://dist.nuget.org/win-x86-commandline/v4.1.0/nuget.exe', '%LocalAppData%\NuGet\NuGet.exe'); ^
} ^
if (Test-Path '%LocalAppData%\NuGet\NuGet.exe') { ^
	Set-Alias NuGet (Resolve-Path %LocalAppData%\NuGet\NuGet.exe); ^
} ^
Write-Host 'Restoring NuGet packages'; ^
NuGet restore; ^
. '.\Functions.ps1'; ^
$projectName = 'Appy.GitDb.Server';  ^
$packagesPath = '.\packages';  ^
$invokeBuildPath = Get-PackagePath Invoke-Build $projectName;  ^
& $invokeBuildPath\tools\Invoke-Build.ps1 %* -File Tasks.ps1;
exit /b %ERRORLEVEL%
