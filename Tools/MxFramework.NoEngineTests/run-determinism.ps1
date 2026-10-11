[CmdletBinding()]
param(
    [ValidateSet('core', 'restore', 'network', 'regressions', 'allocation', 'all', 'unity-plan')]
    [string]$Suite = 'core',
    [switch]$SkipBuild,
    [string]$OutputDirectory,
    [string]$NUnitFrameworkPath,
    [string]$NewtonsoftPath
)
$ErrorActionPreference = 'Stop'
$repoPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projectPath = Join-Path $PSScriptRoot 'Determinism.Tests/Determinism.Tests.csproj'
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoPath 'TestResults/Determinism' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if ($Suite -eq 'unity-plan') {
    $plan = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Determinism.Tests/unity-integration-plan.json') -Raw
    $output = Join-Path $OutputDirectory 'unity-integration-plan.json'
    [IO.File]::WriteAllText($output, $plan)
    Write-Output "Plan=$output (export only; does not start or control Unity)"
    exit 0
}
$buildProperties = @('-p:NuGetAudit=false')
if ($NUnitFrameworkPath) { $buildProperties += "-p:NUnitFrameworkPath=$NUnitFrameworkPath" }
if ($NewtonsoftPath) { $buildProperties += "-p:NewtonsoftPath=$NewtonsoftPath" }
if (!$SkipBuild) {
    & dotnet restore $projectPath --configfile (Join-Path $PSScriptRoot 'Determinism.Tests/NuGet.Config') @buildProperties
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & dotnet build $projectPath --no-restore --disable-build-servers '-p:UseSharedCompilation=false' @buildProperties --verbosity quiet
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$assemblyPath = Join-Path $PSScriptRoot 'Determinism.Tests/bin/Debug/net9.0/Determinism.Tests.dll'
if (!(Test-Path -LiteralPath $assemblyPath)) { throw 'Build the deterministic runner before using -SkipBuild.' }
& dotnet $assemblyPath $Suite (Join-Path $OutputDirectory "determinism-$Suite.xml")
exit $LASTEXITCODE
