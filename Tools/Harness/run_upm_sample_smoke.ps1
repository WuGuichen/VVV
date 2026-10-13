[CmdletBinding()]
param(
    [string]$UnityPath,
    [ValidateSet('Local', 'PinnedGit')]
    [string]$PackageSource = 'Local',
    [string]$ConsumerRoot = 'C:\WGameFrameworkSampleSmokeRuns',
    [string]$GitUrl,
    [string]$Revision
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$baseRunner = Join-Path $PSScriptRoot 'run_upm_consumer_smoke.ps1'
$modeFolder = $PackageSource
$project = Join-Path $ConsumerRoot "$modeFolder\base\UnityProject"
$resultRoot = Join-Path $ConsumerRoot "$modeFolder\base"
$sampleResults = Join-Path $resultRoot 'sample-results.xml'
$importLog = Join-Path $resultRoot 'sample-import.log'
$testLog = Join-Path $resultRoot 'sample-test.log'
$reportPath = Join-Path $resultRoot 'sample-report.json'
$sampleFilter = 'WGameFramework.UpmSampleSmoke'
$sampleFullName = 'WGameFramework.UpmSampleSmoke.RuntimeHostSampleConsumerSmokeTests.ImportedSample_RunsAndCleansRuntimeHostLifecycle'

function Resolve-UnityEditor {
    if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
        return (Resolve-Path -LiteralPath $UnityPath).Path
    }

    $versionLine = Get-Content -LiteralPath (Join-Path $repositoryRoot 'ProjectSettings\ProjectVersion.txt') |
        Where-Object { $_ -like 'm_EditorVersion: *' } |
        Select-Object -First 1
    $version = $versionLine.Substring('m_EditorVersion: '.Length).Trim()
    $candidate = "C:\Program Files\Unity $version\Editor\Unity.exe"
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Unity Editor was not found: $candidate"
    }
    return $candidate
}

function Get-TreeSnapshot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$ExcludePrefix = ''
    )

    $root = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $prefix = $root + [System.IO.Path]::DirectorySeparatorChar
    $snapshot = New-Object 'System.Collections.Generic.Dictionary[string,string]' (
        [System.StringComparer]::Ordinal)
    Get-ChildItem -LiteralPath $root -Recurse -File -Force | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($prefix.Length).Replace('\', '/')
        if (-not [string]::IsNullOrEmpty($ExcludePrefix)) {
            $excludedRootMeta = $ExcludePrefix.TrimEnd('/') + '.meta'
            if ($relative.StartsWith($ExcludePrefix, [System.StringComparison]::Ordinal) -or
                [string]::Equals($relative, $excludedRootMeta, [System.StringComparison]::Ordinal)) {
                return
            }
        }
        $snapshot.Add(
            $relative,
            (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())
    }
    return ,$snapshot
}

function Assert-SnapshotEqual {
    param(
        [Parameter(Mandatory = $true)]$Before,
        [Parameter(Mandatory = $true)]$After,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $paths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    foreach ($path in $Before.Keys) { $null = $paths.Add($path) }
    foreach ($path in $After.Keys) { $null = $paths.Add($path) }
    $changes = @($paths | Where-Object {
        $beforeHash = if ($Before.ContainsKey($_)) { $Before[$_] } else { $null }
        $afterHash = if ($After.ContainsKey($_)) { $After[$_] } else { $null }
        -not [string]::Equals($beforeHash, $afterHash, [System.StringComparison]::Ordinal)
    } | Sort-Object)
    if ($changes.Count -gt 0) {
        throw "$Label changed outside the explicit sample import ($($changes.Count) file(s)): $(@($changes | Select-Object -First 8) -join ', ')"
    }
}

function Invoke-Unity {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $process = Start-Process -FilePath $script:unityEditor -ArgumentList $Arguments `
        -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "$Label exited with code $($process.ExitCode)."
    }
}

function Assert-LogClean {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Unity log is missing: $Path"
    }
    $text = Get-Content -Raw -LiteralPath $Path
    foreach ($pattern in @('error CS\d+', 'Scripts have compiler errors', 'Unhandled log message', 'Test run completed\. Exiting with code 2')) {
        if ($text -match $pattern) {
            throw "Unity sample smoke log contains '$pattern': $Path"
        }
    }
}

$script:unityEditor = Resolve-UnityEditor
$baseArguments = @{
    UnityPath = $script:unityEditor
    PackageSource = $PackageSource
    Profile = 'base'
    ConsumerRoot = $ConsumerRoot
}
if ($PackageSource -eq 'PinnedGit') {
    if ([string]::IsNullOrWhiteSpace($GitUrl) -or [string]::IsNullOrWhiteSpace($Revision)) {
        throw 'PinnedGit sample smoke requires -GitUrl and -Revision.'
    }
    $baseArguments.GitUrl = $GitUrl
    $baseArguments.Revision = $Revision
}

& $baseRunner @baseArguments

$packageRoot = if ($PackageSource -eq 'Local') {
    (Resolve-Path (Join-Path $repositoryRoot 'Packages\com.vincent.wgameframework')).Path
} else {
    $matches = @(Get-ChildItem -LiteralPath (Join-Path $project 'Library\PackageCache') -Directory |
        Where-Object { $_.Name -like 'com.vincent.wgameframework@*' })
    if ($matches.Count -ne 1) {
        throw "Expected one resolved MxFramework package cache directory, found $($matches.Count)."
    }
    $matches[0].FullName
}

$packageBefore = Get-TreeSnapshot -Path $packageRoot
$assetsBefore = Get-TreeSnapshot -Path (Join-Path $project 'Assets') -ExcludePrefix 'Samples/'
$settingsBefore = Get-TreeSnapshot -Path (Join-Path $project 'ProjectSettings')

Invoke-Unity -Label 'Package sample import' -Arguments @(
    '-batchmode', '-nographics',
    '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'WGameFramework.UpmSampleSmoke.RuntimeHostSampleImporter.Import',
    '-quit',
    '-logFile', ('"' + $importLog + '"')
)
Assert-LogClean -Path $importLog

Invoke-Unity -Label 'Imported package sample test' -Arguments @(
    '-batchmode', '-nographics',
    '-projectPath', ('"' + $project + '"'),
    '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', $sampleFilter,
    '-testResults', ('"' + $sampleResults + '"'),
    '-logFile', ('"' + $testLog + '"')
)
Assert-LogClean -Path $testLog

[xml]$results = Get-Content -Raw -LiteralPath $sampleResults
$testRun = $results.'test-run'
if ([int]$testRun.total -ne 1 -or [int]$testRun.passed -ne 1 -or [int]$testRun.failed -ne 0) {
    throw "Imported sample smoke did not pass 1/1: $sampleResults"
}
$test = $results.SelectSingleNode("//test-case[@fullname='$sampleFullName']")
if ($null -eq $test -or $test.result -ne 'Passed') {
    throw "Expected imported sample test did not pass: $sampleFullName"
}

$sampleScenes = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets\Samples') -Recurse -File -Filter 'RuntimeHostMinimalLifecycle.unity')
if ($sampleScenes.Count -ne 1) {
    throw "Expected one imported RuntimeHost sample scene, found $($sampleScenes.Count)."
}

Assert-SnapshotEqual $packageBefore (Get-TreeSnapshot -Path $packageRoot) 'Resolved package tree'
Assert-SnapshotEqual $assetsBefore (Get-TreeSnapshot -Path (Join-Path $project 'Assets') -ExcludePrefix 'Samples/') 'Consumer Assets outside Assets/Samples'
Assert-SnapshotEqual $settingsBefore (Get-TreeSnapshot -Path (Join-Path $project 'ProjectSettings')) 'Consumer ProjectSettings'

$report = [ordered]@{
    schemaVersion = 1
    sourceMode = $PackageSource.ToLowerInvariant()
    packageRevision = if ($PackageSource -eq 'PinnedGit') { $Revision } else { $null }
    sampleDisplayName = 'Runtime Host Minimal Lifecycle'
    importedScene = $sampleScenes[0].FullName.Substring($project.Length + 1).Replace('\', '/')
    testFullName = $sampleFullName
    tests = [ordered]@{ total = 1; passed = 1; failed = 0 }
    sampleResultsSha256 = (Get-FileHash -LiteralPath $sampleResults -Algorithm SHA256).Hash.ToLowerInvariant()
    guards = [ordered]@{
        packageTreeUnchanged = $true
        assetsOutsideSamplesUnchanged = $true
        projectSettingsUnchanged = $true
    }
}
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding utf8NoBOM
Write-Output 'UPM imported sample smoke passed.'
Write-Output "Report: $reportPath"
