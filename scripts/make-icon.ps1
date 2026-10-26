param([string]$IconPath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$iconDirectory=Join-Path $projectRoot 'assets'
$localIcon=Join-Path $iconDirectory 'chatgpt.ico'
New-Item -ItemType Directory -Path $iconDirectory -Force | Out-Null
if($IconPath){
 if(!(Test-Path -LiteralPath $IconPath -PathType Leaf)){throw 'The supplied icon file does not exist.'}
 $resolvedIcon=(Resolve-Path -LiteralPath $IconPath).Path
 if(![string]::Equals($resolvedIcon,$localIcon,[StringComparison]::OrdinalIgnoreCase)){
  Copy-Item -LiteralPath $resolvedIcon -Destination $localIcon -Force
 }
}
if(!(Test-Path -LiteralPath $localIcon)){
 $settings=Get-Content -LiteralPath (Join-Path $projectRoot 'settings.json') -Raw | ConvertFrom-Json
 $packageName=$settings.ApplicationId.Split('_')[0]
 $package=Get-AppxPackage -Name $packageName | Sort-Object Version -Descending | Select-Object -First 1
 if(!$package){throw 'Codex is not installed for this Windows user. Install it or supply an icon with make-icon.ps1 -IconPath.'}
 $installedIcon=Join-Path $package.InstallLocation 'app\resources\chatgpt-app-dark.ico'
 if(!(Test-Path -LiteralPath $installedIcon -PathType Leaf)){throw 'The installed app icon was not found. Supply an icon with make-icon.ps1 -IconPath.'}
 Copy-Item -LiteralPath $installedIcon -Destination $localIcon
}
Copy-Item -LiteralPath $localIcon -Destination (Join-Path $iconDirectory 'launcher.ico') -Force
