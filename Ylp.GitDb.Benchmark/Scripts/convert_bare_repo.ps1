param([string]$gitFolder)

pushd $gitFolder

#move all folder into a .git subdirectory
mkdir .git
Get-ChildItem -Exclude '.git' -Recurse | Move-Item -Destination {Join-Path .git $_.Name}

#initialize the repo
git init

#set the HEAD to the first commit so we don't have a dirty working directory
$firstCommitHash = git rev-list --max-parents=0 HEAD
git checkout --detach $firstCommitHash

popd
