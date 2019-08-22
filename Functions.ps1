#region Variables

$script:artifactsPath = "artifacts"
$script:prereleaseNumber = ""
$script:ylpBuildVersion = "2.0.0"
$script:buildTools = New-Object BuildTools

#endregion

#region General

$script:projectConfig = $null
function Get-ProjectsForTask($task){
	$task = $task.ToLower()

	if($projectConfig -eq $null) {	
        $yamlPackagePath = $buildTools.GetPackagePath("YamlDotNet");
		Add-Type -Path "$yamlPackagePath\lib\net45\YamlDotNet.dll";
		$config = Resolve-Path ".\config.yml"
		$yaml = [IO.File]::ReadAllText($config).Replace("`t", "    ")
		$stringReader = new-object System.IO.StringReader([string]$yaml)
		$Deserializer = New-Object -TypeName YamlDotNet.Serialization.Deserializer
		$projectConfig = $Deserializer.Deserialize([System.IO.TextReader]$stringReader)

        if($projectConfig -eq $null) {
            Write-Warning "The config.yml file contains no configuration";
            return;
        }
	}
	
	$config = @{
		"clean" = "EmbeddedWebJob", "StandaloneWebJob", "App", "VsTest", "XUnit", "VsTestAndXUnit", "Package", "AppPackage";
		"compile" = "EmbeddedWebJob", "StandaloneWebJob", "App", "VsTest", "XUnit", "VsTestAndXUnit", "Package", "AppPackage";
		"test" = "VsTest", "XUnit", "VsTestAndXUnit";
		"pack" = "StandaloneWebJob", "Package", "App", "AppPackage";
		"push" = "StandaloneWebJob", "Package", "App", "AppPackage";
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

function Get-PackagePath($packageId, $project) {
    return $buildTools.GetPackagePath($packageId, $project);
}#endregion

#region "Clean"

function Clean-Folder($folder){
	"Cleaning $folder"
	Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
}

#endregion

#region Compile

function Compile-Project($projectName) {
	use "15.0" MsBuild
    $version = Get-Version $projectName
    $framework = $buildTools.GetBuildVersion($projectName);

	if ($framework.UseMsBuild) {
		Compile-MsBuild $projectName $artifactsPath $config
	}
    elseif ($framework.UseDotNetCli){
		Compile-DotNetCli $projectName $artifactsPath $config $version
    }
}

function Compile-MsBuild($projectName, $artifactsPath, $config) {
    $projectFile = $buildTools.GetProjectFilePath($projectName)
	$isWebProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -ne $null) -and ((Select-String -pattern "<OutputType>WinExe</OutputType>" -path $projectFile) -eq $null))
	$isWinProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -eq $null) -and ((Select-String -pattern "<OutputType>WinExe</OutputType>" -path $projectFile) -ne $null))
	$isExeProject = (((Select-String -pattern "<UseIISExpress>.+</UseIISExpress>" -path $projectFile) -eq $null) -and ((Select-String -pattern "<OutputType>Exe</OutputType>" -path $projectFile) -ne $null))
    
	if ($isWebProject) {
		Write-Host "Compiling $projectName to $artifactsPath"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:DebugType=None /p:Platform=AnyCpu /p:WebProjectOutputDir=..\$artifactsPath\$projectName /p:OutDir=..\$artifactsPath\bin /verbosity:quiet }
	}
	elseif ($isWinProject -or $isExeProject) {
		Write-Host "Compiling $projectName to $artifactsPath"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:DebugType=None /p:Platform=AnyCpu /p:OutDir=..\$artifactsPath\$projectName /verbosity:quiet /p:Disable_CopyWebApplication=True }
	}
	else {
		Write-Host "Compiling $projectName"
		exec { MSBuild $projectFile /p:Configuration=$config /nologo /p:Platform=AnyCpu /verbosity:quiet }
	}
}

function Compile-DotNetCli($projectName, $artifactsPath, $config, $version) {
    $projectFile = $buildTools.GetProjectFilePath($projectName)
	$isAzureFunctionDotnetProject = (Select-String -pattern "<AzureFunctionsVersion>.+</AzureFunctionsVersion>" -path $projectFile) -ne $null
	if ($isAzureFunctionDotnetProject) {    
        Write-Host "===== Packing Function ====="
        Write-Host "Artifacts: $artifactsPath"
        Write-Host "Working Directory: " (Get-Location).Path
        Write-Host "Compiling $projectName to ..\$artifactsPath\$projectName"
        
        exec { dotnet publish "$projectFile" -o "..\$artifactsPath\$projectName" }
		return
    }
	
	Write-Host "Compiling Dotnet $projectName"		
    exec { dotnet build $projectFile --configuration $config --no-restore  }
	
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

function Execute-Xunit($projectName) {
    $frameworkVersion = $buildTools.GetBuildVersion($projectName);
	
	if ($frameworkVersion.UseMsBuild) {
		Execute-Xunit-MsBuild $projectName $artifactsPath
	}
    elseif ($frameworkVersion.UseDotNetCli){
		Execute-Xunit-DotNetCli $projectName $artifactsPath $config
    }
}

function Execute-Xunit-MsBuild($projectName, $artifactsPath) {
	$xunitRunnerPath = $buildTools.GetPackagePath("XUnit.Runner.Console", "$projectName")
	$runnerExecutable = "$xunitRunnerPath\tools\xunit.console.exe"

    # Old versions of xunit.console.exe could be found under \tools\xunit.console.exe.  For the latest versions of xunit
    # the executable has been moved to \tools\net452\xunit.console.exe
    if (![System.IO.File]::Exists($runnerExecutable)) {
        $runnerExecutable = "$xunitRunnerPath\tools\net452\xunit.console.exe"
    }

	exec { & $runnerExecutable $projectName\bin\$config\$projectName.dll -xml "$artifactsPath\xunit_$projectName.xml" -html "$artifactsPath\xunit_$projectName.html" -nologo }
}

function Execute-Xunit-DotNetCli($projectName, $artifactsPath, $config) {
    $projectFile = $buildTools.GetProjectFilePath($projectName)
	$singleFramework = Get-Content $projectFile | Where { $_.Contains("<TargetFramework>") }
	$multiFrameworks = Get-Content $projectFile | Where { $_.Contains("<TargetFrameworks>") }
	$frameworks = If ($singleFramework -ne $null) { @(([xml]$singleFramework).TargetFramework) } Else { ([xml]$multiFrameworks).TargetFrameworks.Split(';') }

	$frameworks | ForEach-Object {
		$framework = $_		
		Write-Host "Executing tests for $projectName with framework $framework to $artifactsPath"		
		exec { dotnet test $projectFile --configuration $config --no-build --no-restore --framework $framework --test-adapter-path:. "--logger:xunit;LogFilePath=../$artifactsPath/xunit-$projectName-$framework.xml" --verbosity quiet }
	}
}

function Execute-VsTest($projectName){
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
	if (Test-Path $vstestrunner){
		Write-Host "Using vstest.console.exe from VS 2017 Install"
		return $vstestrunner
	}

	$vstestrunner = "C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
	if (Test-Path $vstestrunner){
		Write-Host "Using vstest.console.exe from VS 2015 Install"
		return $vstestrunner
	}

	Write-Host "No VS install detected, Using vstest.console.exe from PATH"
	return "vstest.console.exe"
}

#endregion

#region Push and Pack
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
            $version = Get-VersionFromCsProj $buildTools.GetProjectFilePath($projectName)
        }
		if (!$version){
            $version = Get-VersionFromCsProj "$projectName\..\version.props"
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

function Get-CurrentBranch{
	if ([String]::IsNullOrEmpty($env:APPVEYOR_REPO_BRANCH)){
		$branch = git branch | Where {$_ -match "^\*(.*)"} | Select-Object -First 1
	} else {
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
    if ($branch.IndexOf("/") -ne -1){
        $prefix = $branch.Substring($branch.IndexOf("/") + 1)
    } else {
        $prefix = $branch
    }

    $prefix = $prefix.Substring(0, [System.Math]::Min(10, $prefix.Length))
	return $prefix + "-" + $(Get-Date).ToString("yyMMddHHmmss") -Replace "[^a-zA-Z0-9-]", ""
}

function Pack-Project($projectName){
    use "15.0" MsBuild
    $version = Get-Version $projectName
	Write-Host "Packing $projectName $version to $artifactsPath"
    $frameworkVersion = $buildTools.GetBuildVersion($projectName);

    if ($frameworkVersion.UseMsBuild){
		Pack-MsBuild $projectName $version
    } 
	elseif ($frameworkVersion.UseDotNetCli) {
        Pack-DotNetCli $projectName $version
    }
}

function Pack-DotNetCli($project, $version) {		
    $projectFilePath = $buildTools.GetProjectFilePath($project)
	exec { dotnet pack $projectFilePath --configuration $config --no-build --no-restore --output ..\$artifactsPath -p:Version=$version }  
}

function Pack-MsBuild($project, $version){
    $projectFilePath = $buildTools.GetProjectFilePath($project)
    exec { & NuGet pack $projectFilePath -Build -Properties Configuration=$config -OutputDirectory $artifactsPath -Version $version -IncludeReferencedProjects -NonInteractive }
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
	$octopusToolsPath = $buildTools.GetPackagePath("OctopusTools")
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
	$octopusToolsPath = $buildTools.GetPackagePath("OctopusTools")
	$version = Get-Version $project
	$releaseNotes = Get-ReleaseNotes
	Write-Host "Creating release $version of $project on $env:ylp_octopusDeployServer"
	exec { & $octopusToolsPath\tools\Octo.exe create-release --server="$env:ylp_octopusDeployServer" --apiKey="$env:ylp_octopusDeployApiKey" --project="$project" --version="$version" --releaseNotes=$releaseNotes --packageVersion="$version" --ignoreexisting }
}

function Octo-DeployRelease($project){
	$octopusToolsPath = $buildTools.GetPackagePath("OctopusTools")
	$version = Get-Version $project

	$environments = $env:ylp_environment.Split(",")

	$environments | ForEach-Object{
		$environment = $_	
		Write-Host "Deploying release $version of $project on $env:ylp_octopusDeployServer to $environment"
		exec { & $octopusToolsPath\tools\Octo.exe deploy-release --server="$env:ylp_octopusDeployServer" --apiKey="$env:ylp_octopusDeployApiKey" --project="$project" --version="$version" --deployto="$environment" }
	}
}


#endregion

#region Build Tools

class BuildTools {

    #
    # Searches a number of locations on the host machine for the specified nuget package
    #
    [string] GetPackagePath($packageId) {
        return $this.GetPackagePath($packageId, $null)
    }

    #
    # Searches a number of locations on the host machine for the specified nuget package
    #
    [string] GetPackagePath($packageId, $project) {
        [BuildVersion]$package = $null;

        if($project -ne $null) {
            $framework = $this.GetBuildVersion($project);

            # If we're dealing with the .Net Framework, search for the packages.config file for installed packages
            if($framework.UseMsBuild -and (Test-Path "$project\packages.config")) {
                [xml]$packagesXml = Get-Content "$project\packages.config";
                $packageXml = $packagesXml.packages.package | Where { $_.id -eq $packageId }
                if($packageXml -ne $null) {
                    $package = New-Object BuildVersion -Property @{ Id = $packageXml.id; Version = $packageXml.version }
                }
            }
            # If we're dealing with the .Net Core Framework, search the csproj for the installed packages
            elseif($framework.UseDotNetCli -and (Test-Path "$project\$project.csproj")) {
                [xml]$packagesXml = Get-Content "$project\$project.csproj";
                $packageXml = $packagesXml.Project.ItemGroup.PackageReference | Where { $_.Include -eq $packageId }
                if($packageXml -ne $null) {
                    $package = New-Object BuildVersion -Property @{ Id = $packageXml.Include; Version = $packageXml.Version }
                }
            }
        } else {
            $package = New-Object BuildVersion -Property @{ Id = $packageId; Version = "*" }
        }

        if($package -ne $null) {
            # First, check the local package cache
            $children = Get-ChildItem "packages" -Filter $package.GetPackageFolderName();

            if($children -eq $null) {
                #Then, check the global package cache
                $children = Get-ChildItem "$env:userprofile\.nuget\packages\$($package.Id)" -Filter $package.Version
            }

            if($children -ne $null)
            {
                [System.IO.DirectoryInfo]$packageDirectory = $null;

                if($children -is [system.array]) {
                    $packageDirectory = $children[0];
                }
                else {
                    $packageDirectory = $children;
                }

                return $packageDirectory.FullName;
            }
        }
        
        if($project -ne $null) {
            throw "$packageId is required in $project but it is not installed. Please install $packageId in $project."
        }

        throw "$packageId is required but it is not installed. Please install $packageId."
    }

    [FrameworkVersion]GetBuildVersion([string]$project) {
        $projectFilePath = $this.GetProjectFilePath($project)
        $msBuildFrameworkVersion = (Select-String -pattern '<TargetFrameworkVersion>(.+)<\/TargetFrameworkVersion>' -path $projectFilePath)
        $dotNetCoreFrameworkMatch = (Select-String -pattern '<TargetFramework[s]?>(.+)<\/TargetFramework[s]?>' -path $projectFilePath)

        if($msBuildFrameworkVersion -ne $null) {
            return New-Object FrameworkVersion -Property @{ 
                Version = "net" + $msBuildFrameworkVersion.Matches[0].Groups[1].Value.Replace(".", "").Replace("v", "");
                UseMsBuild = $true;
            }
        }
        if($dotNetCoreFrameworkMatch -ne $null) {
            return New-Object FrameworkVersion -Property @{ 
                Version = $dotNetCoreFrameworkMatch.Matches[0].Groups[1].Value; 
                UseDotNetCli = $true;
            };
        }

        throw "Unable to determine version of project";
    }

    [string]GetProjectFilePath([string]$project) {
        return "$project/$project.csproj"
    }
}

class BuildVersion {
    [string]$Id
    [string]$Version
    
    [string] GetPackageFolderName() {
        return $this.Id + "." + $this.Version;
    }
}

class FrameworkVersion {
    [string]$Version = $null;
    [bool]$UseMsBuild = $false;
    [bool]$UseDotNetCli = $false;
}

#endregion