param(
    [switch]$Live,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'src\CodexWeeklyTray'
$objectRoot = Join-Path $projectRoot 'obj'

& (Join-Path $projectRoot 'verify-ascii.ps1')

if (-not $SkipBuild) {
    & (Join-Path $projectRoot 'build.ps1') -SkipTests
}

New-Item -ItemType Directory -Path $objectRoot -Force | Out-Null

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'Windows .NET Framework C# compiler csc.exe was not found.'
}

$testOutput = Join-Path $objectRoot 'CodexWeeklyTray.Tests.exe'
$testSources = @(
    (Join-Path $projectRoot 'tests\SmokeTests.cs'),
    (Join-Path $sourceRoot 'UsageSnapshot.cs'),
    (Join-Path $sourceRoot 'UsageWindowSet.cs'),
    (Join-Path $sourceRoot 'UsageParser.cs'),
    (Join-Path $sourceRoot 'RefreshPolicy.cs'),
    (Join-Path $sourceRoot 'TrayIconRenderer.cs'),
    (Join-Path $sourceRoot 'CodexLocator.cs'),
    (Join-Path $sourceRoot 'CodexAppServerClient.cs')
)
$references = @(
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Web.Extensions.dll'
)

& $compiler /nologo /target:exe /optimize+ /platform:anycpu "/out:$testOutput" $references $testSources
if ($LASTEXITCODE -ne 0) {
    throw "Test program compilation failed with exit code $LASTEXITCODE."
}

$arguments = @()
if ($Live) {
    $arguments += '--live'
}

& $testOutput $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}
