#region Variables

$script:artifactsPath = "artifacts"

#endregion

#region General
function Get-PackagePath($packageId, $project) {
	if (!(Test-Path "$project\packages.config")) {
		throw "Could not find a packages.config file at $project"
	}
	
	[xml]$packagesXml = Get-Content "$project\packages.config"
	$package = $packagesXml.packages.package | Where { $_.id -eq $packageId }
	if (!$package) {
		throw "$packageId is required in $project, but it is not installed. Please install $packageId in $project"
	}
	return "$packagesPath\$($package.id).$($package.version)"
}

$script:projectConfig = $null
function Get-ProjectsForTask($task){
	$task = $task.ToLower()

	if($projectConfig -eq $null){
		$yamlPackagePath = Get-PackagePath "YamlDotNet" $projectName
		Add-Type -Path "$yamlPackagePath\lib\dotnet\yamldotnet.dll"
		$config = Resolve-Path ".\config.yml"
		$yaml = [IO.File]::ReadAllText($config).Replace("`t", "    ")
		$stringReader = new-object System.IO.StringReader([string]$yaml)
		$Deserializer = New-Object -TypeName YamlDotNet.Serialization.Deserializer -ArgumentList $null, $null, $false
		$projectConfig = $Deserializer.Deserialize([System.IO.TextReader]$stringReader)
	}
	
	$config = @{
		"clean" = "EmbeddedWebJob", "StandaloneWebJob", "App", "VsTest", "XUnit", "VsTestAndXUnit", "Package";
		"compile" = "EmbeddedWebJob", "StandaloneWebJob", "App", "VsTest", "XUnit", "VsTestAndXUnit", "Package";
		"test" = "VsTest", "XUnit", "VsTestAndXUnit";
		"pack" = "StandaloneWebJob", "Package", "App";
		"push" = "StandaloneWebJob", "Package", "App";
		"release" = "StandaloneWebJob", "App";
		"deploy" = "StandaloneWebJob", "App";
	}
	$projectTypes = $config[$task]

    return $projectConfig.Keys | 
        Where { 
			($projectTypes -contains $projectConfig[$_]["Type"] -and `
			($projectConfig[$_]["Exclude"] -eq $null -or $projectConfig[$_]["Exclude"].ToLower().IndexOf($task) -eq -1) -or `
			 ($projectConfig[$_]["Include"] -ne $null -and $projectConfig[$_]["Include"].ToLower().IndexOf($task) -ne -1))
		} | 
        ForEach-Object { 
            @{
                "Name" = $_;
                "Type" = $projectConfig[$_]["Type"];
                "Config" = $projectConfig[$_]["Config"];
            }}
}

#endregion

#region "Clean"

function Clean-Folder($folder){
	"Cleaning $folder"
	Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
}

#endregion

#region Compile

function Compile-Project($projectName) {
	use "15.0" MsBuild
	$projectFile = "$projectName\$projectName.csproj"
	$isWebProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -ne $null) -and ((Select-String -pattern "<OutputType>WinExe</OutputType>" -path $projectFile) -eq $null))
	$isWinProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -eq $null) -and ((Select-String -pattern "<OutputType>WinExe</OutputType>" -path $projectFile) -ne $null))
	$isExeProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -eq $null) -and ((Select-String -pattern "<OutputType>Exe</OutputType>" -path $projectFile) -ne $null))
	
	if ($isWebProject) {
		Write-Host "Compiling $projectName to $artifactsPath"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:DebugType=None /p:Platform=AnyCpu /p:WebProjectOutputDir=..\$artifactsPath\$projectName /p:OutDir=$artifactsPath\bin /verbosity:quiet }
	}
	elseif ($isWinProject -or $isExeProject) {
		Write-Host "Compiling $projectName to $artifactsPath"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:DebugType=None /p:Platform=AnyCpu /p:OutDir=..\$artifactsPath\$projectName /verbosity:quiet /p:Disable_CopyWebApplication=True }
	}
	else{
		Write-Host "Compiling $projectName"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:Platform=AnyCpu /verbosity:quiet }
	}
}

function Move-WebJob($projectName, $target, $type){
	Write-Host "Moving WebJob $projectName into $target as a $type job"
	$sourcePath = "$artifactsPath/$projectName"

	if($projectName -eq $target){
		$tempPath = "$artifactsPath\temp"
		New-Item $tempPath -ItemType Directory -Force
		Move-Item -Path $sourcePath -Destination $tempPath | Out-Null
		$sourcePath = "$artifactsPath\temp\*"
	}

	$targetPath = "$artifactsPath\$target\App_Data\Jobs\$type"
	New-Item -Path $targetPath -ItemType Directory -Force
	Copy-Item $sourcePath $targetPath -Force -Recurse | Out-Null

	if($projectName -eq $target){
		Remove-Item "$artifactsPath\temp" -Recurse -Force | Out-Null
	}
}

#endregion

#region Tests

function Execute-Xunit($projectName){
	$xunitRunnerPath = Get-PackagePath XUnit.Runner.Console "$projectName"
	$runnerExecutable = "$xunitRunnerPath\tools\xunit.console.exe"
	exec { & $runnerExecutable $projectName\bin\$config\$projectName.dll -xml "$artifactsPath\xunit_$projectName.xml" -html "$artifactsPath\xunit_$projectName.html" -nologo }
}

function Execute-VsTest ($projectName){
	$vstestrunner = Resolve-VsTestRunner
	
	$path = Resolve-Path "$projectName\bin\$config\$projectName.dll"

	try
	{
		if (Is-LocalBuild) {
			$logger = "trx"
		}
		else {
			$logger = "AppVeyor"
		}
		exec { & $vstestrunner $path /logger:$logger  }
	}
	finally
	{
		# Move the files from the project dir to the output dir because trx does not support defining the outputfile
		if(Test-Path "TestResults")
		{
			Get-ChildItem "TestResults" -Filter *.trx | 
				Foreach-Object {
					Move-Item -Path $_.FullName -Destination $artifactsPath\$projectName.trx
				}
		}
	}
}

function Resolve-VsTestRunner {
	$vstestrunner = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
	if(Test-Path $vstestrunner){
		Write-Host "Using vstest.console.exe from VS 2017 Install"
		return $vstestrunner
	}

	$vstestrunner = "C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
	if(Test-Path $vstestrunner){
		Write-Host "Using vstest.console.exe from VS 2015 Install"
		return $vstestrunner
	}

	Write-Host "No VS install detected, Using vstest.console.exe from PATH"
	return "vstest.console.exe"
}

#endregion

#region Push and Pack
$versions = @{}
function Get-Version($projectName) {
	if(!$versions.ContainsKey($projectName)){

		$line = Get-Content "$projectName\Properties\AssemblyInfo.cs" | Where { $_.Contains("AssemblyVersion") }
		if (!$line) {
			throw "Couldn't find an AssemblyVersion attribute"
		}
		$version = $line.Split('"')[1]
		$branch = Get-CurrentBranch
		$isGeneralRelease = Is-GeneralRelease $branch
		$isLocalBuild = Is-LocalBuild

		if($isLocalBuild -or !$isGeneralRelease){
			$version = "$($version.Replace("*", 0))-$(Get-PrereleaseNumber $branch)"
		} else{
			$version = $version.Replace("*", $env:APPVEYOR_BUILD_NUMBER)
		}
		$versions.Add($projectName, $version)
	}

	return $versions[$projectName]
}

function Get-CurrentBranch{
	if([String]::IsNullOrEmpty($env:APPVEYOR_REPO_BRANCH)){
		$branch = git branch | Where {$_ -match "^\*(.*)"} | Select-Object -First 1
	} else{
		$branch = $env:APPVEYOR_REPO_BRANCH
	}
	return $branch
}

function Is-GeneralRelease($branch){
	return ($branch -eq "develop" -or $branch -eq "master")
}

function Is-LocalBuild(){
	return [String]::IsNullOrEmpty($env:APPVEYOR_REPO_BRANCH)
}

function Get-PrereleaseNumber($branch){
	$branch = $branch.Replace("* ", "")
    if($branch.IndexOf("/") -ne -1){
        $prefix = $branch.Substring(0, $branch.IndexOf("/") + 1)
    }else{
        $prefix = $branch
    }

    $prefix = $prefix.Substring(0, [System.Math]::Min(7, $prefix.Length))
	return $prefix + "-" + $(Get-Date).ToString("yyMMddHHmmss") -Replace "[^a-zA-Z0-9-]", ""
}

function Pack-Project($projectName){
	$version = Get-Version $projectName
	Write-Host "Packing $projectName $version to $artifactsPath"
	exec { & NuGet pack $projectName\$projectName.csproj -Build -Properties Configuration=$config -OutputDirectory $artifactsPath -Version $version -IncludeReferencedProjects -NonInteractive }
}

function Push-Package($package, $nugetPackageSource, $nugetPackageSourceApiKey, $ignoreNugetPushErrors) {
	$package = $package -replace "\.", "\."
	$package = @(Get-ChildItem $artifactsPath\*.nupkg) | Where-Object {$_.Name -match "$package\.\d*\.\d*\.\d*.*\.nupkg"}
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

#endregion

#region Octopus Deploy

function OctoPack-Project($project){
	$octopusToolsPath = Get-PackagePath OctopusTools $projectName
	$version = Get-Version $project
	Write-Host "Packing $project $version to $artifactsPath"
	$releaseNotes = Get-ReleaseNotes
	exec { & $octopusToolsPath\tools\Octo.exe pack --basePath=$artifactsPath\$project --outFolder=$artifactsPath --id=$project --version=$version --releaseNotes=$releaseNotes --overwrite}
}

function Get-ReleaseNotes(){
	$commit = git rev-parse HEAD
	$shortCommit = $commit.Substring(0,7)
	$branch = Get-CurrentBranch
	$branch = $branch.Replace("* ", "")
	$author = git --no-pager show -s --format='%an' HEAD
	$authorEmail = git --no-pager show -s --format='%ae' HEAD
	$computerName = $env:computername
	return "Build date: $(Get-Date -f 'yyyy-MM-dd HH:mm:ss')`rBuilt on: $computerName`rCommit Author: $author`rCommit author Email: $authorEmail`rBranch: $branch`rCommit: $shortCommit"
}

function Octo-CreateRelease($project){
	$octopusToolsPath = Get-PackagePath OctopusTools $projectName
	$version = Get-Version $project
	$releaseNotes = Get-ReleaseNotes
	Write-Host "Creating release $version of $project on $env:ylp_octopusDeployServer"
	exec { & $octopusToolsPath\tools\Octo.exe create-release --server="$env:ylp_octopusDeployServer" --apiKey="$env:ylp_octopusDeployApiKey" --project="$project" --version="$version" --releaseNotes=$releaseNotes --packageVersion="$version" --ignoreexisting }
}

function Octo-DeployRelease($project){
	$octopusToolsPath = Get-PackagePath OctopusTools $projectName
	$version = Get-Version $project

	$environments = $env:ylp_environment.Split(",")

	$environments | ForEach-Object{
		$environment = $_	
		Write-Host "Deploying release $version of $project on $env:ylp_octopusDeployServer to $environment"
		exec { & $octopusToolsPath\tools\Octo.exe deploy-release --server="$env:ylp_octopusDeployServer" --apiKey="$env:ylp_octopusDeployApiKey" --project="$project" --version="$version" --deployto="$environment" }
	}
}

#endregion