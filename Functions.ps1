function Get-RequiredPackagePath($packageId, $path) {
	$package = Get-PackageInfo $packageId $path
	if (!$package.Exists) {
		throw "$packageId is required in $path, but it is not installed. Please install $packageId in $path"
	}
	return $package.Path
}

function Get-AssemblyFileVersion($assemblyInfoFile) {
	$line = Get-Content $assemblyInfoFile | Where { $_.Contains("AssemblyFileVersion") }
	if (!$line) {
		$line = Get-Content $assemblyInfoFile | Where { $_.Contains("AssemblyVersion") }
		if (!$line) {
			throw "Couldn't find an AssemblyFileVersion or AssemblyVersion attribute"
		}
	}
	return $line.Split('"')[1]
}

function Get-PackageInfo($packageId, $path) {
	if (!(Test-Path "$path\packages.config")) {
		return New-Object PSObject -Property @{
			Exists = $false;
		}
	}
	
	[xml]$packagesXml = Get-Content "$path\packages.config"
	$package = $packagesXml.packages.package | Where { $_.id -eq $packageId }
	if (!$package) {
		return New-Object PSObject -Property @{
			Exists = $false;
		}
	}
	
	$versionComponents = $package.version.Split('.')
    [array]::Reverse($versionComponents)
		
	$numericalVersion = 0
	$modifier = 1
	
	foreach ($component in $versionComponents) {
		$numericalComponent = $component -as [int]
		if ($numericalComponent -eq $null) {
			continue
		}
		$numericalVersion = $numericalVersion + ([int]$numericalComponent * $modifier)
		$modifier = $modifier * 10
	}
	
	return New-Object PSObject -Property @{
		Exists = $true;
		Version = $package.version;
		Number = $numericalVersion;
		Id = $package.id;
		Path = "$packagesPath\$($package.id).$($package.version)"
	}
}

function Push-Package($package, $nugetPackageSource, $nugetPackageSourceApiKey, $ignoreNugetPushErrors) {
	try {
		if (![string]::IsNullOrEmpty($nugetPackageSourceApiKey) -and $nugetPackageSourceApiKey -ne "LoadFromNuGetConfig") {
			$out = NuGet push $package -Source $nugetPackageSource -ApiKey $nugetPackageSourceApiKey 2>&1
		}
		else {
			$out = NuGet push $package -Source $nugetPackageSource 2>&1
		}
		Write-Host $out
	}
	catch {
		$errorMessage = $_
		$ignoreNugetPushErrors.Split(";") | foreach {
			if ($([String]$errorMessage).Contains($_)) {
				$isNugetPushError = $true
			}
		}
		if (!$isNugetPushError) {
			throw
		}
		else {
			Write-Host "WARNING: $errorMessage"
		}
	}
}

function Convert-Project($projectName, $config, $out) {
	use $msBuildVersion MSBuild
	$projectFile = "$projectName\$projectName.csproj"
	$isWebProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -ne $null) -and ((Select-String -pattern "<OutputType>WinExe</OutputType>" -path $projectFile) -eq $null))
	$isWinProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -eq $null) -and ((Select-String -pattern "<OutputType>WinExe</OutputType>" -path $projectFile) -ne $null))
	$isExeProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -eq $null) -and ((Select-String -pattern "<OutputType>Exe</OutputType>" -path $projectFile) -ne $null))
	
	if ($isWebProject) {
		Write-Host "Compiling $projectName to $out"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:DebugType=None /p:Platform=AnyCpu /p:WebProjectOutputDir=$out\$projectName /p:OutDir=$out\bin /verbosity:quiet }
	}
	elseif ($isWinProject -or $isExeProject) {
		Write-Host "Compiling $projectName to $out"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:DebugType=None /p:Platform=AnyCpu /p:OutDir=$out\$projectName /verbosity:quiet }
	}
	else{
		Write-Host "Compiling $projectName"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:Platform=AnyCpu /verbosity:quiet }
	}
}

function Execute-Xunit($projectName, $config, $out){
	New-Item $out -Type directory -Force | Out-Null

	$xunitRunnerPath = Get-RequiredPackagePath XUnit.Runner.Console "$projectName"
	$runnerExecutable = "$xunitRunnerPath\tools\xunit.console.exe"
	exec { & $runnerExecutable $projectName\bin\$config\$projectName.dll -xml "$out\xunit.xml" -html "$out\xunit.html" -nologo }
}

function Pack-Project($projectName, $config, $out){
	$null = @(
		New-Item $out -Type directory -Force | Out-Null
		$version = Get-AssemblyFileVersion "$projectName\properties\assemblyInfo.cs"
		Write-Host "Packing $projectName $version to $out"
		exec { & NuGet pack $projectName\$projectName.csproj -Build -Properties Configuration=$config -OutputDirectory $out -Version $version -IncludeReferencedProjects }
	)	
	return $version
}

function OctoPack-Project($projectName, $out){
	$null = @(
		$octopusToolsPath = Get-RequiredPackagePath OctopusTools "$projectName"
		$version = Get-AssemblyFileVersion "$projectName\properties\assemblyInfo.cs"
		Write-Host "Packing $projectName $version to $out"
		exec { & $octopusToolsPath\tools\Octo.exe pack --basePath=$out\$projectName --outFolder=$out --id=$projectName --version=$version }
	)
	return $version
}

function Octo-CreateRelease($projectName, $version, $server, $apiKey){
	$octopusToolsPath = Get-RequiredPackagePath OctopusTools $projectName
	Write-Host "Creating release $version of $projectName on $server"
	exec { & $octopusToolsPath\tools\Octo.exe create-release --server="$server" --apiKey="$apiKey" --project="$projectName" --version="$version" --ignoreexisting }
}

function Octo-DeployRelease($projectName, $version, $environment, $server, $apiKey){
	$octopusToolsPath = Get-RequiredPackagePath OctopusTools $projectName
	Write-Host "Deploying release $version of $projectName on $server to $environment"
	exec { & $octopusToolsPath\tools\Octo.exe deploy-release --server="$server" --apiKey="$apiKey" --project="$projectName" --version="$version" --deployto="$environment" }
}

function Clean-Folders($folders){
	$folders | foreach {
		"Cleaning $_"
		Remove-Item $_ -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
	}
}