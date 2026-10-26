$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testDir=Join-Path $projectRoot 'work\tests'
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$references=@('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll',(Join-Path $framework 'WPF\PresentationCore.dll'),(Join-Path $framework 'WPF\PresentationFramework.dll'),(Join-Path $framework 'WPF\WindowsBase.dll'))
$compilerArgs=@('/nologo','/target:exe','/platform:x64','/warnaserror+','/main:HostTests',('/out:'+(Join-Path $testDir 'HostTests.exe')))
$compilerArgs+=$references | ForEach-Object {'/r:'+$_}
$compilerArgs+=@('Core.cs','Native.cs','AnimationView.cs','WindowFader.cs','Program.cs') | ForEach-Object {Join-Path $projectRoot ('src\'+$_)}
$compilerArgs+=(Join-Path $projectRoot 'tests\HostTests.cs')
& (Join-Path $framework 'csc.exe') @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Host test compilation failed.'}
& (Join-Path $testDir 'HostTests.exe')
if($LASTEXITCODE -ne 0){throw 'Host checks failed.'}
