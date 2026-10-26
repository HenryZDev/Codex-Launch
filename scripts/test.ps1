$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testDir=Join-Path $projectRoot 'work\tests'
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$testExe=Join-Path $testDir 'CoreTests.exe'
& $compiler /nologo /target:exe /platform:x64 /warnaserror+ /out:$testExe (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'tests\CoreTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed.'}
& $testExe
if($LASTEXITCODE -ne 0){throw 'Core tests failed.'}
