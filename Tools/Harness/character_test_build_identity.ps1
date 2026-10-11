# Unity-independent preflight. Loading this file never starts or contacts Unity.
Set-StrictMode -Version Latest

function Read-CharacterTestProjectVersion {
    param([Parameter(Mandatory)][string]$ProjectRoot)
    $path = Join-Path $ProjectRoot 'ProjectSettings/ProjectVersion.txt'
    $contents = Get-Content -LiteralPath $path -Raw
    if ($contents -notmatch '(?m)^m_EditorVersion:\s*(\S+)\s*$') {
        throw "Missing Unity version in $path"
    }
    $version = $Matches[1]
    $revision = ''
    if ($contents -match '(?m)^m_EditorVersionWithRevision:\s*\S+\s+\(([0-9a-f]+)\)') {
        $revision = $Matches[1]
    }
    return [pscustomobject]@{ Version = $version; Revision = $revision }
}

function Assert-CharacterTestUnityVersion {
    param([Parameter(Mandatory)]$Expected, [Parameter(Mandatory)][string]$ProductVersion)
    if ($ProductVersion -notmatch '^(\d+\.\d+\.\d+[abfp]\d+)(?:_([0-9a-f]+))?$') {
        throw "Unrecognized Unity executable ProductVersion: $ProductVersion"
    }
    $actualVersion = $Matches[1]
    $actualRevision = if ($Matches.ContainsKey(2)) { $Matches[2] } else { '' }
    if ($actualVersion -ne $Expected.Version -or
        (-not [string]::IsNullOrWhiteSpace($Expected.Revision) -and $actualRevision -ne $Expected.Revision)) {
        throw "Unity executable does not match ProjectVersion.txt: expected $($Expected.Version)_$($Expected.Revision), actual $ProductVersion"
    }
    return [pscustomobject]@{ Version = $actualVersion; Revision = $actualRevision }
}

function Assert-CharacterTestBuildPaths {
    param([Parameter(Mandatory)][string]$ProjectRoot, [Parameter(Mandatory)][string]$OutputRoot)
    $project = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([char[]]'\/')
    $output = [IO.Path]::GetFullPath($OutputRoot).TrimEnd([char[]]'\/')
    if ($output.Equals($project, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Build output must not be the project root.'
    }
    foreach ($directory in @('Assets', 'Packages', 'ProjectSettings', 'Library', 'Temp')) {
        $protected = Join-Path $project $directory
        if ($output.Equals($protected, [StringComparison]::OrdinalIgnoreCase) -or
            $output.StartsWith($protected + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Build output must not be inside $directory. Use a fresh directory outside the project."
        }
    }
    if (Test-Path -LiteralPath $output) {
        if (-not (Test-Path -LiteralPath $output -PathType Container) -or
            @(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) {
            throw "Build output must be a new or empty directory; existing evidence is preserved: $output"
        }
    }
    $lockPath = Join-Path $project 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $lockPath) {
        throw "Unity project has an Editor lock. Use an isolated snapshot; do not build the shared workspace: $project"
    }
    $attemptPath = Join-Path $project 'SourceSnapshotAttempt.json'
    if (Test-Path -LiteralPath $attemptPath -PathType Leaf) {
        $attempt = Get-Content -LiteralPath $attemptPath -Raw | ConvertFrom-Json
        $snapshotPath = Join-Path $project 'SourceSnapshot.json'
        if ($attempt.status -ne 'Verified' -or -not (Test-Path -LiteralPath $snapshotPath -PathType Leaf)) {
            throw 'Isolated snapshot is incomplete or rejected; wait for a verified source snapshot.'
        }
        $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json
        if ($snapshot.status -ne 'Verified') { throw 'Isolated snapshot is not verified.' }
    }
}

function New-CharacterTestBuildInvocation {
    param([Parameter(Mandatory)][string]$ProjectRoot, [Parameter(Mandatory)][string]$UnityPath,
        [Parameter(Mandatory)][string]$OutputRoot, [Parameter(Mandatory)][string]$Configuration,
        [bool]$SkipResourceBuild, [bool]$NoRun)
    $expected = Read-CharacterTestProjectVersion -ProjectRoot $ProjectRoot
    $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($UnityPath).ProductVersion
    $actual = Assert-CharacterTestUnityVersion -Expected $expected -ProductVersion $productVersion
    $snapshotIdentity = $null
    $snapshotPath = Join-Path $ProjectRoot 'SourceSnapshot.json'
    if (Test-Path -LiteralPath $snapshotPath -PathType Leaf) {
        $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json
        if ($snapshot.status -ne 'Verified') { throw 'Isolated snapshot is not verified.' }
        $snapshotIdentity = [ordered]@{
            sourceRoot = $snapshot.sourceRoot; sourceFingerprint = $snapshot.sourceFingerprint
            verifiedUtc = $snapshot.verifiedUtc; fileCount = $snapshot.fileCount; fileBytes = $snapshot.fileBytes
            sourceControl = $snapshot.sourceControl
        }
    }
    $inputs = @()
    foreach ($relative in @('ProjectSettings/ProjectVersion.txt', 'ProjectSettings/ProjectSettings.asset',
        'ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset',
        'Packages/manifest.json', 'Packages/packages-lock.json', 'SourceSnapshot.json', 'BuildOptimizationOverlay.json')) {
        $path = Join-Path $ProjectRoot $relative
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $inputs += [ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
    }
    $overlay = @()
    $overlayPath = Join-Path $ProjectRoot 'BuildOptimizationOverlay.json'
    if (Test-Path -LiteralPath $overlayPath -PathType Leaf) {
        foreach ($entry in (Get-Content -LiteralPath $overlayPath -Raw | ConvertFrom-Json).files) {
            $path = [IO.Path]::GetFullPath((Join-Path $ProjectRoot $entry.path))
            $projectPrefix = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
            if (-not $path.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Overlay path escapes the project.' }
            $overlay += [ordered]@{path=$entry.path;baselineSha256=$entry.baselineSha256;invocationSha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()}
        }
    }
    $sourceControl = [ordered]@{ available = $false; commit = ''; status = @() }
    if ((Test-Path -LiteralPath (Join-Path $ProjectRoot '.git')) -and (Get-Command git -ErrorAction SilentlyContinue)) {
        $commit = & git -C $ProjectRoot rev-parse HEAD 2>$null
        if ($LASTEXITCODE -eq 0) {
            $status = @(& git -C $ProjectRoot -c core.quotepath=false status --porcelain=v1 --untracked-files=all)
            if ($LASTEXITCODE -ne 0) { throw 'Could not record source control status.' }
            $sourceControl = [ordered]@{ available = $true; commit = [string]$commit; status = $status }
        }
    }
    return [ordered]@{
        schemaVersion = 1; invocationId = [Guid]::NewGuid().ToString('N'); utc = [DateTime]::UtcNow.ToString('o')
        projectPath = [IO.Path]::GetFullPath($ProjectRoot); outputRoot = [IO.Path]::GetFullPath($OutputRoot)
        unityExecutable = [IO.Path]::GetFullPath($UnityPath); expectedUnityVersion = $expected.Version
        expectedUnityRevision = $expected.Revision; executableUnityVersion = $actual.Version
        executableUnityRevision = $actual.Revision; executableProductVersion = $productVersion
        configuration = $Configuration; target = 'StandaloneWindows64'; skipResourceBuild = $SkipResourceBuild
        noRun = $NoRun; sourceControl = $sourceControl; inputIdentities = $inputs
        sourceSnapshot = $snapshotIdentity
        sourceOverlay = $overlay
        sourceIdentityScope = 'Project/package settings hashes and Git status; full file identities require SourceSnapshot.json.'
    }
}

function Assert-CharacterTestUnityLogVersion {
    param([Parameter(Mandatory)][string]$LogPath, [Parameter(Mandatory)]$Expected)
    $headers = @(Get-Content -LiteralPath $LogPath -TotalCount 40)
    $header = $headers | Where-Object { $_ -match "Version is '(\S+) \(([0-9a-f]+)\)" } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($header) -or $header -notmatch "Version is '(\S+) \(([0-9a-f]+)\)") {
        throw "Unity log has no actual Editor version header: $LogPath"
    }
    $productVersion = $Matches[1] + '_' + $Matches[2]
    Assert-CharacterTestUnityVersion -Expected $Expected -ProductVersion $productVersion | Out-Null
}
