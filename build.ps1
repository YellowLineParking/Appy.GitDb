$script:artifactsPath = (property artifactsPath $basePath\artifacts)
$script:msBuildVersion = (property msBuildVersion "14.0")
$script:config = (property config "release")
$script:myGetUrl = (property myGetUrl "")
$script:myGetApiKey = (property myGetApiKey "")
$script:octopusUrl = (property octopusUrl "")
$script:octopusApiKey = (property octopusApiKey "")
$script:gitDbLocalVersion = ""
$script:gitDbRemoteVersion = ""
$script:gitServerVersion = ""

$myGetUrl = "https://www.myget.org/F/ylpcore/auth/c933ee04-37ee-4253-aefa-18f07b182643/api/v3/index.json"
$myGetApiKey = "c933ee04-37ee-4253-aefa-18f07b182643"
$octopusUrl = "http://deploy.yellowlineparking.com"
$octopusApiKey = "API-VP77PW0EO9AZUTUCAMZMPEEMXQG"

task Clean{
	Clean-Folders @($artifactsPath, "*\bin", "*\obj")
}

task Compile{
	Convert-Project "Ylp.GitDb.Local" $config $artifactsPath
	Convert-Project "Ylp.GitDb.Remote" $config $artifactsPath
	Convert-Project "Ylp.GitDb.Server" $config $artifactsPath
	Convert-Project "Ylp.GitDb.Tests" $config $artifactsPath
}

task Test {
	Execute-Xunit "Ylp.GitDb.Tests" $config $artifactsPath
}

task Pack{
	$script:gitDbLocalVersion = Pack-Project "Ylp.GitDb.Local" $config $artifactsPath
	$script:gitDbRemoteVersion = Pack-Project "Ylp.GitDb.Remote" $config $artifactsPath
	$script:gitServerVersion = OctoPack-Project "Ylp.GitDb.Server" $artifactsPath
}

task Push{

	#Push-Package $artifactsPath\Ylp.GitDb.Local.$script:gitDbLocalVersion.nupkg $myGetUrl $myGetApiKey "409"
	#Push-Package $artifactsPath\Ylp.GitDb.Remote.$script:gitDbRemoteVersion.nupkg $myGetUrl $myGetApiKey "409"
	Write-Host "Package: $artifactsPath\Ylp.GitDb.Server.$script:gitServerVersion.nupkg"
	Write-Host "Url: $octopusUrl/nuget/packages"
	Push-Package $artifactsPath\Ylp.GitDb.Server.$script:gitServerVersion.nupkg "$octopusUrl/nuget/packages" $octopusApiKey "409"
}

task CreateRelease{
	Octo-CreateRelease "Ylp.GitDb.Server" $script:gitServerVersion $octopusUrl $octopusApiKey
}

task DeployRelease{
	Octo-DeployRelease "Ylp.GitDb.Server" $script:gitServerVersion "QA" $octopusUrl $octopusApiKey
}

task dev Clean, Compile, Test, Pack
task ci dev, Push, CreateRelease, DeployRelease