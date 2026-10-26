param([switch]$SkipShortcut)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1')
if(!$SkipShortcut){& (Join-Path $PSScriptRoot 'create-shortcut.ps1')}
$package=Join-Path $projectRoot 'dist\Codex-Launch'
$archive=Join-Path $projectRoot 'dist\Codex-Launch-win-x64.zip'
$files=@('Codex Launch.exe','Codex Launch.exe.config','settings.json','chatgpt.ico','README.md','Preview Animation.cmd') | ForEach-Object {Join-Path $package $_}
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
$hash=Get-FileHash -LiteralPath $archive -Algorithm SHA256
Set-Content -LiteralPath ($archive+'.sha256') -Value ($hash.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($archive)) -Encoding ascii
Write-Output ('Packaged: '+$archive)
