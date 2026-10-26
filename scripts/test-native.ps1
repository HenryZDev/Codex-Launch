$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testDir=Join-Path $projectRoot 'work\tests'
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$references=@('System.dll','System.Core.dll','System.Xaml.dll',(Join-Path $framework 'WPF\PresentationCore.dll'),(Join-Path $framework 'WPF\PresentationFramework.dll'),(Join-Path $framework 'WPF\WindowsBase.dll'))
$compilerArgs=@('/nologo','/target:exe','/platform:x64','/warnaserror+',('/out:'+(Join-Path $testDir 'NativeTests.exe')))
$compilerArgs+= $references | ForEach-Object { '/r:'+$_ }
$compilerArgs+=@((Join-Path $projectRoot 'src\Core.cs'),(Join-Path $projectRoot 'src\Native.cs'),(Join-Path $projectRoot 'src\WindowFader.cs'),(Join-Path $projectRoot 'tests\NativeTests.cs'))
& (Join-Path $framework 'csc.exe') @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Native test compilation failed.'}
& (Join-Path $testDir 'NativeTests.exe')
if($LASTEXITCODE -ne 0){throw 'Native checks failed.'}
