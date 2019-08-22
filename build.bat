@echo off
powershell -NoProfile -ExecutionPolicy Unrestricted -Command ^
$ErrorActionPreference = 'Stop'; ^
if (!(Get-Command NuGet -ErrorAction SilentlyContinue) -and (!(Test-Path '%LocalAppData%\NuGet\NuGet.exe') -or !(get-item '%LocalAppData%\NuGet\nuget.exe').VersionInfo.FileVersion.StartsWith('4.9'))) { ^
	Write-Host 'Downloading NuGet.exe'; ^
	(New-Object system.net.WebClient).DownloadFile('https://dist.nuget.org/win-x86-commandline/v4.9.2/nuget.exe', '%LocalAppData%\NuGet\NuGet.exe'); ^
} ^
if (Test-Path '%LocalAppData%\NuGet\NuGet.exe') { ^
	Set-Alias NuGet (Resolve-Path %LocalAppData%\NuGet\NuGet.exe); ^
} ^
Write-Host 'Restoring NuGet packages'; ^
NuGet restore; ^

$invokeBuildVesion = '5.4.2'; ^
$yamelDotNetVersion = '5.4.0'; ^
$octopusToolsVersion = '3.5.4'; ^

Write-Host 'Installing Invoke-Build'; ^
NuGet install Invoke-Build -Version $invokeBuildVesion -Source https://api.nuget.org/v3/index.json -OutputDirectory packages; ^

Write-Host 'Installing YamlDotNet'; ^
NuGet install YamlDotNet -Version $yamelDotNetVersion -OutputDirectory packages; ^

Write-Host 'Installing OctopusTools'; ^
NuGet install OctopusTools -Version $octopusToolsVersion -OutputDirectory packages; ^

. '.\functions.ps1' -yamelDotNetVersion $yamelDotNetVersion -octopusToolsVersion $octopusToolsVersion -artifactsPath artifacts; ^
$packagesPath = '.\packages';  ^
$invokeBuildPath = '.\packages\Invoke-Build.' + $invokeBuildVesion; ^
& $invokeBuildPath\tools\Invoke-Build.ps1 %* -File tasks.ps1;
exit /b %ERRORLEVEL%

