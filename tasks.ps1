$script:config = "Release"

task Clean{
	Clean-Folder $artifactsPath
	Clean-Folder TestResults
	Get-ProjectsForTask "Clean" | ForEach-Object {
		Clean-Folder "$($_.Name)\bin"
	}
	New-Item $artifactsPath -Type directory -Force | Out-Null
}

task Compile {
	Get-ProjectsForTask "Compile" | ForEach-Object {
		Compile-Project $_.Name
	}

	Get-ProjectsForTask "Compile" | 
		Where { $_.Type -eq "EmbeddedWebJob" -or $_.Type -eq "StandaloneWebJob"} |
		ForEach-Object {
			Move-WebJob $_.Name $_.Config["Target"]  $_.Config["RunMode"]
		}
}

task Test{
	Get-ProjectsForTask "Test" | 
		Where { $_.Type -eq "VsTest"} |
		ForEach-Object {
			Execute-VsTest $_.Name
		}

	Get-ProjectsForTask "Test" | 
		Where { $_.Type -eq "XUnit"} |
		ForEach-Object {
			Execute-XUnit $_.Name
		}

	Get-ProjectsForTask "Test" | 
		Where { $_.Type -eq "VsTestAndXUnit"} |
		ForEach-Object {
			Execute-VsTest $_.Name
			Execute-XUnit $_.Name
		}

}
task Pack{
	Get-ProjectsForTask "Pack" | ForEach-Object {
		if($_.Type -eq "Package"){
			Pack-Project $_.Name			
		} else{
			OctoPack-Project $_.Name
		}
	}
}

task Push{
	Get-ProjectsForTask "Push" | 
		Where { $_.Type -eq "Package" -Or $_.Type -eq "AppPackage"} |
		ForEach-Object{ 
			Push-Package $_.Name $env:ylp_nugetPackageSource $env:ylp_nugetPackageSourceApiKey "409"
		}

	Get-ProjectsForTask "Push" | 
		Where { $_.Type -ne "Package"} |
		ForEach-Object{ 
			Push-Package $_.Name $env:ylp_octopusDeployPackageSource $env:ylp_octopusDeployApiKey "409"
		}
}

task Release{
	Get-ProjectsForTask "Release" | ForEach-Object {
		Octo-CreateRelease $_.Name
	}
}

task Deploy {
	$branch = Get-CurrentBranch
	$isGeneralRelease = Is-GeneralRelease $branch
	$isLocalBuild = Is-LocalBuild
	if($isGeneralRelease -or $isLocalBuild){
		Get-ProjectsForTask "Deploy" | ForEach-Object {
			Octo-DeployRelease $_.Name
		}	
	} else{
		"Skipped deployment: this is a prerelease version"
	}
}

task dev Clean, Compile, Test, Pack
task ci dev, Push, Release, Deploy