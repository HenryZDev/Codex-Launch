param([switch]$Desktop,[switch]$UpdateIcon)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$target=Join-Path $projectRoot 'dist\Codex-Launch\Codex Launch.exe'
if(!(Test-Path -LiteralPath $target)){throw 'Build the launcher first using scripts\build.ps1.'}
$shortcutFolder=if($Desktop){[Environment]::GetFolderPath('Desktop')}else{$projectRoot}
$shortcutPath=Join-Path $shortcutFolder 'Codex (Animated).lnk'
$exists=Test-Path -LiteralPath $shortcutPath
if($exists -and !$UpdateIcon){Write-Output ('Already exists; left unchanged: '+$shortcutPath);exit 0}
$shell=New-Object -ComObject WScript.Shell
try {
 $shortcut=$shell.CreateShortcut($shortcutPath)
 if($exists){
  if(![string]::Equals($shortcut.TargetPath,$target,[StringComparison]::OrdinalIgnoreCase)){throw 'The existing shortcut targets another application; it was left unchanged.'}
  $shortcut.IconLocation=(Join-Path (Split-Path -Parent $target) 'chatgpt.ico')+',0'
  $shortcut.Save()
  Write-Output ('Updated animated launcher icon: '+$shortcutPath)
  return
 }
 $shortcut.TargetPath=$target;$shortcut.WorkingDirectory=Split-Path -Parent $target
 $shortcut.Description='Open Codex with the Bring ideas to life launch animation.'
 $shortcut.IconLocation=(Join-Path (Split-Path -Parent $target) 'chatgpt.ico')+',0';$shortcut.Save()
 Write-Output ('Created: '+$shortcutPath)
} finally {[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
