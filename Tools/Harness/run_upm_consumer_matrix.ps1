[CmdletBinding()]
param(
    [string]$UnityPath,
    [Parameter(Mandatory = $true)]
    [string]$Revision,
    [string]$GitUrl,
    [string]$ConsumerRoot,
    [string]$OutputRoot,
    [string]$PythonPath
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$profileRunner = (Resolve-Path (
    Join-Path $repositoryRoot 'Tools\Harness\run_upm_consumer_smoke.ps1')).Path
$validator = Join-Path $repositoryRoot 'Tools\Harness\validate_upm_consumer_evidence.py'

function Write-Utf8WithoutBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RelativeArtifactId {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $artifactPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $artifactPath.StartsWith(
            $rootPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Artifact escaped the matrix output root: $artifactPath"
    }

    return $artifactPath.Substring($rootPath.Length).Replace('\', '/')
}

function Resolve-PythonExecutable {
    if (-not [string]::IsNullOrWhiteSpace($PythonPath)) {
        $resolved = (Resolve-Path -LiteralPath $PythonPath -ErrorAction Stop).Path
        return $resolved
    }
    if (-not [string]::IsNullOrWhiteSpace($env:PYTHON_BIN)) {
        $resolved = (Resolve-Path -LiteralPath $env:PYTHON_BIN -ErrorAction Stop).Path
        return $resolved
    }

    foreach ($commandName in @('python3', 'python')) {
        $command = Get-Command $commandName -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $command) {
            return $command.Source
        }
    }

    throw 'Python was not found. Pass -PythonPath or set PYTHON_BIN.'
}

function Invoke-EvidenceValidator {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('profile', 'matrix', 'receipt-candidate')]
        [string]$Kind,
        [Parameter(Mandatory = $true)]
        [string]$ReportPath,
        [Parameter(Mandatory = $true)]
        [string]$ArtifactRoot
    )

    & $script:pythonExecutable $validator $Kind $ReportPath '--artifact-root' $ArtifactRoot |
        Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "UPM evidence validator rejected $Kind report '$ReportPath'."
    }
}

function Get-ReportValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Report,
        [Parameter(Mandatory = $true)]
        [string[]]$Path
    )

    $value = $Report
    foreach ($segment in $Path) {
        if ($null -eq $value) {
            return $null
        }
        $property = $value.PSObject.Properties[$segment]
        if ($null -eq $property) {
            return $null
        }
        $value = $property.Value
    }
    return $value
}

function Test-ProfileTestsComplete {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Report
    )

    $expectedNames = @(Get-ReportValue -Report $Report -Path @('tests', 'expectedFullNames'))
    $observedNames = @(Get-ReportValue -Report $Report -Path @('tests', 'observedFullNames'))
    if ($expectedNames.Count -eq 0 -or $expectedNames.Count -ne $observedNames.Count) {
        return $false
    }
    if ([int](Get-ReportValue -Report $Report -Path @('tests', 'failed')) -ne 0 -or
        [int](Get-ReportValue -Report $Report -Path @('tests', 'skipped')) -ne 0) {
        return $false
    }

    $expectedSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $observedSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($name in $expectedNames) {
        if (-not $expectedSet.Add([string]$name)) {
            return $false
        }
    }
    foreach ($name in $observedNames) {
        if (-not $observedSet.Add([string]$name)) {
            return $false
        }
    }
    return $expectedSet.SetEquals($observedSet)
}

function Test-MutationGuardClean {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Report
    )

    $passed = Get-ReportValue -Report $Report -Path @('mutationGuard', 'passed')
    if ($passed -is [bool]) {
        return $passed
    }
    $verdict = [string](Get-ReportValue -Report $Report -Path @('mutationGuard', 'verdict'))
    return $verdict.Equals('clean', [System.StringComparison]::OrdinalIgnoreCase) -or
        $verdict.Equals('passed', [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-LogsClean {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Report
    )

    $passed = Get-ReportValue -Report $Report -Path @('logPolicy', 'passed')
    if ($passed -is [bool]) {
        return $passed
    }
    $explicitHitCount = Get-ReportValue -Report $Report -Path @('logPolicy', 'forbiddenHitCount')
    if ($null -eq $explicitHitCount) {
        $explicitHitCount = Get-ReportValue -Report $Report -Path @('logPolicy', 'hitCount')
    }
    if ($null -eq $explicitHitCount) {
        $hits = Get-ReportValue -Report $Report -Path @('logPolicy', 'hits')
        $explicitHitCount = if ($null -eq $hits) { $null } else { @($hits).Count }
    }
    $verdict = [string](Get-ReportValue -Report $Report -Path @('logPolicy', 'verdict'))
    if ($verdict.Equals('passed', [System.StringComparison]::OrdinalIgnoreCase)) {
        # The profile schema validator already proves that a passed verdict has
        # an empty hits array; PowerShell otherwise unwraps an empty array to null.
        return $true
    }
    if ($verdict.Equals('clean', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null -ne $explicitHitCount -and [int]$explicitHitCount -eq 0
    }
    return $false
}

if ($Revision -notmatch '^[0-9a-fA-F]{40}$') {
    throw '-Revision must be a full 40-character commit SHA.'
}
$candidateCommit = $Revision.ToLowerInvariant()

if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    throw "The planned P7 evidence validator is missing: $validator"
}
$script:pythonExecutable = Resolve-PythonExecutable

$observedHead = (& git -C $repositoryRoot rev-parse HEAD).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $observedHead -ne $candidateCommit) {
    throw "Local matrix rows require a checkout at candidate $candidateCommit; observed $observedHead."
}
$repositoryDirtyPaths = @(& git -C $repositoryRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the harness checkout state.'
}
if ($repositoryDirtyPaths.Count -ne 0) {
    throw 'The eight-row candidate matrix requires a clean checkout for its Local rows.'
}

if ([string]::IsNullOrWhiteSpace($ConsumerRoot)) {
    $ConsumerRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'WGameFrameworkConsumerMatrix'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $ConsumerRoot $candidateCommit
}
$matrixRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if ($matrixRoot.Length -gt 80) {
    throw "OutputRoot is too long for Unity PackageCache test paths ($($matrixRoot.Length) characters; maximum 80)."
}
$repositoryPrefix = $repositoryRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if ($matrixRoot.Equals($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $matrixRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must be outside the repository: $matrixRoot"
}
New-Item -ItemType Directory -Force -Path $matrixRoot | Out-Null

$matrixPath = Join-Path $matrixRoot 'matrix.json'
$matrixMarkdownPath = Join-Path $matrixRoot 'matrix.md'
$hashIndexPath = Join-Path $matrixRoot 'reports.sha256'
$receiptPath = Join-Path $matrixRoot 'p7-receipt-candidate.json'
foreach ($oldOutput in @($matrixPath, $matrixMarkdownPath, $hashIndexPath, $receiptPath)) {
    Remove-Item -LiteralPath $oldOutput -Force -ErrorAction SilentlyContinue
}

$requiredRows = @(
    [pscustomobject]@{ Source = 'Local'; SourceId = 'local'; Profile = 'base'; Code = 'lb' },
    [pscustomobject]@{ Source = 'Local'; SourceId = 'local'; Profile = 'with-fairygui'; Code = 'lf' },
    [pscustomobject]@{ Source = 'Local'; SourceId = 'local'; Profile = 'with-unity-transport'; Code = 'lu' },
    [pscustomobject]@{ Source = 'Local'; SourceId = 'local'; Profile = 'with-vfx-graph'; Code = 'lv' },
    [pscustomobject]@{ Source = 'PinnedGit'; SourceId = 'pinned-git'; Profile = 'base'; Code = 'pb' },
    [pscustomobject]@{ Source = 'PinnedGit'; SourceId = 'pinned-git'; Profile = 'with-fairygui'; Code = 'pf' },
    [pscustomobject]@{ Source = 'PinnedGit'; SourceId = 'pinned-git'; Profile = 'with-unity-transport'; Code = 'pu' },
    [pscustomobject]@{ Source = 'PinnedGit'; SourceId = 'pinned-git'; Profile = 'with-vfx-graph'; Code = 'pv' }
)

$rowResults = @()
foreach ($requiredRow in $requiredRows) {
    $rowId = "$($requiredRow.SourceId)-$($requiredRow.Profile)"
    $rowConsumerRoot = Join-Path (Join-Path $matrixRoot 'r') $requiredRow.Code
    $reportPath = Join-Path (
        Join-Path (Join-Path $rowConsumerRoot $requiredRow.Source) $requiredRow.Profile) `
        'report.json'
    $failureReason = $null
    $report = $null
    $reportHash = $null
    $schemaValid = $false
    $artifactHashValid = $false
    $mutationClean = $false
    $testsComplete = $false
    $logsClean = $false
    $eligibleForMatrix = $false
    $passed = $false

    Write-Host "Running required matrix row: $rowId"
    try {
        $runnerArguments = @{
            PackageSource = $requiredRow.Source
            Profile = $requiredRow.Profile
            ConsumerRoot = $rowConsumerRoot
        }
        if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
            $runnerArguments.UnityPath = $UnityPath
        }
        if ($requiredRow.Source -eq 'PinnedGit') {
            $runnerArguments.Revision = $candidateCommit
            if (-not [string]::IsNullOrWhiteSpace($GitUrl)) {
                $runnerArguments.GitUrl = $GitUrl
            }
        }

        & $profileRunner @runnerArguments
        if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
            throw "Profile runner did not create $reportPath"
        }

        Invoke-EvidenceValidator `
            -Kind profile `
            -ReportPath $reportPath `
            -ArtifactRoot (Split-Path -Parent $reportPath)
        $schemaValid = $true
        $artifactHashValid = $true
        $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
        $reportHash = Get-Sha256 -Path $reportPath

        $mutationClean = Test-MutationGuardClean -Report $report
        $testsComplete = Test-ProfileTestsComplete -Report $report
        $logsClean = Test-LogsClean -Report $report

        $reportedProfile = [string](Get-ReportValue -Report $report -Path @('execution', 'profile'))
        $reportedSource = [string](Get-ReportValue -Report $report -Path @('candidate', 'sourceMode'))
        $eligible = Get-ReportValue -Report $report -Path @('execution', 'eligibleForMatrix')
        $eligibleForMatrix = $eligible -eq $true
        if ($reportedProfile -ne $requiredRow.Profile) {
            throw "Report profile '$reportedProfile' does not match required '$($requiredRow.Profile)'."
        }
        if ($reportedSource -ne $requiredRow.SourceId) {
            throw "Report source '$reportedSource' does not match required '$($requiredRow.SourceId)'."
        }
        if ($eligible -ne $true) {
            throw 'Profile report is not eligible for the required matrix.'
        }
        if (-not $mutationClean) {
            throw 'Profile report mutation guard is not clean.'
        }
        if (-not $testsComplete) {
            throw 'Profile report did not execute the exact expected test-name set.'
        }
        if (-not $logsClean) {
            throw 'Profile report contains forbidden log hits.'
        }
        $passed = $true
    }
    catch {
        $failureDetail = $_.Exception.Message
        $failureReason = 'profile-runner-or-validation-failed'
        Write-Warning "Required matrix row $rowId failed: $failureDetail"
    }

    $rowResults += [pscustomobject][ordered]@{
        id = $rowId
        sourceMode = $requiredRow.SourceId
        profile = $requiredRow.Profile
        required = $true
        report = if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
            Get-RelativeArtifactId -Root $matrixRoot -Path $reportPath
        } else { $null }
        reportSha256 = $reportHash
        eligible = $eligibleForMatrix
        schemaValid = $schemaValid
        artifactHashValid = $artifactHashValid
        mutationClean = $mutationClean
        testsComplete = $testsComplete
        logsClean = $logsClean
        candidateEqual = $false
        passed = $passed
        failureReason = $failureReason
        reportData = $report
    }
}

$passingReports = @($rowResults | Where-Object { $_.passed } | Select-Object -ExpandProperty reportData)
$packageNames = @($passingReports | ForEach-Object {
        [string](Get-ReportValue -Report $_ -Path @('candidate', 'packageName'))
    } | Sort-Object -Unique)
$packageVersions = @($passingReports | ForEach-Object {
        [string](Get-ReportValue -Report $_ -Path @('candidate', 'packageVersion'))
    } | Sort-Object -Unique)
$packageTreeHashes = @($passingReports | ForEach-Object {
        [string](Get-ReportValue -Report $_ -Path @('candidate', 'packageTreeHash'))
    } | Sort-Object -Unique)
$pinnedResolvedCommits = @($passingReports | Where-Object {
        (Get-ReportValue -Report $_ -Path @('candidate', 'sourceMode')) -eq 'pinned-git'
    } | ForEach-Object {
        [string](Get-ReportValue -Report $_ -Path @('candidate', 'resolvedCommit'))
    } | Sort-Object -Unique)

$identityReasons = @()
if ($passingReports.Count -ne $requiredRows.Count) {
    $identityReasons += 'one-or-more-required-rows-failed'
}
if ($packageNames.Count -ne 1 -or [string]::IsNullOrWhiteSpace($packageNames[0])) {
    $identityReasons += 'package-name-mismatch'
}
if ($packageVersions.Count -ne 1 -or [string]::IsNullOrWhiteSpace($packageVersions[0])) {
    $identityReasons += 'package-version-mismatch'
}
if ($packageTreeHashes.Count -ne 1 -or $packageTreeHashes[0] -notmatch '^[0-9a-fA-F]{64}$') {
    $identityReasons += 'package-tree-hash-mismatch'
}
if ($pinnedResolvedCommits.Count -ne 1 -or $pinnedResolvedCommits[0] -ne $candidateCommit) {
    $identityReasons += 'pinned-resolved-commit-mismatch'
}
$candidateEqualityPassed = $identityReasons.Count -eq 0
$rowResults | ForEach-Object {
    if (-not $_.passed -or $null -eq $_.reportData) {
        $_.candidateEqual = $false
    }
    else {
        $rowReport = $_.reportData
        $rowPackageName = [string](Get-ReportValue -Report $rowReport -Path @('candidate', 'packageName'))
        $rowPackageVersion = [string](Get-ReportValue -Report $rowReport -Path @('candidate', 'packageVersion'))
        $rowTreeHash = [string](Get-ReportValue -Report $rowReport -Path @('candidate', 'packageTreeHash'))
        $rowCandidateEqual = $candidateEqualityPassed -and
            $rowPackageName -eq $packageNames[0] -and
            $rowPackageVersion -eq $packageVersions[0] -and
            $rowTreeHash -eq $packageTreeHashes[0]
        if ($_.sourceMode -eq 'pinned-git') {
            $requestedCommit = [string](Get-ReportValue `
                -Report $rowReport `
                -Path @('candidate', 'requestedCommit'))
            $resolvedCommit = [string](Get-ReportValue `
                -Report $rowReport `
                -Path @('candidate', 'resolvedCommit'))
            $rowCandidateEqual = $rowCandidateEqual -and
                $requestedCommit -eq $candidateCommit -and
                $resolvedCommit -eq $candidateCommit
        }
        else {
            $harnessCommit = [string](Get-ReportValue `
                -Report $rowReport `
                -Path @('harness', 'repositoryCommit'))
            $harnessDirty = Get-ReportValue `
                -Report $rowReport `
                -Path @('harness', 'repositoryDirty')
            $rowCandidateEqual = $rowCandidateEqual -and
                $harnessCommit -eq $candidateCommit -and
                $harnessDirty -eq $false
        }
        $_.candidateEqual = $rowCandidateEqual
    }
}
$candidateEqualityPassed = $candidateEqualityPassed -and
    @($rowResults | Where-Object { -not $_.candidateEqual }).Count -eq 0
if (-not $candidateEqualityPassed -and 'row-candidate-identity-mismatch' -notin $identityReasons) {
    $identityReasons += 'row-candidate-identity-mismatch'
}
$promotable = $candidateEqualityPassed -and @($rowResults | Where-Object { -not $_.passed }).Count -eq 0
$matrixReasons = @($identityReasons)
if (-not $promotable -and $matrixReasons.Count -eq 0) {
    $matrixReasons += 'required-row-failed'
}

$publishedRows = @($rowResults | ForEach-Object {
        [ordered]@{
            id = $_.id
            sourceMode = $_.sourceMode
            profile = $_.profile
            required = $_.required
            report = $_.report
            reportSha256 = $_.reportSha256
            eligible = $_.eligible
            schemaValid = $_.schemaValid
            artifactHashValid = $_.artifactHashValid
            mutationClean = $_.mutationClean
            testsComplete = $_.testsComplete
            logsClean = $_.logsClean
            candidateEqual = $_.candidateEqual
            passed = $_.passed
            failureReason = $_.failureReason
        }
    })
$matrixReport = [ordered]@{
    schemaVersion = 1
    type = 'upm-consumer-matrix'
    evidenceLevel = 'E2-matrix'
    candidate = [ordered]@{
        commit = $candidateCommit
        packageName = if ($packageNames.Count -eq 1) { $packageNames[0] } else { $null }
        packageVersion = if ($packageVersions.Count -eq 1) { $packageVersions[0] } else { $null }
        packageTreeHash = if ($packageTreeHashes.Count -eq 1) { $packageTreeHashes[0] } else { $null }
        workingTreeDirty = $false
    }
    requiredRowCount = $requiredRows.Count
    requiredRows = $publishedRows
    candidateEquality = [ordered]@{
        passed = $candidateEqualityPassed
        reasons = @($identityReasons)
    }
    promotable = $promotable
    reasons = @($matrixReasons | Sort-Object -Unique)
}
Write-Utf8WithoutBom `
    -Path $matrixPath `
    -Content ($matrixReport | ConvertTo-Json -Depth 12)
Invoke-EvidenceValidator -Kind matrix -ReportPath $matrixPath -ArtifactRoot $matrixRoot
$matrixHash = Get-Sha256 -Path $matrixPath

$markdownLines = @(
    '# UPM Consumer Candidate Matrix',
    '',
    "- Candidate: $candidateCommit",
    "- Required rows: $($requiredRows.Count)",
    "- Candidate equality: $($candidateEqualityPassed.ToString().ToLowerInvariant())",
    "- Promotable: $($promotable.ToString().ToLowerInvariant())",
    '',
    '| Row | Report SHA-256 | Result |',
    '| --- | --- | --- |'
)
foreach ($row in $publishedRows) {
    $result = if ($row.passed) { 'passed' } else { "failed: $($row.failureReason)" }
    $markdownLines += "| $($row.id) | $($row.reportSha256) | $result |"
}
Write-Utf8WithoutBom -Path $matrixMarkdownPath -Content ($markdownLines -join "`n")

if ($promotable) {
    $pendingP8Gates = @(
        'legal-and-release-metadata',
        'n-minus-one-upgrade-and-migration',
        'optional-player-gates',
        'pinned-git-player-build-and-run',
        'public-api-compatibility',
        'release-tag'
    )
    $receiptCandidate = [ordered]@{
        schemaVersion = 1
        type = 'upm-e3-receipt-candidate'
        candidate = $matrixReport.candidate
        matrix = [ordered]@{
            report = Get-RelativeArtifactId -Root $matrixRoot -Path $matrixPath
            sha256 = $matrixHash
        }
        reports = @($publishedRows | ForEach-Object {
                [ordered]@{
                    id = $_.id
                    report = $_.report
                    sha256 = $_.reportSha256
                    passed = $_.passed
                }
            })
        promotable = $true
        releaseReady = $false
        pendingP8Gates = $pendingP8Gates
    }
    Write-Utf8WithoutBom `
        -Path $receiptPath `
        -Content ($receiptCandidate | ConvertTo-Json -Depth 12)
    Invoke-EvidenceValidator `
        -Kind receipt-candidate `
        -ReportPath $receiptPath `
        -ArtifactRoot $matrixRoot
}

$hashedArtifacts = @($publishedRows | Where-Object { $null -ne $_.report } |
        ForEach-Object { Join-Path $matrixRoot ($_.report.Replace('/', '\')) })
$hashedArtifacts += @($matrixPath, $matrixMarkdownPath)
if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
    $hashedArtifacts += $receiptPath
}
$hashLines = @($hashedArtifacts | ForEach-Object {
        $artifactId = Get-RelativeArtifactId -Root $matrixRoot -Path $_
        '{0}  {1}' -f (Get-Sha256 -Path $_), $artifactId
    } | Sort-Object)
Write-Utf8WithoutBom -Path $hashIndexPath -Content ($hashLines -join "`n")

Write-Host "Matrix report: $matrixPath"
Write-Host "Report hashes: $hashIndexPath"
if ($promotable) {
    Write-Host "P7 receipt candidate: $receiptPath"
    Write-Host 'UPM Consumer matrix passed and is promotable for P7 evidence.'
}
else {
    throw 'UPM Consumer matrix is incomplete or failed and is not promotable.'
}
