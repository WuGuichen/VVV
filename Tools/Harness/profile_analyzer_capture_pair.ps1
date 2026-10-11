[CmdletBinding()]
param(
    [string]$BaselineRef = "origin/main",
    [string]$BaselineWorktree = "",
    [string]$AfterWorktree = "",
    [string]$UnityExe = "",
    [string]$OutputDir = "",
    [string]$CaptureExecuteMethod = "MxFramework.Tests.Game.CharacterTest.CharacterTestProfileAnalyzerCommandLineCapture.RunPairCapture",
    [int]$ExcludeFrame = 200,
    [int]$StressTargetCount = 0,
    [string]$BaselinePData = "",
    [string]$AfterPData = "",
    [switch]$SkipBaselineCapture,
    [switch]$SkipAfterCapture,
    [switch]$StressScenario,
    [switch]$BatchMode,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$WorkingDirectory = ""
    )

    $commandLine = "$FilePath " + ($Arguments -join " ")
    if ($WorkingDirectory) {
        Write-Host "[$WorkingDirectory] $commandLine"
    }
    else {
        Write-Host $commandLine
    }

    if ($DryRun) {
        return
    }

    if ($WorkingDirectory) {
        Push-Location -LiteralPath $WorkingDirectory
    }

    try {
        if ([System.IO.Path]::GetFileName($FilePath).Equals("Unity.exe", [System.StringComparison]::OrdinalIgnoreCase)) {
            $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -Wait -PassThru
            if ($process.ExitCode -ne 0) {
                throw "Command failed with exit code $($process.ExitCode)`: $commandLine"
            }
        }
        else {
            & $FilePath @Arguments
            if ($LASTEXITCODE -ne 0) {
                throw "Command failed with exit code $LASTEXITCODE`: $commandLine"
            }
        }
    }
    finally {
        if ($WorkingDirectory) {
            Pop-Location
        }
    }
}

function Resolve-UnityExecutable {
    param([string]$ProjectRoot)

    if ($UnityExe) {
        return (Resolve-Path -LiteralPath $UnityExe).Path
    }

    $versionFile = Join-Path $ProjectRoot "ProjectSettings/ProjectVersion.txt"
    $versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match "^m_EditorVersion:\s*(\S+)" } | Select-Object -First 1
    if (-not $versionLine -or $versionLine -notmatch "^m_EditorVersion:\s*(\S+)") {
        throw "Could not read Unity editor version from $versionFile"
    }

    $version = $Matches[1]
    $candidates = @(
        (Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$version/Editor/Unity.exe"),
        (Join-Path ${env:ProgramFiles} "Unity $version/Editor/Unity.exe"),
        (Join-Path ${env:ProgramFiles} "Unity/$version/Editor/Unity.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Unity executable not found for $version. Pass -UnityExe explicitly."
}

function Get-RepoRoot {
    $root = (& git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $root) {
        throw "Unable to resolve repository root."
    }

    return $root
}

function Ensure-CleanWorktree {
    param([string]$Path)

    if ($DryRun -and -not (Test-Path -LiteralPath $Path)) {
        Write-Host "DryRun: skipping clean-worktree check for missing path $Path"
        return
    }

    $status = & git -C $Path status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read git status for $Path"
    }

    if ($status) {
        throw "Worktree is not clean: $Path"
    }
}

function Test-BaselineHarnessApplied {
    param([string]$Path)

    $testPath = Join-Path $Path "Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestFireballPlayModeTests.cs"
    if (-not (Test-Path -LiteralPath $testPath)) {
        return $false
    }

    $content = Get-Content -LiteralPath $testPath -Raw
    if ($content -notmatch "CharacterTestScene_PlayModeTornadoProfileAnalyzerPairCapture_WritesPDataIntoTemp") {
        return $false
    }

    if ($content -notmatch "CharacterTestProfileAnalyzerCommandLineCapture") {
        return $false
    }

    if ($content -notmatch "AppendTornadoProfileAnalyzerEvidence") {
        return $false
    }

    if ($content -notmatch "CreateTornadoProfileAnalyzerStressPairCapture") {
        return $false
    }

    if ($content -notmatch "RunStressPairCapture") {
        return $false
    }

    if ($content -notmatch "Tornado stress Profile Analyzer capture should include non-zero TornadoFieldQuery samples") {
        return $false
    }

    $changed = & git -C $Path diff --name-only
    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    $allowedPaths = @(
        "Assets/Materials/MxFramework/CharacterTest/CharacterTestCourseLabel.mat",
        "Assets/Materials/MxFramework/CharacterTest/CharacterTestSurfaceProbe_BouncePad.mat",
        "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/preview_materials/CharacterPreview_148C8C8C.mat",
        "Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestInteractionPhysicsSystem.cs",
        "Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestFireballPlayModeTests.cs",
        "Assets/UniversalRenderPipelineGlobalSettings.asset"
    )
    foreach ($pathLine in $changed) {
        if ($pathLine -and -not ($allowedPaths -contains $pathLine)) {
            return $false
        }
    }

    return $true
}

function Test-BaselineMarkerInstrumentationApplied {
    param([string]$Path)

    $sourcePath = Join-Path $Path "Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestInteractionPhysicsSystem.cs"
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        return $false
    }

    $content = Get-Content -LiteralPath $sourcePath -Raw
    if ($content -notmatch "ProfileAnalyzerBaselineTornadoFieldQueryMarker") {
        return $false
    }

    if ($content -notmatch "CharacterTest.InteractionPhysics.TornadoFieldQuery") {
        return $false
    }

    if ($content -notmatch "ShouldProfileTornadoCapsuleQuery") {
        return $false
    }

    return $true
}

function Ensure-BaselineWorktree {
    param(
        [string]$RepoRoot,
        [string]$Path,
        [string]$Ref
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Invoke-Checked git @("-C", $RepoRoot, "-c", "core.longpaths=true", "worktree", "add", "--detach", $Path, $Ref)
        return
    }

    $gitDir = & git -C $Path rev-parse --git-dir
    if ($LASTEXITCODE -ne 0 -or -not $gitDir) {
        throw "Baseline path exists but is not a git worktree: $Path"
    }

    if ((Test-BaselineHarnessApplied $Path) -and (Test-BaselineMarkerInstrumentationApplied $Path)) {
        Write-Host "Baseline capture patches already applied in $Path"
        return
    }

    $status = & git -C $Path status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read git status for $Path"
    }

    if ($status) {
        throw "Baseline worktree has stale or unrelated changes: $Path. Pass -BaselineWorktree to a clean path for this harness revision, or refresh the dedicated baseline worktree manually."
    }

    Ensure-CleanWorktree $Path
    Invoke-Checked git @("-C", $Path, "-c", "core.longpaths=true", "checkout", "--detach", $Ref)
}

function Write-BaselineHarnessPatch {
    param(
        [string]$RepoRoot,
        [string]$Ref,
        [string]$PatchPath
    )

    $testPath = "Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestFireballPlayModeTests.cs"
    Write-Host "Writing baseline harness patch: $PatchPath"
    if ($DryRun) {
        Write-Host "git -C $RepoRoot diff --binary --output=$PatchPath $Ref -- $testPath"
        return
    }

    $patchParent = Split-Path -Parent $PatchPath
    New-Item -ItemType Directory -Force -Path $patchParent | Out-Null
    & git -C $RepoRoot diff --binary "--output=$PatchPath" $Ref -- $testPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to write baseline harness patch."
    }

    if ((Get-Item -LiteralPath $PatchPath).Length -le 0) {
        throw "Baseline harness patch is empty."
    }
}

function Apply-BaselineHarnessPatch {
    param(
        [string]$BaselinePath,
        [string]$PatchPath
    )

    if (Test-BaselineHarnessApplied $BaselinePath) {
        Write-Host "Baseline harness patch already applied; skipping git apply."
        return
    }

    Ensure-CleanWorktree $BaselinePath
    Invoke-Checked git @("-C", $BaselinePath, "apply", "--check", $PatchPath)
    Invoke-Checked git @("-C", $BaselinePath, "apply", $PatchPath)
}

function Apply-BaselineMarkerInstrumentationPatch {
    param(
        [string]$BaselinePath,
        [string]$PatchPath
    )

    if (Test-BaselineMarkerInstrumentationApplied $BaselinePath) {
        Write-Host "Baseline Tornado marker patch already applied; skipping git apply."
        return
    }

    if (-not (Test-Path -LiteralPath $PatchPath)) {
        throw "Baseline Tornado marker patch not found: $PatchPath"
    }

    Invoke-Checked git @("-C", $BaselinePath, "apply", "--check", $PatchPath)
    Invoke-Checked git @("-C", $BaselinePath, "apply", $PatchPath)
}

function Invoke-UnityCapture {
    param(
        [string]$ProjectRoot,
        [string]$Label,
        [string]$UnityPath,
        [string]$OutDir
    )

    $logFile = Join-Path $OutDir "$Label.Unity.log"
    $targetPData = Join-Path $OutDir "$Label.pdata"
    $targetSummary = Join-Path $OutDir "$Label.capture_summary.txt"
    $arguments = @(
        "-projectPath", $ProjectRoot,
        "-executeMethod", $CaptureExecuteMethod,
        "-mxProfileAnalyzerPData", $targetPData,
        "-mxProfileAnalyzerSummary", $targetSummary,
        "-logFile", $logFile
    )
    if ($StressScenario -and $StressTargetCount -gt 0) {
        $arguments += @("-mxProfileAnalyzerStressTargets", $StressTargetCount.ToString())
    }

    if ($BatchMode) {
        $arguments = @("-batchmode") + $arguments
    }

    Invoke-Checked $UnityPath $arguments

    if (-not $DryRun) {
        if (-not (Test-Path -LiteralPath $targetPData)) {
            throw "Unity capture command completed but did not create $targetPData. Inspect $logFile."
        }

        if (-not (Test-Path -LiteralPath $targetSummary)) {
            throw "Unity capture command completed but did not create $targetSummary. Inspect $logFile."
        }
    }

    return $targetPData
}

function Resolve-CaptureArtifact {
    param(
        [string]$ExplicitPath,
        [string]$ProjectRoot,
        [string]$FallbackRelativePath,
        [string]$TargetPath
    )

    if ($ExplicitPath) {
        $source = (Resolve-Path -LiteralPath $ExplicitPath).Path
    }
    else {
        $source = Join-Path $ProjectRoot $FallbackRelativePath
    }

    if (-not $DryRun) {
        if (-not (Test-Path -LiteralPath $source)) {
            throw "Capture artifact not found: $source"
        }

        $resolvedTarget = [System.IO.Path]::GetFullPath($TargetPath)
        if (-not [string]::Equals($source, $resolvedTarget, [System.StringComparison]::OrdinalIgnoreCase)) {
            Copy-Item -LiteralPath $source -Destination $TargetPath -Force
        }
    }

    return $TargetPath
}

function Export-PData {
    param(
        [string]$RepoRoot,
        [string]$PDataPath,
        [string]$Label,
        [string]$OutDir
    )

    $frameTime = Join-Path $OutDir "$Label.frameTime.csv"
    $markerTable = Join-Path $OutDir "$Label.gcMarkerTable.csv"
    $markerTiming = Join-Path $OutDir "$Label.markerTiming.csv"
    Invoke-Checked python @(
        (Join-Path $RepoRoot "Tools/Harness/profile_analyzer_gc_summary.py"),
        "--pdata", $PDataPath,
        "--pdata-thread", "1:Main Thread",
        "--frame-time-out", $frameTime,
        "--marker-table-out", $markerTable,
        "--marker-timing-out", $markerTiming,
        "--exclude-frame", $ExcludeFrame.ToString(),
        "--top-markers", "25",
        "--top-time-markers", "25",
        "--exclude-editor-noise"
    ) | Out-Host

    return $markerTiming
}

function Compare-Markers {
    param(
        [string]$RepoRoot,
        [string]$BeforeTiming,
        [string]$AfterTiming,
        [string]$OutDir
    )

    Invoke-Checked python @(
        (Join-Path $RepoRoot "Tools/Harness/profile_analyzer_compare_markers.py"),
        "--before", $BeforeTiming,
        "--after", $AfterTiming,
        "--before-label", "fresh_before",
        "--after-label", "fresh_after",
        "--out-csv", (Join-Path $OutDir "fresh_before_after_marker_comparison.csv"),
        "--out-md", (Join-Path $OutDir "fresh_before_after_marker_comparison.md"),
        "--note", "Capture-chain audit with exclude frame $ExcludeFrame; stressScenario=$StressScenario stressTargetCount=$StressTargetCount; rows with before and after values are comparable, while rows named in validity notes have no formal delta."
    )
}

$repoRoot = Get-RepoRoot
$defaultCaptureExecuteMethod = "MxFramework.Tests.Game.CharacterTest.CharacterTestProfileAnalyzerCommandLineCapture.RunPairCapture"
if ($StressScenario -and [string]::Equals($CaptureExecuteMethod, $defaultCaptureExecuteMethod, [System.StringComparison]::Ordinal)) {
    $CaptureExecuteMethod = "MxFramework.Tests.Game.CharacterTest.CharacterTestProfileAnalyzerCommandLineCapture.RunStressPairCapture"
}

if (-not $AfterWorktree) {
    $AfterWorktree = $repoRoot
}
else {
    $AfterWorktree = (Resolve-Path -LiteralPath $AfterWorktree).Path
}

if (-not $BaselineWorktree) {
    $repoDriveRoot = [System.IO.Path]::GetPathRoot($repoRoot)
    $BaselineWorktree = Join-Path $repoDriveRoot "WGF_pa_base_stress_current"
}

if (-not $OutputDir) {
    if ($StressScenario) {
        $OutputDir = Join-Path $repoRoot "ProfileAnalyzer/paired_tornado_stress_capture"
    }
    else {
        $OutputDir = Join-Path $repoRoot "ProfileAnalyzer/paired_tornado_capture"
    }
}

if (-not $DryRun) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$unityPath = Resolve-UnityExecutable $repoRoot
$patchPath = Join-Path $OutputDir "baseline_capture_harness.patch"
$markerPatchPath = Join-Path $repoRoot "Tools/Harness/profile_analyzer_baseline_tornado_markers.patch"

Ensure-BaselineWorktree $repoRoot $BaselineWorktree $BaselineRef
Write-BaselineHarnessPatch $repoRoot $BaselineRef $patchPath
Apply-BaselineHarnessPatch $BaselineWorktree $patchPath
Apply-BaselineMarkerInstrumentationPatch $BaselineWorktree $markerPatchPath

if ($SkipBaselineCapture) {
    $baselineFallbackPData = if ($StressScenario) {
        "Temp/ProfileAnalyzer/pair_tornado_stress_playmode.pdata"
    }
    else {
        "Temp/ProfileAnalyzer/pair_tornado_playmode.pdata"
    }

    $baselinePDataPath = Resolve-CaptureArtifact `
        $BaselinePData `
        $BaselineWorktree `
        $baselineFallbackPData `
        (Join-Path $OutputDir "fresh_before.pdata")
}
else {
    $baselinePDataPath = Invoke-UnityCapture $BaselineWorktree "fresh_before" $unityPath $OutputDir
}

if ($SkipAfterCapture) {
    $afterFallbackPData = if ($StressScenario) {
        "Temp/ProfileAnalyzer/pair_tornado_stress_playmode.pdata"
    }
    else {
        "Temp/ProfileAnalyzer/current_after_tornado_playmode.pdata"
    }

    $afterPDataPath = Resolve-CaptureArtifact `
        $AfterPData `
        $AfterWorktree `
        $afterFallbackPData `
        (Join-Path $OutputDir "fresh_after.pdata")
}
else {
    $afterPDataPath = Invoke-UnityCapture $AfterWorktree "fresh_after" $unityPath $OutputDir
}

$beforeTiming = Export-PData $repoRoot $baselinePDataPath "fresh_before" $OutputDir
$afterTiming = Export-PData $repoRoot $afterPDataPath "fresh_after" $OutputDir
Compare-Markers $repoRoot $beforeTiming $afterTiming $OutputDir

Write-Host "Pair capture artifacts written to: $OutputDir"
