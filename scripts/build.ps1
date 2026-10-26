$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$package=Join-Path $projectRoot 'dist\Codex-Launch'
New-Item -ItemType Directory -Path $package -Force | Out-Null
& (Join-Path $PSScriptRoot 'make-icon.ps1')
$references=@('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll',(Join-Path $framework 'WPF\PresentationCore.dll'),(Join-Path $framework 'WPF\PresentationFramework.dll'),(Join-Path $framework 'WPF\WindowsBase.dll'))
$compilerArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+','/warnaserror+',('/win32manifest:'+(Join-Path $projectRoot 'src\app.manifest')),('/win32icon:'+(Join-Path $projectRoot 'assets\launcher.ico')),('/out:'+(Join-Path $package 'Codex Launch.exe')))
$compilerArgs+= $references | ForEach-Object { '/r:'+$_ }
$compilerArgs+=@('Core.cs','Native.cs','AnimationView.cs','WindowFader.cs','Program.cs') | ForEach-Object {Join-Path $projectRoot ('src\'+$_)}
& (Join-Path $framework 'csc.exe') @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Launcher compilation failed.'}
if(!(Test-Path -LiteralPath (Join-Path $package 'settings.json'))){Copy-Item -LiteralPath (Join-Path $projectRoot 'settings.json') -Destination (Join-Path $package 'settings.json')}
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\app.config') -Destination (Join-Path $package 'Codex Launch.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $package 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\launcher.ico') -Destination (Join-Path $package 'launcher.ico') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\chatgpt.ico') -Destination (Join-Path $package 'chatgpt.ico') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\Preview Animation.cmd') -Destination (Join-Path $package 'Preview Animation.cmd') -Force
Write-Output ('Built: '+(Join-Path $package 'Codex Launch.exe'))
