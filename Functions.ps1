Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$yamelDotNetVersion,
  [Parameter(Mandatory=$True,Position=2)]
  [string]$octopusToolsVersion,
  [Parameter(Mandatory=$True,Position=3)]
  [string]$artifactsPath
)

$script:projectConfig = $null

function Get-YamlDotNetPath() {
	return ".\packages\YamlDotNet.$yamelDotNetVersion"	
}

function Get-OctopusToolsPath() {
	return ".\packages\OctopusTools.$octopusToolsVersion"
}

function Get-ProjectsForTask($task) {
	$task = $task.ToLower()

	if($projectConfig -eq $null) {		
		$yamlPackagePath = Get-YamlDotNetPath;
		Add-Type -Path "$yamlPackagePath\lib\net45\YamlDotNet.dll"
		$config = Resolve-Path ".\config.yml"
		$yaml = [IO.File]::ReadAllText($config).Replace("`t", "    ")
		$stringReader = new-object System.IO.StringReader([string]$yaml)
		$Deserializer = New-Object -TypeName YamlDotNet.Serialization.Deserializer
		$projectConfig = $Deserializer.Deserialize([System.IO.TextReader]$stringReader)
	}
	
	$config = @{
		"clean" =  "App", "XUnit", "Package", "AppPackage";
		"compile" = "App", "XUnit", "Package", "AppPackage";
		"test" = "XUnit";
		"pack" = "Package", "App", "AppPackage";
		"push" = "Package", "App", "AppPackage";
		"release" = "App";
		"deploy" = "App";
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

function Clean-Folder($folder) {
	"Cleaning $folder"
	Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
}

function Compile-Project($projectName) {
	use "15.0" MsBuild
	$projectFile = "$projectName\$projectName.csproj"
	$version = Get-Version $projectName	    
	
	Compile-Dotnet $projectFile $projectName $artifactsPath $config $version
}

function Compile-Dotnet($projectFile, $projectName, $artifactsPath, $config, $version) {	
	Write-Host "Compiling Dotnet $projectName"		
    exec { dotnet build $projectFile --configuration $config }
	
	$isDotnetWebProject = (Select-String -pattern '<Project Sdk=\"Microsoft.NET.Sdk.Web\">' -path $projectFile) -ne $null
	$isDotnetExeProject = (Select-String -pattern "<OutputType>Exe</OutputType>" -path $projectFile) -ne $null
	if ($isDotnetWebProject -or $isDotnetExeProject) {
		Write-Host "Publishing Dotnet $projectName to $artifactsPath"
		exec { dotnet publish $projectFile --no-build --no-restore --configuration $config --output ..\$artifactsPath\$projectName --version-suffix $version }
		return
	}

	$singleFramework = Get-Content $projectFile | Where { $_.Contains("<TargetFramework>") }
	$multiFrameworks = Get-Content $projectFile | Where { $_.Contains("<TargetFrameworks>") }
	$frameworks = If ($singleFramework -ne $null) { @(([xml]$singleFramework).TargetFramework) } Else { ([xml]$multiFrameworks).TargetFrameworks.Split(';') }
	$frameworks | ForEach-Object {
		$framework = $_		
		Write-Host "Publishing Dotnet $projectName with framework $framework to $artifactsPath"		
		exec { dotnet publish $projectFile --no-build --no-restore --configuration $config --output ..\$artifactsPath\$projectName\$framework --framework $framework --version-suffix $version }
	}
}

function Execute-Xunit($projectName) {
	$projectFile = "$projectName\$projectName.csproj"
		    
	Execute-Xunit-Dotnet $projectFile $projectName $artifactsPath $config    
}

function Execute-Xunit-Dotnet($projectFile, $projectName, $artifactsPath, $config) {
	$singleFramework = Get-Content $projectFile | Where { $_.Contains("<TargetFramework>") }
	$multiFrameworks = Get-Content $projectFile | Where { $_.Contains("<TargetFrameworks>") }
	$frameworks = If ($singleFramework -ne $null) { @(([xml]$singleFramework).TargetFramework) } Else { ([xml]$multiFrameworks).TargetFrameworks.Split(';') }

	$frameworks | ForEach-Object {
		$framework = $_		
		Write-Host "Executing tests for $projectName with framework $framework to $artifactsPath"		
		exec { dotnet test $projectFile --configuration $config --no-build --no-restore --framework $framework --test-adapter-path:. "--logger:xunit;LogFilePath=../$artifactsPath/xunit-$projectName-$framework.xml" --verbosity quiet }
	}
}

$versions = @{}
function Get-VersionFromAssemblyInfo($path){
    if (!(Test-Path $path)) {
        return
    }
    $line = Get-Content $path | Where { $_.Contains("AssemblyVersion") }
    if ($line){
        return $line.Split('"')[1]
    }
    return
}

function Get-VersionFromCsProj($path){
    if (!(Test-Path $path)) {
        return
    }
    $line = Get-Content $path | Where { $_.Contains("<Version>") }
    if ($line){
        return ([xml]$line).Version
    }
    return
}

function Get-Version($projectName) {
	if (!$versions.ContainsKey($projectName)){

        $version = Get-VersionFromAssemblyInfo "$projectName\Properties\AssemblyInfo.cs"
        if (!$version) {
            $version = Get-VersionFromAssemblyInfo "$projectName\..\SharedAssemblyInfo.cs"
        }
        if (!$version){
            $version = Get-VersionFromCsProj "$projectName\$projectName.csproj"
        }		
        if (!$version) {
			throw "Couldn't find an AssemblyVersion from the AssemblyInfo or .csproj file"
		}

		$branch = Get-CurrentBranch
		$isGeneralRelease = Is-GeneralRelease $branch
		$isLocalBuild = Is-LocalBuild

		if ($isLocalBuild -or !$isGeneralRelease){
            if ([String]::IsNullOrEmpty($script:prereleaseNumber)) {
                $script:prereleaseNumber = Get-PrereleaseNumber $branch;
            }
			$version = "$($version.Replace("*", 0))-$script:prereleaseNumber"
		} else {
			$version = $version.Replace("*", $env:APPVEYOR_BUILD_NUMBER)
		}
		$versions.Add($projectName, $version)
	}

	return $versions[$projectName]
}

function Get-CurrentBranch {
	if([String]::IsNullOrEmpty($env:APPVEYOR_REPO_BRANCH)){
		$branch = git branch | Where {$_ -match "^\*(.*)"} | Select-Object -First 1
	} else{
		$branch = $env:APPVEYOR_REPO_BRANCH
	}
	return $branch
}

function Is-GeneralRelease($branch) {
	return ($branch -eq "develop" -or $branch -eq "master")
}

function Is-LocalBuild(){
	return [String]::IsNullOrEmpty($env:APPVEYOR_REPO_BRANCH)
}

function Get-PrereleaseNumber($branch) {
	$branch = $branch.Replace("* ", "")
    if ($branch.IndexOf("/") -ne -1){
        $prefix = $branch.Substring($branch.IndexOf("/") + 1)
    } else {
        $prefix = $branch
    }

    $prefix = $prefix.Substring(0, [System.Math]::Min(10, $prefix.Length))
	return $prefix + "-" + $(Get-Date).ToString("yyMMddHHmmss") -Replace "[^a-zA-Z0-9-]", ""
}

function Pack-Project($projectName) {
    use "15.0" MsBuild
	Write-Host "Packing $projectName $version to $artifactsPath"
	$version = Get-Version $projectName
    $projectFile = "$projectName\$projectName.csproj"

	Pack-Dotnet $projectFile $version
}

function Pack-Dotnet($projectFile, $version) {		
	exec { dotnet pack $projectFile --configuration $config --no-build --no-restore --output ..\$artifactsPath -p:PackageVersion=$version }  
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

function OctoPack-Project($project) {
	$octopusToolsPath = Get-OctopusToolsPath
	$version = Get-Version $project
	Write-Host "Packing $project $version to $artifactsPath"
	$releaseNotes = Get-ReleaseNotes
	exec { & $octopusToolsPath\tools\Octo.exe pack --basePath=$artifactsPath\$project --outFolder=$artifactsPath --id=$project --version=$version --releaseNotes=$releaseNotes --overwrite}
}

function Get-ReleaseNotes() {
	$commit = git rev-parse HEAD
	$shortCommit = $commit.Substring(0,7)
	$branch = Get-CurrentBranch
	$branch = $branch.Replace("* ", "")
	$author = git --no-pager show -s --format='%an' HEAD
	$authorEmail = git --no-pager show -s --format='%ae' HEAD
	$computerName = $env:computername
	return "Build date: $(Get-Date -f 'yyyy-MM-dd HH:mm:ss')`rBuilt on: $computerName`rCommit Author: $author`rCommit author Email: $authorEmail`rBranch: $branch`rCommit: $shortCommit"
}

function Octo-CreateRelease($project) {
	$octopusToolsPath = Get-OctopusToolsPath
	$version = Get-Version $project
	$releaseNotes = Get-ReleaseNotes
	Write-Host "Creating release $version of $project on $env:ylp_octopusDeployServer"
	exec { & $octopusToolsPath\tools\Octo.exe create-release --server="$env:ylp_octopusDeployServer" --apiKey="$env:ylp_octopusDeployApiKey" --project="$project" --version="$version" --releaseNotes=$releaseNotes --packageVersion="$version" --ignoreexisting }
}

function Octo-DeployRelease($project) {
	$octopusToolsPath = Get-OctopusToolsPath
	$version = Get-Version $project

	$environments = $env:ylp_environment.Split(",")

	$environments | ForEach-Object{
		$environment = $_	
		Write-Host "Deploying release $version of $project on $env:ylp_octopusDeployServer to $environment"
		exec { & $octopusToolsPath\tools\Octo.exe deploy-release --server="$env:ylp_octopusDeployServer" --apiKey="$env:ylp_octopusDeployApiKey" --project="$project" --version="$version" --deployto="$environment" }
	}
}