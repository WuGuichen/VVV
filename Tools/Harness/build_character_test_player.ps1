param(
    [string]$UnityExe = "",
    [string]$ProjectPath = "",
    [string]$OutputRoot = "",
    [ValidateSet("Release", "Development")]
    [string]$Configuration = "Release",
    [ValidateSet("None", "OptimizeSize", "MediumStripping")]
    [string]$Experiment = "None",
    [ValidateRange(1, 16)]
    [int]$JobWorkerCount = 2,
    [switch]$SkipResourceBuild,
    [switch]$NoRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'character_test_build_identity.ps1')

function Resolve-RepoRoot {
    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        return [System.IO.Path]::GetFullPath($ProjectPath)
    }

    $scriptRoot = Split-Path -Parent $PSCommandPath
    return [System.IO.Path]::GetFullPath((Join-Path $scriptRoot "..\.."))
}

function Resolve-UnderRoot {
    param(
        [string]$Root,
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $Root $Path))
}

function Resolve-UnityExePath {
    param(
        [string]$Root,
        [string]$ExplicitUnityExe
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitUnityExe)) {
        if (-not (Test-Path -LiteralPath $ExplicitUnityExe -PathType Leaf)) {
            throw "Unity executable was not found: $ExplicitUnityExe"
        }

        return [System.IO.Path]::GetFullPath($ExplicitUnityExe)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EXE)) {
        if (-not (Test-Path -LiteralPath $env:UNITY_EXE -PathType Leaf)) {
            throw "UNITY_EXE points to a missing file: $env:UNITY_EXE"
        }

        return [System.IO.Path]::GetFullPath($env:UNITY_EXE)
    }

    $projectVersionPath = Join-Path $Root "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $projectVersionPath -PathType Leaf)) {
        throw "ProjectVersion.txt was not found: $projectVersionPath"
    }

    $versionLine = Get-Content -LiteralPath $projectVersionPath |
        Where-Object { $_ -like "m_EditorVersion:*" } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($versionLine)) {
        throw "m_EditorVersion was not found in $projectVersionPath"
    }

    $version = ($versionLine -replace "^m_EditorVersion:\s*", "").Trim()
    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates.Add((Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"))
        $candidates.Add((Join-Path $env:ProgramFiles "Unity $version\Editor\Unity.exe"))
    }
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor\$version\Editor\Unity.exe"))
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "Unity $version was not found under Unity Hub. Pass -UnityExe or set UNITY_EXE."
}

function Read-KeyValueFile {
    param([string]$Path)

    $values = @{}
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $values
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        $separator = $line.IndexOf(":")
        if ($separator -le 0) {
            continue
        }

        $key = $line.Substring(0, $separator).Trim()
        $value = $line.Substring($separator + 1).Trim()
        if (-not [string]::IsNullOrWhiteSpace($key)) {
            if ($values.ContainsKey($key) -and (
                $key -in @('status', 'issue', 'buildConfiguration', 'scriptingBackend', 'developmentBuild', 'overviewReportStatus', 'overviewReport',
                    'timingReport', 'profilerRecorderReport',
                    'animationReport', 'processExited', 'processExitCode',
                    'acceptanceStatus', 'acceptanceIssue') -or
                $key.StartsWith('runtimeCatalogLoad.') -or
                $key.StartsWith('gameplayProbe.') -or
                $key.StartsWith('runtimeCatalogGate.'))) {
                throw "Duplicate report field: $key in $Path"
            }
            if (-not $values.ContainsKey($key)) {
                $values[$key] = $value
            }
        }
    }

    return $values
}

function Require-File {
    param(
        [string]$Path,
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }
}

$repoRoot = Resolve-RepoRoot
$unityPath = Resolve-UnityExePath -Root $repoRoot -ExplicitUnityExe $UnityExe
$outputRootFull = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $runName = "WGF-Pkg-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
    [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) $runName))
} else {
    Resolve-UnderRoot -Root $repoRoot -Path $OutputRoot
}
Assert-CharacterTestBuildPaths -ProjectRoot $repoRoot -OutputRoot $outputRootFull
$invocation = New-CharacterTestBuildInvocation -ProjectRoot $repoRoot -UnityPath $unityPath `
    -OutputRoot $outputRootFull -Configuration $Configuration `
    -SkipResourceBuild ([bool]$SkipResourceBuild) -NoRun ([bool]$NoRun)
if ($Configuration -ne 'Release' -and $Experiment -ne 'None') {
    throw 'Optimization experiments require Release; change only one experiment at a time.'
}
$invocation['experiment'] = $Experiment
$invocation['jobWorkerCount'] = $JobWorkerCount
New-Item -ItemType Directory -Force -Path $outputRootFull | Out-Null
$invocationPath = Join-Path $outputRootFull 'CharacterTestBuildInvocation.json'
$invocation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $invocationPath -Encoding UTF8

$summaryPath = Join-Path $outputRootFull "CharacterTestPackageTestSummary.txt"
$overviewPath = Join-Path $outputRootFull "CharacterTestPackageOverview.html"
$logPath = Join-Path $outputRootFull "unity_batch.log"
$executeMethod = "MxFramework.Game.CharacterTest.Editor.CharacterTestPackageTestCommand.RunDefaultPackageTest"

$unityArgs = @(
    "-batchmode",
    "-quit",
    "-job-worker-count",
    [string]$JobWorkerCount,
    "-buildTarget",
    "win64",
    "-projectPath",
    $repoRoot,
    "-executeMethod",
    $executeMethod,
    "-logFile",
    $logPath,
    "-character-test-package-test-output",
    $outputRootFull,
    "-character-test-package-test-configuration",
    $Configuration
)
if ($Experiment -ne 'None') {
    $unityArgs += @('-character-test-package-test-experiment', $Experiment)
}

if ($SkipResourceBuild) {
    $unityArgs += "-character-test-package-test-skip-resource-build"
}
if ($NoRun) {
    $unityArgs += "-character-test-package-test-no-run"
}

Write-Host "Unity: $unityPath"
Write-Host "Unity version: $($invocation.executableProductVersion) (matches project pin)"
Write-Host "Build identity: $invocationPath"
Write-Host "Project: $repoRoot"
Write-Host "Output: $outputRootFull"
Write-Host "Configuration: $Configuration / IL2CPP / Windows x64"
Write-Host "Log: $logPath"

$quotedUnityArgs = ($unityArgs | ForEach-Object {
    if ($_ -match '"') {
        throw "Unity argument contains an unsupported quote character: $_"
    }
    '"' + $_ + '"'
}) -join ' '
$unityProcess = Start-Process -FilePath $unityPath -ArgumentList $quotedUnityArgs `
    -PassThru -WindowStyle Hidden
$unityProcess.PriorityClass = 'BelowNormal'
$unityProcess.WaitForExit()
$unityExitCode = [int]$unityProcess.ExitCode
Assert-CharacterTestUnityLogVersion -LogPath $logPath `
    -Expected (Read-CharacterTestProjectVersion -ProjectRoot $repoRoot)
if ($unityExitCode -ne 0) {
    if (Test-Path -LiteralPath $overviewPath -PathType Leaf) {
        Write-Host "Overview: $overviewPath"
    }
    Write-Error "Unity package test failed with exit code $unityExitCode. Log: $logPath"
    exit $unityExitCode
}

Require-File -Path $summaryPath -Description "Package test summary"
$summary = Read-KeyValueFile -Path $summaryPath
$status = [string]$summary["status"]
if ($status -ne "Passed") {
    if (Test-Path -LiteralPath $overviewPath -PathType Leaf) {
        Write-Host "Overview: $overviewPath"
    }
    $issue = [string]$summary["issue"]
    if ([string]::IsNullOrWhiteSpace($issue)) {
        $issue = "summary status was $status"
    }

    Write-Error "CharacterTest package test did not pass: $issue"
    exit 1
}

if ([string]$summary["overviewReportStatus"] -ne "Succeeded" -or
    [string]$summary["overviewReport"] -ne $overviewPath) {
    Write-Error "Package overview report status or path is invalid. Summary: $summaryPath"
    exit 1
}
Require-File -Path $overviewPath -Description "Package overview report"

if ([string]$summary["buildTarget"] -ne "StandaloneWindows64" -or
    [string]$summary["buildConfiguration"] -ne $Configuration -or
    [string]$summary["scriptingBackend"] -ne "IL2CPP" -or
    [string]$summary["playerBuildResult"] -ne "Succeeded") {
    Write-Error "Package summary target or Player build result contradicts Passed."
    exit 1
}

if (-not $NoRun) {
    if ([string]$summary["acceptanceMode"] -ne "BuildAndRun" -or
        [string]$summary["playerRunStatus"] -ne "finished") {
        Write-Error "Package summary does not contain a completed Player run."
        exit 1
    }

    $editorRunReportPath = [string]$summary["editorRunReport"]
    $animationReportPath = [string]$summary["animationReport"]
    Require-File -Path $editorRunReportPath -Description "Editor build-run report"
    Require-File -Path $animationReportPath -Description "Animation report"
    $editorRun = Read-KeyValueFile -Path $editorRunReportPath
    $animation = Read-KeyValueFile -Path $animationReportPath
    if ([string]$editorRun["status"] -ne "finished" -or
        [string]$editorRun["processExited"] -ne "true" -or
        [string]$editorRun["processExitCode"] -ne "0" -or
        [string]$editorRun["runtimeCatalogGate.allExpectedBinary"] -ne "true" -or
        [string]$animation["status"] -ne "ready") {
        Write-Error "Editor process, Binary catalog gate or animation report contradicts Passed."
        exit 1
    }

    $playerRunReport = [string]$summary["playerRunReport"]
    $timingReport = [string]$summary["timingReport"]
    $profilerRecorderReport = [string]$summary["profilerRecorderReport"]
    Require-File -Path $playerRunReport -Description "Player run report"
    Require-File -Path $timingReport -Description "Timing report"
    Require-File -Path $profilerRecorderReport -Description "ProfilerRecorder report"

    $playerRun = Read-KeyValueFile -Path $playerRunReport
    $playerRunStatus = [string]$playerRun["status"]
    $expectedDevelopment = if ($Configuration -eq "Development") { "true" } else { "false" }
    if ($playerRunStatus -ne "finished" -or [string]$playerRun["gameplayProbe.status"] -ne "Passed" -or
        [string]$playerRun["scriptingBackend"] -ne "IL2CPP" -or
        [string]$playerRun["developmentBuild"] -ne $expectedDevelopment) {
        Write-Error "Player run report status was not finished: $playerRunStatus"
        exit 1
    }
} elseif ([string]$summary["acceptanceMode"] -ne "BuildOnly" -or
        [string]$summary["playerRunStatus"] -ne "Skipped") {
    Write-Error "NoRun summary does not describe a build-only result."
    exit 1
}

Write-Host "CharacterTest package test passed."
Write-Host "Summary: $summaryPath"
Write-Host "Overview: $overviewPath"
exit 0
