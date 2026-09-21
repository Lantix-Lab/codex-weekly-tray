param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'src\CodexWeeklyTray'
$outputRoot = Join-Path $projectRoot 'dist'
$objectRoot = Join-Path $projectRoot 'obj'

& (Join-Path $projectRoot 'verify-ascii.ps1')

New-Item -ItemType Directory -Path $outputRoot, $objectRoot -Force | Out-Null

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'Windows .NET Framework C# compiler csc.exe was not found.'
}

$references = @(
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Web.Extensions.dll'
)

$applicationSources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | ForEach-Object { $_.FullName }
$applicationOutput = Join-Path $outputRoot 'CodexWeeklyTray.exe'

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu "/out:$applicationOutput" $references $applicationSources
if ($LASTEXITCODE -ne 0) {
    throw "Application compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built $applicationOutput"

if (-not $SkipTests) {
    & (Join-Path $projectRoot 'test.ps1') -SkipBuild
}
