param([string] $gitFolder,
	  [string] $outputPath)

$output = [System.IO.Path]::GetFullPath($outputPath)
pushd $gitFolder
git bundle create $output --branches --tags 2>&1
popd
Remove-Item -Recurse -Force $gitFolder