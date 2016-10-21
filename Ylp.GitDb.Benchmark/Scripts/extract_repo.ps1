param([string]$bundleFile,
	  [string]$targetDir)

git clone --bare $bundleFile $targetDir 2>&1 | write-host