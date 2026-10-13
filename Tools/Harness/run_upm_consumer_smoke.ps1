[CmdletBinding()]
param(
    [string]$UnityPath,
    [ValidateSet('Local', 'PinnedGit')]
    [string]$PackageSource = 'Local',
    [ValidateSet('base', 'with-fairygui', 'with-unity-transport', 'with-vfx-graph')]
    [string]$Profile = 'base',
    [string]$ConsumerRoot = 'C:\WGameFrameworkConsumerSmokeRuns',
    [string]$GitUrl,
    [string]$Revision,
    [switch]$ReuseLibrary,
    [switch]$IncludePlayerBuild
)

$ErrorActionPreference = 'Stop'

$packageName = 'com.vincent.wgameframework'
$packageSubpath = '/Packages/com.vincent.wgameframework'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$consumerFixture = (Resolve-Path (Join-Path $repositoryRoot 'Tools\PackageConsumerSmoke\UnityProject')).Path
$sourcePackageRoot = (Resolve-Path (Join-Path $repositoryRoot 'Packages\com.vincent.wgameframework')).Path
$packageBoundaryChecker = (Resolve-Path (
    Join-Path $repositoryRoot 'Tools\Harness\check_upm_package_boundary.py')).Path
$gitPackageTreeHasher = (Resolve-Path (
    Join-Path $repositoryRoot 'Tools\Harness\hash_git_package_tree.py')).Path
$sourcePackageManifestPath = Join-Path $sourcePackageRoot 'package.json'
$sourcePackageManifest = Get-Content -Raw -LiteralPath $sourcePackageManifestPath | ConvertFrom-Json
$optionalIntegrationsPath = Join-Path $sourcePackageRoot 'optional-integrations.json'
$optionalIntegrationsAuthority = Get-Content -Raw -LiteralPath $optionalIntegrationsPath |
    ConvertFrom-Json
if ([int]$optionalIntegrationsAuthority.schemaVersion -ne 1 -or
    [string]$optionalIntegrationsAuthority.package -ne $packageName) {
    throw 'optional-integrations.json has an unsupported schema or package identity.'
}
$mainProject = $repositoryRoot
$projectVersionFile = Join-Path $mainProject 'ProjectSettings\ProjectVersion.txt'
$projectVersionLine = Get-Content -LiteralPath $projectVersionFile |
    Where-Object { $_ -like 'm_EditorVersion: *' } |
    Select-Object -First 1
$editorVersion = $projectVersionLine.Substring('m_EditorVersion: '.Length).Trim()

$consumerProfiles = [ordered]@{
    'base' = [pscustomobject]@{
        Name = 'base'
        PackageName = $null
        PackageVersion = $null
        PackageSubpath = $null
        AdapterAssembly = $null
        BehaviorTest = $null
        Define = $null
        SupportTier = 'O0'
    }
    'with-fairygui' = [pscustomobject]@{
        Name = 'with-fairygui'
    }
    'with-unity-transport' = [pscustomobject]@{
        Name = 'with-unity-transport'
    }
    'with-vfx-graph' = [pscustomobject]@{
        Name = 'with-vfx-graph'
    }
}

$profileName = $Profile.ToLowerInvariant()
$profileConfig = $consumerProfiles[$profileName]
if ($null -eq $profileConfig) {
    throw "Unknown consumer profile '$Profile'."
}
$profileAuthority = $null
if ($profileName -ne 'base') {
    $profileAuthority = @(
        $optionalIntegrationsAuthority.integrations |
            Where-Object { [string]$_.profile -eq $profileName }
    )
    if ($profileAuthority.Count -ne 1) {
        throw "Optional integration authority must contain exactly one '$profileName' entry."
    }

    $authority = $profileAuthority[0]
    $adapterAssemblies = @($authority.adapterAssemblies)
    if ([string]$authority.supportTier -ne 'O2' -or
        [string]::IsNullOrWhiteSpace([string]$authority.packageName) -or
        [string]::IsNullOrWhiteSpace([string]$authority.validatedVersion) -or
        [string]::IsNullOrWhiteSpace([string]$authority.define) -or
        $adapterAssemblies.Count -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$authority.behaviorTest)) {
        throw "Optional integration authority entry '$profileName' is incomplete or is not an O2 positive profile."
    }
    $profileConfig = [pscustomobject]@{
        Name = $profileName
        IntegrationId = [string]$authority.id
        PackageName = [string]$authority.packageName
        MinimumVersion = [string]$authority.minimumVersion
        PackageVersion = [string]$authority.validatedVersion
        PackageSubpath = if ([string]::IsNullOrWhiteSpace([string]$authority.sourceSubpath)) {
            $null
        } else {
            [string]$authority.sourceSubpath
        }
        SourceKind = [string]$authority.sourceKind
        AdapterAssembly = [string]$adapterAssemblies[0]
        BehaviorTest = [string]$authority.behaviorTest
        Define = [string]$authority.define
        SupportTier = [string]$authority.supportTier
    }
}
if ($IncludePlayerBuild -and $profileName -ne 'base') {
    throw '-IncludePlayerBuild currently supports only the base Consumer profile.'
}

function Remove-ConsumerDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$AllowedParent
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedParent = [System.IO.Path]::GetFullPath($AllowedParent)
    $expectedPrefix = $resolvedParent.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar

    if (-not $resolvedPath.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the allowed consumer boundary: $resolvedPath"
    }

    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        return
    }

    try {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }
    catch [System.IO.IOException], [System.IO.DirectoryNotFoundException] {
        if ($env:OS -ne 'Windows_NT') {
            throw
        }

        $extendedPath = '\\?\' + $resolvedPath
        [System.IO.Directory]::Delete($extendedPath, $true)
    }
}

function Write-Utf8WithoutBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Get-SnapshotHash {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.Dictionary[string,string]]$Snapshot,
        [string[]]$ExcludedPaths = @()
    )

    $lines = @(
        $Snapshot.Keys |
            Where-Object { $_ -notin $ExcludedPaths } |
            Sort-Object |
            ForEach-Object { "$_`t$($Snapshot[$_])" }
    )
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-TextHash {
    param([AllowEmptyString()][Parameter(Mandatory = $true)][string]$Text)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-FileArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Id,
        [Parameter(Mandatory = $true)]
        [string]$Type
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    $artifactPath = [System.IO.Path]::GetFullPath($Path)
    $artifactRoot = [System.IO.Path]::GetFullPath($outputDirectory).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $artifactPath.StartsWith(
            $artifactRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Evidence artifact escaped the Consumer output directory: $artifactPath"
    }
    $storageKey = $artifactPath.Substring($artifactRoot.Length).Replace('\', '/')

    return [ordered]@{
        id = $Id
        type = $Type
        sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
        externalStorageReference = "consumer-output:$storageKey"
    }
}

function Get-FailureClassification {
    param([Parameter(Mandatory = $true)][string]$Message)

    if ($Message -match '(?i)license|serial|activation') { return 'unity-license' }
    if ($Message -match '(?i)credential|authentication|authorization|access denied|permission denied') { return 'credential' }
    if ($Message -match '(?i)network|unable to access|could not resolve host|connection|timed out|TLS|SSL') { return 'network' }
    if ($Message -match '(?i)mutation|changed during') { return 'mutation' }
    if ($Message -match '(?i)resolved.*expected|revision|commit hash|candidate|tree hash|identity') { return 'identity-mismatch' }
    if ($Message -match '(?i)schema') { return 'schema-failure' }
    if ($Message -match '(?i)was not found|required|did not create|unavailable|cannot snapshot|Python 3') { return 'infrastructure-unavailable' }
    return 'product-failure'
}

function Write-FailureReport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    $sanitizedMessage = [string]$ErrorRecord.Exception.Message
    foreach ($sensitiveValue in @(
        [string]$repositoryRoot,
        [string]$consumerRootPath,
        [string]$env:USERPROFILE)) {
        if (-not [string]::IsNullOrWhiteSpace($sensitiveValue)) {
            $sanitizedMessage = $sanitizedMessage.Replace($sensitiveValue, '<redacted-path>')
        }
    }

    $failure = [ordered]@{
        schemaVersion = 4
        reportType = 'failure'
        candidate = [ordered]@{
            sourceMode = if ($PackageSource -eq 'PinnedGit') { 'pinned-git' } else { 'local' }
            requestedCommit = $requestedRevision
            resolvedCommit = $resolvedRevision
            packageName = $packageName
            packageVersion = [string]$sourcePackageManifest.version
        }
        execution = [ordered]@{
            profile = $profileName
            evidenceLevel = if ($PackageSource -eq 'PinnedGit') { 'E2-row' } else { 'E1' }
            eligibleForMatrix = $false
            result = 'failed'
            failureClassification = Get-FailureClassification -Message $sanitizedMessage
        }
        error = [ordered]@{
            message = $sanitizedMessage
            fullyQualifiedErrorId = [string]$ErrorRecord.FullyQualifiedErrorId
        }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    Write-Utf8WithoutBom -Path $Path -Content ($failure | ConvertTo-Json -Depth 8)
}

function Get-DirectoryContentSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($Path).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
        throw "Cannot snapshot a missing directory: $resolvedRoot"
    }

    $prefix = $resolvedRoot + [System.IO.Path]::DirectorySeparatorChar
    $snapshot = New-Object 'System.Collections.Generic.Dictionary[string,string]' (
        [System.StringComparer]::Ordinal)
    $files = @(
        Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File -Force |
            Sort-Object FullName
    )
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($prefix.Length).Replace('\', '/')
        $contentHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $snapshot.Add($relativePath, $contentHash)
    }

    return ,$snapshot
}

function Assert-DirectoryContentUnchanged {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.Dictionary[string,string]]$Before,
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.Dictionary[string,string]]$After,
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [string[]]$AllowedPaths = @()
    )

    $paths = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::Ordinal)
    foreach ($path in $Before.Keys) {
        $null = $paths.Add([string]$path)
    }
    foreach ($path in $After.Keys) {
        $null = $paths.Add([string]$path)
    }

    $changes = New-Object 'System.Collections.Generic.List[string]'
    foreach ($path in @($paths | Sort-Object)) {
        $beforeHash = if ($Before.ContainsKey($path)) { [string]$Before[$path] } else { $null }
        $afterHash = if ($After.ContainsKey($path)) { [string]$After[$path] } else { $null }
        if (-not [string]::Equals($beforeHash, $afterHash, [System.StringComparison]::Ordinal)) {
            if ($path -in $AllowedPaths) {
                continue
            }
            $changes.Add([string]$path)
        }
    }

    if ($changes.Count -gt 0) {
        $sample = @($changes | Select-Object -First 8) -join ', '
        throw "$Label changed during the Unity Consumer run ($($changes.Count) file(s)): $sample"
    }
}

function Resolve-PythonExecutable {
    $candidates = New-Object 'System.Collections.Generic.List[string]'
    if (-not [string]::IsNullOrWhiteSpace($env:PYTHON_BIN)) {
        $candidates.Add($env:PYTHON_BIN)
    }
    $candidates.Add('python3')
    $candidates.Add('python')

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }

        $command = Get-Command $candidate -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $command) {
            return [string]$command.Source
        }
    }

    throw 'Python 3 is required for the cross-platform UPM package boundary check. Set PYTHON_BIN when it is not on PATH.'
}

function Get-PinnedGitReference {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryUrl,
        [Parameter(Mandatory = $true)]
        [string]$Commit,
        [string]$PackagePath = $packageSubpath
    )

    if ($Commit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'PinnedGit requires -Revision to be a full 40-character commit SHA.'
    }

    $transportUrl = $RepositoryUrl.Trim()
    if ($transportUrl.StartsWith('git+', [System.StringComparison]::OrdinalIgnoreCase)) {
        $transportUrl = $transportUrl.Substring(4)
    }

    if ($transportUrl.Contains('?') -or $transportUrl.Contains('#')) {
        throw 'Pass the repository root in -GitUrl; the runner owns the package subpath and revision.'
    }

    $transportUrl = $transportUrl.TrimEnd('/')
    try {
        $parsedUri = [System.Uri]$transportUrl
    }
    catch {
        throw "GitUrl is not a valid absolute URI: $RepositoryUrl"
    }

    if (-not $parsedUri.IsAbsoluteUri) {
        throw "GitUrl is not an absolute URI: $RepositoryUrl"
    }

    $allowedSchemes = @('http', 'https', 'ssh', 'git')
    if ($parsedUri.Scheme -notin $allowedSchemes) {
        throw "GitUrl scheme '$($parsedUri.Scheme)' is not supported by this runner."
    }

    if (-not [string]::IsNullOrWhiteSpace($parsedUri.UserInfo)) {
        throw 'GitUrl must not embed credentials.'
    }

    if ([string]::IsNullOrWhiteSpace($PackagePath) -or
        -not $PackagePath.StartsWith('/') -or
        $PackagePath.Contains('?') -or
        $PackagePath.Contains('#')) {
        throw "PackagePath must be an absolute repository subpath without query or fragment: $PackagePath"
    }

    if (-not $transportUrl.EndsWith('.git', [System.StringComparison]::OrdinalIgnoreCase)) {
        $transportUrl += '.git'
    }

    return "git+${transportUrl}?path=${PackagePath}#$($Commit.ToLowerInvariant())"
}

function Test-PackageStructure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageRoot,
        [Parameter(Mandatory = $true)]
        [string]$ReportPath,
        [string]$CandidateCommit
    )

    $pythonExecutable = Resolve-PythonExecutable
    $checkerArguments = @(
        $packageBoundaryChecker,
        '--package-root', $PackageRoot,
        '--expected-package-name', $packageName,
        '--report-json', $ReportPath
    )
    if (-not [string]::IsNullOrWhiteSpace($CandidateCommit)) {
        $checkerArguments += @('--candidate-commit', $CandidateCommit)
    }

    & $pythonExecutable @checkerArguments | Out-Host
    $checkerExitCode = $LASTEXITCODE
    if ($checkerExitCode -ne 0) {
        throw "UPM package boundary check exited with code $checkerExitCode. See $ReportPath"
    }
    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
        throw "UPM package boundary check did not create its JSON report: $ReportPath"
    }

    $boundaryReport = Get-Content -Raw -LiteralPath $ReportPath | ConvertFrom-Json
    if (-not [bool]$boundaryReport.result.passed) {
        throw "UPM package boundary report is not passing. See $ReportPath"
    }

    $manifestPath = Join-Path $PackageRoot 'package.json'
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $declaredDependencies = @($boundaryReport.package.dependencies.PSObject.Properties.Name)
    $optionalGateAssemblyCounts = [ordered]@{}
    foreach ($property in $boundaryReport.assemblies.optionalGates.PSObject.Properties) {
        $optionalGateAssemblyCounts[$property.Name] = [int]$property.Value.assemblyCount
    }

    return [pscustomobject]@{
        Manifest = $manifest
        DeclaredDependencies = [string[]]$declaredDependencies
        OptionalGateAssemblyCounts = $optionalGateAssemblyCounts
        BoundaryReport = $boundaryReport
    }
}

if ($sourcePackageManifest.name -ne $packageName) {
    throw "Source package name is '$($sourcePackageManifest.name)', expected '$packageName'."
}

$consumerRootPath = [System.IO.Path]::GetFullPath($ConsumerRoot)
$consumerDriveRoot = [System.IO.Path]::GetPathRoot($consumerRootPath)
$longLivedConsumerRoots = @(
    [System.IO.Path]::GetFullPath('C:\WGameFrameworkConsumerSmoke'),
    [System.IO.Path]::GetFullPath('C:\WGameFrameworkConsumerRemote')
)
$repositoryPrefix = $repositoryRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$consumerPrefix = $consumerRootPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if ($consumerRootPath -eq $consumerDriveRoot) {
    throw "ConsumerRoot must not be a drive root: $consumerRootPath"
}
foreach ($longLivedConsumerRoot in $longLivedConsumerRoots) {
    $longLivedConsumerPrefix = $longLivedConsumerRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if ($consumerRootPath.Equals($longLivedConsumerRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $consumerRootPath.StartsWith($longLivedConsumerPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "ConsumerRoot must not use a long-lived development consumer: $consumerRootPath"
    }
}
if ($consumerRootPath.Equals($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $consumerRootPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "ConsumerRoot must be outside the repository: $consumerRootPath"
}
if ($repositoryRoot.StartsWith($consumerPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "ConsumerRoot must not contain the repository: $consumerRootPath"
}

New-Item -ItemType Directory -Force -Path $consumerRootPath | Out-Null
$prospectiveOutputDirectory = Join-Path (Join-Path $consumerRootPath $PackageSource) $profileName
$failureReportPath = Join-Path $prospectiveOutputDirectory 'failure-report.json'
trap {
    $failureRecord = $_
    try {
        Write-FailureReport -Path $failureReportPath -ErrorRecord $failureRecord
        Write-Warning "Failure report: $failureReportPath"
    }
    catch {
        Write-Warning "Could not write failure report: $($_.Exception.Message)"
    }
    throw $failureRecord
}

function Test-LogPatternForbidden {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string]$Pattern
    )

    if ($Pattern -ne 'UnityEngine\.Debug:LogError') {
        return $Text -match $Pattern
    }

    $lines = @($Text -split "`r?`n")
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -notmatch $Pattern) {
            continue
        }
        $start = [Math]::Max(0, $index - 10)
        $end = [Math]::Min($lines.Count - 1, $index + 20)
        $window = ($lines[$start..$end] -join "`n")
        $knownUrpImportIssue =
            $window.Contains(
                'Host type is not matching any asset type at Path ' +
                'Packages/com.unity.render-pipelines.core/Editor/Lighting/' +
                'ProbeVolume/') -and
            $window.Contains('.urtshader.') -and
            $window.Contains(
                'UnityEditor.Rendering.Universal.' +
                'UniversalRenderPipelineGlobalSettingsPostprocessor:OnPostprocessAllAssets')
        if (-not $knownUrpImportIssue) {
            return $true
        }
    }
    return $false
}
if ($PackageSource -eq 'Local') {
    if (-not [string]::IsNullOrWhiteSpace($GitUrl) -or
        -not [string]::IsNullOrWhiteSpace($Revision)) {
        throw '-GitUrl and -Revision are only valid with -PackageSource PinnedGit.'
    }

    $packageReference = 'file:' + ($sourcePackageRoot -replace '\\', '/')
    $expectedResolvedSource = 'local'
    $requestedRevision = $null
    $generatedConsumer = $true
    $outputDirectory = Join-Path (Join-Path $consumerRootPath 'Local') $profileName
    $consumerProject = Join-Path $outputDirectory 'UnityProject'
}
else {
    if ([string]::IsNullOrWhiteSpace($Revision)) {
        throw 'PinnedGit requires -Revision with a full 40-character commit SHA.'
    }

    if ([string]::IsNullOrWhiteSpace($GitUrl)) {
        $GitUrl = [string]$sourcePackageManifest.repository.url
    }
    if ([string]::IsNullOrWhiteSpace($GitUrl)) {
        throw 'PinnedGit requires -GitUrl or package.json repository.url.'
    }

    $requestedRevision = $Revision.ToLowerInvariant()
    $packageReference = Get-PinnedGitReference -RepositoryUrl $GitUrl -Commit $requestedRevision
    $outputDirectory = Join-Path (Join-Path $consumerRootPath 'PinnedGit') $profileName
    $consumerProject = Join-Path $outputDirectory 'UnityProject'
    $expectedResolvedSource = 'git'
    $generatedConsumer = $true
}

$optionalPackageReference = $null
if (-not [string]::IsNullOrWhiteSpace($profileConfig.PackageName)) {
    if ($profileConfig.SourceKind -eq 'git-subpath') {
        if ($PackageSource -eq 'PinnedGit') {
            $optionalPackageReference = Get-PinnedGitReference `
                -RepositoryUrl $GitUrl `
                -Commit $requestedRevision `
                -PackagePath $profileConfig.PackageSubpath
        }
        else {
            $fairyGuiPackageRoot = [System.IO.Path]::GetFullPath(
                (Join-Path $repositoryRoot 'Packages\com.fairygui.gui'))
            $optionalPackageReference = 'file:' + ($fairyGuiPackageRoot -replace '\\', '/')
        }
    }
    else {
        $optionalPackageReference = $profileConfig.PackageVersion
    }
}

if ($generatedConsumer) {
    if (-not $ReuseLibrary) {
        Remove-ConsumerDirectory -Path $consumerProject -AllowedParent $outputDirectory
    }

    if (-not (Test-Path -LiteralPath $consumerProject -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $consumerProject | Out-Null
        Copy-Item -LiteralPath (Join-Path $consumerFixture 'Assets') `
            -Destination $consumerProject -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $consumerFixture 'ProjectSettings') `
            -Destination $consumerProject -Recurse -Force
    }

    $consumerPackagesDirectory = Join-Path $consumerProject 'Packages'
    New-Item -ItemType Directory -Force -Path $consumerPackagesDirectory | Out-Null
    $generatedDependencies = [ordered]@{}
    $generatedDependencies['com.unity.test-framework'] = '1.7.0'
    $generatedDependencies[$packageName] = $packageReference
    if (-not [string]::IsNullOrWhiteSpace($profileConfig.PackageName)) {
        $generatedDependencies[$profileConfig.PackageName] = $optionalPackageReference
    }
    $generatedManifest = [ordered]@{
        dependencies = $generatedDependencies
        testables = @($packageName)
    }
    $generatedManifestJson = $generatedManifest | ConvertTo-Json -Depth 4
    Write-Utf8WithoutBom `
        -Path (Join-Path $consumerPackagesDirectory 'manifest.json') `
        -Content $generatedManifestJson
    Remove-Item -LiteralPath (Join-Path $consumerPackagesDirectory 'packages-lock.json') `
        -Force -ErrorAction SilentlyContinue
}

$consumerProject = [System.IO.Path]::GetFullPath($consumerProject)
$outputDirectory = [System.IO.Path]::GetFullPath($outputDirectory)
if ($consumerProject -eq $mainProject) {
    throw 'Consumer smoke project must not share the main Unity project root.'
}

$expectedConsumerProject = if ($generatedConsumer) {
    [System.IO.Path]::GetFullPath((Join-Path $outputDirectory 'UnityProject'))
}
else {
    [System.IO.Path]::GetFullPath($consumerFixture)
}
if ($consumerProject -ne $expectedConsumerProject) {
    throw "Consumer smoke project resolved outside its expected boundary: $consumerProject"
}

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    if (-not [string]::IsNullOrWhiteSpace($env:MXFRAMEWORK_UNITY_PATH)) {
        $UnityPath = $env:MXFRAMEWORK_UNITY_PATH
    }
    else {
        $hubCandidate = Join-Path 'C:\Program Files\Unity\Hub\Editor' "$editorVersion\Editor\Unity.exe"
        $standaloneCandidate = Join-Path 'C:\Program Files' "Unity $editorVersion\Editor\Unity.exe"
        if (Test-Path -LiteralPath $hubCandidate -PathType Leaf) {
            $UnityPath = $hubCandidate
        }
        elseif (Test-Path -LiteralPath $standaloneCandidate -PathType Leaf) {
            $UnityPath = $standaloneCandidate
        }
        else {
            $unityCommand = Get-Command Unity.exe -ErrorAction SilentlyContinue
            if ($null -ne $unityCommand) {
                $UnityPath = $unityCommand.Source
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($UnityPath) -or
    -not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $editorVersion was not found. Pass -UnityPath or set MXFRAMEWORK_UNITY_PATH."
}

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$testResults = Join-Path $outputDirectory 'editmode-results.xml'
$logFile = Join-Path $outputDirectory 'unity.log'
$coldImportLog = Join-Path $outputDirectory 'cold-import.log'
$coldImportStabilizeLog = Join-Path $outputDirectory 'cold-import-stabilize.log'
$jsonReport = Join-Path $outputDirectory 'report.json'
$markdownReport = Join-Path $outputDirectory 'report.md'
$boundaryJsonReport = Join-Path $outputDirectory 'package-boundary.json'
$playerBuildOutputDirectory = Join-Path $outputDirectory 'player-build'
$playerBuildLog = Join-Path $outputDirectory 'player-build.log'
$playerBuildEvidencePath = Join-Path $outputDirectory 'player-build-report.json'

foreach ($outputFile in @(
    $testResults,
    $logFile,
    $coldImportLog,
    $coldImportStabilizeLog,
    $jsonReport,
    $markdownReport,
    $boundaryJsonReport,
    $playerBuildLog,
    $playerBuildEvidencePath,
    $failureReportPath)) {
    Remove-Item -LiteralPath $outputFile -Force -ErrorAction SilentlyContinue
}
if ($IncludePlayerBuild) {
    Remove-ConsumerDirectory `
        -Path $playerBuildOutputDirectory `
        -AllowedParent $outputDirectory
}

if ($PackageSource -eq 'Local' -and -not $ReuseLibrary) {
    foreach ($directoryName in @('Library', 'Logs', 'Temp', 'UserSettings')) {
        $generatedPath = Join-Path $consumerProject $directoryName
        Remove-ConsumerDirectory -Path $generatedPath -AllowedParent $consumerProject
    }
}

$candidatePackageRoot = if ($PackageSource -eq 'Local') { $sourcePackageRoot } else { $null }
$packageTreeSnapshotBefore = if ($PackageSource -eq 'Local') {
    Get-DirectoryContentSnapshot -Path $candidatePackageRoot
} else {
    $null
}
$fixtureSnapshotBefore = Get-DirectoryContentSnapshot -Path $consumerFixture
$fixtureHashBefore = Get-SnapshotHash -Snapshot $fixtureSnapshotBefore
$harnessSnapshotBefore = Get-DirectoryContentSnapshot -Path (Join-Path $repositoryRoot 'Tools\Harness')
$harnessHashBefore = Get-SnapshotHash -Snapshot $harnessSnapshotBefore
$runnerHashBefore = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$consumerAssetsPath = Join-Path $consumerProject 'Assets'
$consumerProjectSettingsPath = Join-Path $consumerProject 'ProjectSettings'

$consumerAssetsColdSnapshotBefore = Get-DirectoryContentSnapshot -Path $consumerAssetsPath
$coldImportArguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $consumerProject,
    '-quit',
    '-logFile', $coldImportLog
)
& $UnityPath @coldImportArguments | Out-Host
$coldImportExitCode = $LASTEXITCODE
if ($coldImportExitCode -ne 0) {
    throw "Unity cold import exited with code $coldImportExitCode. See $coldImportLog"
}

if (-not (Test-Path -LiteralPath $coldImportLog -PathType Leaf)) {
    throw 'Unity did not create the cold import log.'
}
$coldImportLogText = Get-Content -Raw -LiteralPath $coldImportLog
foreach ($pattern in @('\berror CS\d{4}:', 'UnityEngine\.Debug:LogError', 'Failed to resolve packages:')) {
    if (Test-LogPatternForbidden -Text $coldImportLogText -Pattern $pattern) {
        throw "Unity cold import log contains '$pattern'. See $coldImportLog"
    }
}
$consumerAssetsSnapshotBefore = Get-DirectoryContentSnapshot -Path $consumerAssetsPath
Assert-DirectoryContentUnchanged `
    -Before $consumerAssetsColdSnapshotBefore `
    -After $consumerAssetsSnapshotBefore `
    -Label 'Consumer Assets tree during cold import'
Write-Host 'Consumer cold import Assets mutation guard passed.'
if ($PackageSource -eq 'PinnedGit' -or
    $profileName -in @('with-unity-transport', 'with-vfx-graph')) {
    $vfxStabilizeArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $consumerProject,
        '-quit',
        '-logFile', $coldImportStabilizeLog
    )
    & $UnityPath @vfxStabilizeArguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Unity cold-import stabilization failed. See $coldImportStabilizeLog"
    }
    $vfxStabilizeLogText = Get-Content -Raw -LiteralPath $coldImportStabilizeLog
    foreach ($pattern in @('\berror CS\d{4}:', 'UnityEngine\.Debug:LogError', 'Failed to resolve packages:')) {
        if (Test-LogPatternForbidden -Text $vfxStabilizeLogText -Pattern $pattern) {
            throw "Unity cold-import stabilization log contains '$pattern'. See $coldImportStabilizeLog"
        }
    }
    $consumerAssetsAfterVfxStabilize = Get-DirectoryContentSnapshot -Path $consumerAssetsPath
    Assert-DirectoryContentUnchanged `
        -Before $consumerAssetsSnapshotBefore `
        -After $consumerAssetsAfterVfxStabilize `
        -Label 'Consumer Assets tree during cold-import stabilization'
    $consumerAssetsSnapshotBefore = $consumerAssetsAfterVfxStabilize
    Write-Host 'Consumer cold-import stabilization passed.'
}
if ($PackageSource -eq 'PinnedGit') {
    $candidatePackageRoots = @(
        Get-ChildItem -LiteralPath (Join-Path $consumerProject 'Library\PackageCache') `
            -Directory -Filter "$packageName@*" |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'package.json') -PathType Leaf } |
            Select-Object -ExpandProperty FullName
    )
    if ($candidatePackageRoots.Count -ne 1) {
        throw "Expected one resolved $packageName PackageCache directory, found $($candidatePackageRoots.Count)."
    }
    $candidatePackageRoot = $candidatePackageRoots[0]
    $packageTreeSnapshotBefore = Get-DirectoryContentSnapshot -Path $candidatePackageRoot
}
$consumerProjectSettingsSnapshotBefore = Get-DirectoryContentSnapshot -Path $consumerProjectSettingsPath

Write-Host "Unity: $UnityPath"
Write-Host "Package source: $PackageSource"
Write-Host "Profile: $profileName"
Write-Host "Consumer root: $consumerRootPath"
Write-Host "Package reference: $packageReference"
Write-Host "Consumer project: $consumerProject"
Write-Host "Results: $testResults"

$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $consumerProject,
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testFilter', 'MxFramework.PackageConsumerSmoke;MxFramework.PackageTests',
    '-testResults', $testResults,
    '-logFile', $logFile
)

# Some agent shells expose both Path and PATH. Normalize the runner process
# before Unity launches Git and Package Manager child processes on Windows.
$pathEntries = @([System.Environment]::GetEnvironmentVariables().GetEnumerator() | Where-Object {
    [string]::Equals([string]$_.Key, 'PATH', [System.StringComparison]::OrdinalIgnoreCase)
})
if ($pathEntries.Count -gt 1) {
    $pathSegments = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in $pathEntries) {
        foreach ($segment in ([string]$entry.Value -split ';')) {
            if (-not [string]::IsNullOrWhiteSpace($segment) -and
                -not $pathSegments.Contains($segment)) {
                $pathSegments.Add($segment)
            }
        }
    }

    [System.Environment]::SetEnvironmentVariable(
        'PATH',
        $null,
        [System.EnvironmentVariableTarget]::Process)
    [System.Environment]::SetEnvironmentVariable(
        'Path',
        ($pathSegments -join ';'),
        [System.EnvironmentVariableTarget]::Process)
}

& $UnityPath @unityArguments | Out-Host
$unityExitCode = $LASTEXITCODE
$consumerAssetsSnapshotAfter = Get-DirectoryContentSnapshot -Path $consumerAssetsPath
Assert-DirectoryContentUnchanged `
    -Before $consumerAssetsSnapshotBefore `
    -After $consumerAssetsSnapshotAfter `
    -Label 'Consumer Assets tree'
Write-Host 'Consumer Assets mutation guard passed.'
$consumerProjectSettingsSnapshotAfter = Get-DirectoryContentSnapshot -Path $consumerProjectSettingsPath
$projectSettingsAllowlist = if ($profileName -eq 'with-vfx-graph') {
    @('VFXManager.asset')
} else {
    @()
}
Assert-DirectoryContentUnchanged `
    -Before $consumerProjectSettingsSnapshotBefore `
    -After $consumerProjectSettingsSnapshotAfter `
    -Label 'Consumer ProjectSettings tree' `
    -AllowedPaths $projectSettingsAllowlist
Write-Host 'Consumer ProjectSettings mutation guard passed.'
if ($PackageSource -eq 'Local') {
    $packageTreeSnapshotAfter = Get-DirectoryContentSnapshot -Path $sourcePackageRoot
    Assert-DirectoryContentUnchanged `
        -Before $packageTreeSnapshotBefore `
        -After $packageTreeSnapshotAfter `
        -Label 'MxFramework package tree'
    Write-Host 'Package tree mutation guard passed.'
}
else {
    $packageTreeSnapshotAfter = Get-DirectoryContentSnapshot -Path $candidatePackageRoot
    Assert-DirectoryContentUnchanged `
        -Before $packageTreeSnapshotBefore `
        -After $packageTreeSnapshotAfter `
        -Label 'Pinned package cache tree'
    Write-Host 'Pinned package tree mutation guard passed.'
}
$fixtureSnapshotAfter = Get-DirectoryContentSnapshot -Path $consumerFixture
Assert-DirectoryContentUnchanged `
    -Before $fixtureSnapshotBefore `
    -After $fixtureSnapshotAfter `
    -Label 'Consumer fixture tree'
$runnerHashAfter = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($runnerHashBefore -ne $runnerHashAfter) {
    throw 'Consumer runner changed during the Unity Consumer run.'
}
if ($unityExitCode -ne 0) {
    throw "Unity consumer smoke exited with code $unityExitCode. See $logFile"
}

if (-not (Test-Path -LiteralPath $logFile -PathType Leaf)) {
    throw 'Unity did not create the consumer smoke log.'
}

$unityLog = Get-Content -Raw -LiteralPath $logFile
$forbiddenLogPatterns = [ordered]@{
    'C# compiler error' = '\berror CS\d{4}:'
    'Unity LogError' = 'UnityEngine\.Debug:LogError'
    'package resolution failure' = 'Failed to resolve packages:'
}
foreach ($logPattern in $forbiddenLogPatterns.GetEnumerator()) {
    if (Test-LogPatternForbidden -Text $unityLog -Pattern $logPattern.Value) {
        throw "Consumer smoke log contains $($logPattern.Key). See $logFile"
    }
}

if (-not (Test-Path -LiteralPath $testResults -PathType Leaf)) {
    throw "Unity did not create test results. See $logFile"
}

[xml]$results = Get-Content -Raw -LiteralPath $testResults
$failed = $results.SelectNodes('//*[@result="Failed"]')
if ($null -ne $failed -and $failed.Count -gt 0) {
    throw "Consumer smoke reported $($failed.Count) failed result node(s). See $testResults"
}

$packageLockPath = Join-Path $consumerProject 'Packages\packages-lock.json'
if (-not (Test-Path -LiteralPath $packageLockPath -PathType Leaf)) {
    throw "Consumer package lock was not generated. See $logFile"
}

$packageLock = Get-Content -Raw -LiteralPath $packageLockPath | ConvertFrom-Json
$resolvedPackage = $packageLock.dependencies.PSObject.Properties[$packageName].Value
if ($null -eq $resolvedPackage -or $resolvedPackage.source -ne $expectedResolvedSource) {
    throw "Consumer did not resolve $packageName from expected source '$expectedResolvedSource'."
}

$resolvedRevision = $null
if ($PackageSource -eq 'PinnedGit') {
    $resolvedRevision = [string]$resolvedPackage.hash
    if ($resolvedRevision -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Pinned Git package lock does not contain a full resolved commit hash.'
    }

    $resolvedRevision = $resolvedRevision.ToLowerInvariant()
    if ($resolvedRevision -ne $requestedRevision) {
        throw "Pinned Git resolved '$resolvedRevision', expected '$requestedRevision'."
    }

    $resolvedPackageRoot = $candidatePackageRoot
}
else {
    $resolvedPackageRoot = $sourcePackageRoot
}

$packageValidationArguments = @{
    PackageRoot = $resolvedPackageRoot
    ReportPath = $boundaryJsonReport
}
if (-not [string]::IsNullOrWhiteSpace($resolvedRevision)) {
    $packageValidationArguments.CandidateCommit = $resolvedRevision
}
$packageValidation = Test-PackageStructure @packageValidationArguments
$observedPackageManifest = $packageValidation.Manifest
$declaredDependencies = @($packageValidation.DeclaredDependencies)
$optionalGateAssemblyCounts = $packageValidation.OptionalGateAssemblyCounts
$boundaryReport = $packageValidation.BoundaryReport

$resolvedDependencyNames = @($resolvedPackage.dependencies.PSObject.Properties.Name)
$missingLockedDependencies = @(
    $declaredDependencies |
        Where-Object { $_ -notin $resolvedDependencyNames }
)
$staleLockedDependencies = @(
    $resolvedDependencyNames |
        Where-Object { $_ -notin $declaredDependencies }
)
if ($missingLockedDependencies.Count -gt 0 -or $staleLockedDependencies.Count -gt 0) {
    throw ('Consumer lock does not match the resolved package.json dependencies. ' +
        'Missing: [{0}]. Stale: [{1}].') -f
        ($missingLockedDependencies -join ', '), ($staleLockedDependencies -join ', ')
}

foreach ($dependencyName in $declaredDependencies) {
    if ($null -eq $packageLock.dependencies.PSObject.Properties[$dependencyName]) {
        throw "Declared dependency '$dependencyName' is absent from the resolved consumer lock."
    }
}

$optionalPackages = @(
    $optionalIntegrationsAuthority.integrations |
        ForEach-Object { [string]$_.packageName } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
)
$resolvedOptionalPackage = $null
foreach ($optionalPackage in $optionalPackages) {
    $optionalLockProperty = $packageLock.dependencies.PSObject.Properties[$optionalPackage]
    if ($optionalPackage -eq $profileConfig.PackageName) {
        if ($null -eq $optionalLockProperty) {
            throw "Consumer profile '$profileName' did not resolve required optional package '$optionalPackage'."
        }

        $resolvedOptionalPackage = $optionalLockProperty.Value
        continue
    }

    if ($null -ne $optionalLockProperty) {
        throw "Consumer profile '$profileName' unexpectedly resolved optional package '$optionalPackage'."
    }
}

$resolvedOptionalVersion = $null
if ($null -ne $resolvedOptionalPackage) {
    if ($profileConfig.SourceKind -eq 'git-subpath') {
        if ($PackageSource -eq 'PinnedGit') {
            if ($resolvedOptionalPackage.source -ne 'git') {
                throw "Pinned FairyGUI profile resolved source '$($resolvedOptionalPackage.source)', expected 'git'."
            }

            $resolvedFairyGuiRevision = ([string]$resolvedOptionalPackage.hash).ToLowerInvariant()
            if ($resolvedFairyGuiRevision -ne $requestedRevision) {
                throw "Pinned FairyGUI resolved '$resolvedFairyGuiRevision', expected '$requestedRevision'."
            }

            $fairyGuiPackageCacheRoots = @(
                Get-ChildItem -LiteralPath (Join-Path $consumerProject 'Library\PackageCache') `
                    -Directory -Filter "$($profileConfig.PackageName)@*" |
                    Where-Object {
                        Test-Path -LiteralPath (Join-Path $_.FullName 'package.json') -PathType Leaf
                    } |
                    Select-Object -ExpandProperty FullName
            )
            if ($fairyGuiPackageCacheRoots.Count -ne 1) {
                throw "Expected one resolved FairyGUI PackageCache directory, found $($fairyGuiPackageCacheRoots.Count)."
            }

            $resolvedOptionalManifest = Get-Content -Raw -LiteralPath (
                Join-Path $fairyGuiPackageCacheRoots[0] 'package.json') | ConvertFrom-Json
        }
        else {
            if ($resolvedOptionalPackage.source -ne 'local') {
                throw "Local FairyGUI profile resolved source '$($resolvedOptionalPackage.source)', expected 'local'."
            }

            $resolvedOptionalManifest = Get-Content -Raw -LiteralPath (
                Join-Path $fairyGuiPackageRoot 'package.json') | ConvertFrom-Json
        }

        if ($resolvedOptionalManifest.name -ne $profileConfig.PackageName) {
            throw "FairyGUI profile observed package '$($resolvedOptionalManifest.name)', expected '$($profileConfig.PackageName)'."
        }
        $resolvedOptionalVersion = [string]$resolvedOptionalManifest.version
    }
    else {
        $resolvedOptionalVersion = [string]$resolvedOptionalPackage.version
    }

    if ($resolvedOptionalVersion -ne $profileConfig.PackageVersion) {
        throw "Consumer profile '$profileName' resolved optional version '$resolvedOptionalVersion', expected '$($profileConfig.PackageVersion)'."
    }
}

$adapterAssemblyCompiled = $null
if (-not [string]::IsNullOrWhiteSpace($profileConfig.AdapterAssembly)) {
    $adapterAssemblyPath = Join-Path $consumerProject (
        'Library\ScriptAssemblies\' + $profileConfig.AdapterAssembly + '.dll')
    if (-not (Test-Path -LiteralPath $adapterAssemblyPath -PathType Leaf)) {
        throw "Consumer profile '$profileName' did not compile adapter assembly '$($profileConfig.AdapterAssembly)'."
    }

    $adapterAssemblyCompiled = $true
}

$optionalAdapterAssemblies = @(
    $optionalIntegrationsAuthority.integrations |
        ForEach-Object { @($_.adapterAssemblies) } |
        ForEach-Object { [string]$_ }
)
foreach ($optionalAssembly in $optionalAdapterAssemblies) {
    $assemblyPath = Join-Path $consumerProject ('Library\ScriptAssemblies\' + $optionalAssembly + '.dll')
    $expected = [string]::Equals(
        $optionalAssembly,
        [string]$profileConfig.AdapterAssembly,
        [System.StringComparison]::Ordinal)
    if ($expected -and -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Consumer profile '$profileName' did not compile expected adapter '$optionalAssembly'."
    }
    if (-not $expected -and (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Consumer profile '$profileName' unexpectedly compiled optional adapter '$optionalAssembly'."
    }
}

$testRun = $results.'test-run'
$testTotal = [int]$testRun.total
$expectedTests = @(
    'MxFramework.PackageTests.Runtime.RuntimeHostPackageContractTests.Lifecycle_StartTickStop_DispatchesInOrder',
    'MxFramework.PackageTests.Runtime.RuntimeHostPackageContractTests.CommandEnvelope_RoundTripsThroughBuffer',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.RepresentativeStyle_ResolvesAndLoads',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.PortableStyleCallers_ResolveThroughLocator',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.UnsafeRelativePaths_AreRejected',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.EditorAssembly_DoesNotFriendProjectOnlyFairyGuiAssembly',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.FairyGuiArtSliceEditorSurface_IsPublicConsumerContract',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.EditorProjectPaths_UseRegisteredHostProvider',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.VfxBootstrap_AutomaticDiscoveryDoesNotLogErrors',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.PaletteGenerator_MissingSourceDoesNotScheduleAssetWrites',
    'MxFramework.PackageTests.Editor.PackageAssetLocatorContractTests.PaletteGenerator_ExistingSourceSchedulesOnlyMissingOutputs',
    'MxFramework.PackageConsumerSmoke.RuntimeHostConsumerSmokeTests.BasePackage_CanRunRuntimeHostLifecycle',
    'MxFramework.PackageConsumerSmoke.EditorPackageConsumerSmokeTests.InstalledPackage_ExposesRepresentativeEditorAssets',
    'MxFramework.PackageConsumerSmoke.EditorPackageConsumerSmokeTests.InstalledPackage_PreservesRepresentativeEditorAssetGuids',
    'MxFramework.PackageConsumerSmoke.EditorPackageConsumerSmokeTests.LevelEditor_CreateGui_UsesInstalledPackageStyle',
    'MxFramework.PackageConsumerSmoke.EditorPackageConsumerSmokeTests.ProjectileAuthoring_CreateGui_UsesInstalledPackageStyle',
    'MxFramework.PackageConsumerSmoke.EditorPackageConsumerSmokeTests.ProjectileAuthoring_WithoutRepositoryTools_ReportsUnavailable',
    'MxFramework.PackageConsumerSmoke.EditorPackageConsumerSmokeTests.VfxAuthoring_CreateGuiWithoutCatalog_ReportsUnavailable',
    'MxFramework.PackageConsumerSmoke.EditorPackageConsumerSmokeTests.Fixture_ExcludesProjectOnlyEditorInputs',
    'MxFramework.PackageConsumerSmoke.EditorPackageConsumerSmokeTests.FairyGuiCatalog_ProjectOnlyWindowIsNotInstalled'
)
if (-not [string]::IsNullOrWhiteSpace($profileConfig.BehaviorTest)) {
    $expectedTests += $profileConfig.BehaviorTest
}
if ($testTotal -ne $expectedTests.Count) {
    throw "Consumer profile '$profileName' executed $testTotal tests; expected $($expectedTests.Count). See $testResults"
}
foreach ($expectedTest in $expectedTests) {
    $matchingTest = $results.SelectSingleNode("//test-case[@fullname='$expectedTest']")
    if ($null -eq $matchingTest -or $matchingTest.result -ne 'Passed') {
        throw "Consumer profile '$profileName' did not pass expected test '$expectedTest'. See $testResults"
    }
}

$playerBuildReport = [ordered]@{
    requested = [bool]$IncludePlayerBuild
    passed = $null
    target = $null
    result = $null
    outputPath = $null
    totalSizeBytes = $null
    durationSeconds = $null
    errors = $null
    warnings = $null
}
if ($IncludePlayerBuild) {
    New-Item -ItemType Directory -Force -Path $playerBuildOutputDirectory | Out-Null
    $buildDirectoryVariable = 'MXFRAMEWORK_CONSUMER_PLAYER_BUILD_DIRECTORY'
    $buildReportVariable = 'MXFRAMEWORK_CONSUMER_PLAYER_BUILD_REPORT'
    $processEnvironment = [System.EnvironmentVariableTarget]::Process
    $previousBuildDirectory = [System.Environment]::GetEnvironmentVariable(
        $buildDirectoryVariable,
        $processEnvironment)
    $previousBuildReport = [System.Environment]::GetEnvironmentVariable(
        $buildReportVariable,
        $processEnvironment)
    [System.Environment]::SetEnvironmentVariable(
        $buildDirectoryVariable,
        $playerBuildOutputDirectory,
        $processEnvironment)
    [System.Environment]::SetEnvironmentVariable(
        $buildReportVariable,
        $playerBuildEvidencePath,
        $processEnvironment)

    $playerBuildArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $consumerProject,
        '-executeMethod', 'MxFramework.PackageConsumerSmoke.PackageConsumerPlayerBuild.Build',
        '-quit',
        '-logFile', $playerBuildLog
    )

    Write-Host "Player Build log: $playerBuildLog"
    $playerBuildExitCode = $null
    try {
        & $UnityPath @playerBuildArguments | Out-Host
        $playerBuildExitCode = $LASTEXITCODE
    }
    finally {
        [System.Environment]::SetEnvironmentVariable(
            $buildDirectoryVariable,
            $previousBuildDirectory,
            $processEnvironment)
        [System.Environment]::SetEnvironmentVariable(
            $buildReportVariable,
            $previousBuildReport,
            $processEnvironment)
    }

    $consumerAssetsSnapshotAfterPlayerBuild = Get-DirectoryContentSnapshot -Path $consumerAssetsPath
    Assert-DirectoryContentUnchanged `
        -Before $consumerAssetsSnapshotBefore `
        -After $consumerAssetsSnapshotAfterPlayerBuild `
        -Label 'Consumer Assets tree after Player Build'
    Write-Host 'Consumer Assets Player Build mutation guard passed.'
    if ($PackageSource -eq 'Local') {
        $packageTreeSnapshotAfterPlayerBuild = Get-DirectoryContentSnapshot -Path $sourcePackageRoot
        Assert-DirectoryContentUnchanged `
            -Before $packageTreeSnapshotBefore `
            -After $packageTreeSnapshotAfterPlayerBuild `
            -Label 'MxFramework package tree after Player Build'
        Write-Host 'Package tree Player Build mutation guard passed.'
    }
    else {
        $packageTreeSnapshotAfterPlayerBuild = Get-DirectoryContentSnapshot -Path $candidatePackageRoot
        Assert-DirectoryContentUnchanged `
            -Before $packageTreeSnapshotBefore `
            -After $packageTreeSnapshotAfterPlayerBuild `
            -Label 'Pinned package cache tree after Player Build'
        Write-Host 'Pinned package tree Player Build mutation guard passed.'
    }

    if ($playerBuildExitCode -ne 0) {
        throw "Unity Consumer Player Build exited with code $playerBuildExitCode. See $playerBuildLog"
    }
    if (-not (Test-Path -LiteralPath $playerBuildLog -PathType Leaf)) {
        throw 'Unity did not create the Consumer Player Build log.'
    }

    $playerBuildLogContent = Get-Content -Raw -LiteralPath $playerBuildLog
    foreach ($logPattern in $forbiddenLogPatterns.GetEnumerator()) {
        if (Test-LogPatternForbidden -Text $playerBuildLogContent -Pattern $logPattern.Value) {
            throw "Consumer Player Build log contains $($logPattern.Key). See $playerBuildLog"
        }
    }
    if (-not (Test-Path -LiteralPath $playerBuildEvidencePath -PathType Leaf)) {
        throw "Unity did not create Player Build evidence. See $playerBuildLog"
    }

    $playerBuildEvidence = Get-Content -Raw -LiteralPath $playerBuildEvidencePath | ConvertFrom-Json
    if ($playerBuildEvidence.status -ne 'Passed' -or
        $playerBuildEvidence.result -ne 'Succeeded' -or
        [int]$playerBuildEvidence.errors -ne 0) {
        throw "Consumer Player Build evidence is not passing. See $playerBuildEvidencePath"
    }

    $reportedPlayerOutput = [System.IO.Path]::GetFullPath(
        [string]$playerBuildEvidence.outputPath)
    $playerBuildOutputPrefix = [System.IO.Path]::GetFullPath(
        $playerBuildOutputDirectory).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $reportedPlayerOutput.StartsWith(
            $playerBuildOutputPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Consumer Player Build output escaped its expected directory: $reportedPlayerOutput"
    }
    if (-not (Test-Path -LiteralPath $reportedPlayerOutput)) {
        throw "Consumer Player Build output is missing: $reportedPlayerOutput"
    }

    $playerBuildReport = [ordered]@{
        requested = $true
        passed = $true
        target = [string]$playerBuildEvidence.target
        result = [string]$playerBuildEvidence.result
        outputPath = $reportedPlayerOutput
        totalSizeBytes = [string]$playerBuildEvidence.totalSizeBytes
        durationSeconds = [string]$playerBuildEvidence.durationSeconds
        errors = [int]$playerBuildEvidence.errors
        warnings = [int]$playerBuildEvidence.warnings
    }
    Write-Host "Consumer Player Build passed: $($playerBuildReport.target)."
}

$repositoryCommit = (git -C $repositoryRoot rev-parse HEAD).Trim()
$repositoryDirty = @((git -C $repositoryRoot status --porcelain --untracked-files=all)).Count -gt 0
$sourceLabel = if ($PackageSource -eq 'PinnedGit') { 'pinned-git' } else { 'local' }
$candidateDirty = if ($PackageSource -eq 'Local') {
    @((git -C $repositoryRoot status --porcelain --untracked-files=all -- `
        'Packages/com.vincent.wgameframework')).Count -gt 0
} else {
    $false
}
$cleanLibrary = -not [bool]$ReuseLibrary
$eligibleForMatrix = $cleanLibrary -and -not $candidateDirty
$candidateCommitForTree = if ($PackageSource -eq 'PinnedGit') {
    $resolvedRevision
} else {
    $repositoryCommit
}
$pythonExecutable = Resolve-PythonExecutable
$canonicalPackageTreeHash = (& $pythonExecutable $gitPackageTreeHasher `
    '--repository' $repositoryRoot `
    '--revision' $candidateCommitForTree `
    '--package-subpath' 'Packages/com.vincent.wgameframework').Trim()
if ($LASTEXITCODE -ne 0 -or $canonicalPackageTreeHash -notmatch '^[0-9a-f]{64}$') {
    throw 'Failed to compute the canonical Git package tree hash.'
}

$observedTestCases = @($results.SelectNodes('//test-case'))
$observedFullNames = @(
    $observedTestCases |
        ForEach-Object { [string]$_.fullname } |
        Sort-Object
)
$passedTestCount = @($observedTestCases | Where-Object { $_.result -eq 'Passed' }).Count
$failedTestCount = @($observedTestCases | Where-Object { $_.result -eq 'Failed' }).Count
$skippedTestCount = @($observedTestCases | Where-Object {
    $_.result -in @('Skipped', 'Ignored', 'Inconclusive')
}).Count

$optionalIntegrationReports = @(
    foreach ($integration in @($optionalIntegrationsAuthority.integrations)) {
        $isSelected = -not [string]::IsNullOrWhiteSpace([string]$integration.profile) -and
            [string]$integration.profile -eq $profileName
        $optionalResolvedRevision = $null
        if ($isSelected -and $null -ne $resolvedOptionalPackage -and
            $null -ne $resolvedOptionalPackage.PSObject.Properties['hash']) {
            $optionalResolvedRevision = [string]$resolvedOptionalPackage.hash
        }
        $adapterDlls = @(
            foreach ($assemblyName in @($integration.adapterAssemblies)) {
                $assemblyPath = Join-Path $consumerProject "Library\ScriptAssemblies\$assemblyName.dll"
                [ordered]@{
                    name = "$assemblyName.dll"
                    compiled = [bool](Test-Path -LiteralPath $assemblyPath -PathType Leaf)
                    sha256 = if (Test-Path -LiteralPath $assemblyPath -PathType Leaf) {
                        (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash.ToLowerInvariant()
                    } else {
                        $null
                    }
                }
            }
        )
        [ordered]@{
            id = [string]$integration.id
            profile = if ([string]::IsNullOrWhiteSpace([string]$integration.profile)) { $null } else { [string]$integration.profile }
            requested = $isSelected
            packageName = if ([string]::IsNullOrWhiteSpace([string]$integration.packageName)) { $null } else { [string]$integration.packageName }
            sourceKind = [string]$integration.sourceKind
            minimumVersion = if ([string]::IsNullOrWhiteSpace([string]$integration.minimumVersion)) { $null } else { [string]$integration.minimumVersion }
            requestedVersion = if ($isSelected) { [string]$integration.validatedVersion } else { $null }
            validatedVersion = if ([string]::IsNullOrWhiteSpace([string]$integration.validatedVersion)) { $null } else { [string]$integration.validatedVersion }
            resolvedSource = if ($isSelected) { [string]$resolvedOptionalPackage.source } else { $null }
            resolvedVersion = if ($isSelected) { $resolvedOptionalVersion } else { $null }
            resolvedRevision = $optionalResolvedRevision
            define = [string]$integration.define
            adapterAssemblies = @($integration.adapterAssemblies)
            adapterDlls = $adapterDlls
            behaviorTest = if ([string]::IsNullOrWhiteSpace([string]$integration.behaviorTest)) { $null } else { [string]$integration.behaviorTest }
            behaviorPassed = if ($isSelected) { $true } else { $null }
            supportTier = [string]$integration.supportTier
        }
    }
)

$artifacts = @(
    Get-FileArtifact -Path $boundaryJsonReport -Id 'package-boundary.json' -Type 'static-boundary-report'
    Get-FileArtifact -Path $testResults -Id 'editmode-results.xml' -Type 'nunit-xml'
    Get-FileArtifact -Path $logFile -Id 'unity.log' -Type 'unity-log'
    Get-FileArtifact -Path $coldImportLog -Id 'cold-import.log' -Type 'unity-log'
    if (Test-Path -LiteralPath $coldImportStabilizeLog -PathType Leaf) {
        Get-FileArtifact -Path $coldImportStabilizeLog -Id 'cold-import-stabilize.log' -Type 'unity-log'
    }
    if ($IncludePlayerBuild) {
        Get-FileArtifact -Path $playerBuildEvidencePath -Id 'player-build-report.json' -Type 'player-build-report'
        Get-FileArtifact -Path $playerBuildLog -Id 'player-build.log' -Type 'unity-log'
    }
    if ($profileName -ne 'base') {
        foreach ($assemblyName in @($profileConfig.AdapterAssembly)) {
            Get-FileArtifact `
                -Path (Join-Path $consumerProject "Library\ScriptAssemblies\$assemblyName.dll") `
                -Id "adapter/$assemblyName.dll" `
                -Type 'adapter-assembly'
        }
    }
)
$playerBuildArtifact = @($artifacts | Where-Object { $_.id -eq 'player-build-report.json' }) |
    Select-Object -First 1

$packageTreeSnapshotFinal = Get-DirectoryContentSnapshot -Path $candidatePackageRoot
$fixtureSnapshotFinal = Get-DirectoryContentSnapshot -Path $consumerFixture
$harnessSnapshotFinal = Get-DirectoryContentSnapshot -Path (Join-Path $repositoryRoot 'Tools\Harness')
$consumerAssetsSnapshotFinal = Get-DirectoryContentSnapshot -Path $consumerAssetsPath
$consumerProjectSettingsSnapshotFinal = Get-DirectoryContentSnapshot -Path $consumerProjectSettingsPath
Assert-DirectoryContentUnchanged `
    -Before $consumerAssetsSnapshotBefore `
    -After $consumerAssetsSnapshotFinal `
    -Label 'Consumer Assets tree at report finalization'
Assert-DirectoryContentUnchanged `
    -Before $consumerProjectSettingsSnapshotBefore `
    -After $consumerProjectSettingsSnapshotFinal `
    -Label 'Consumer ProjectSettings tree at report finalization' `
    -AllowedPaths $projectSettingsAllowlist
Assert-DirectoryContentUnchanged `
    -Before $fixtureSnapshotBefore `
    -After $fixtureSnapshotFinal `
    -Label 'Consumer fixture tree at report finalization'
Assert-DirectoryContentUnchanged `
    -Before $harnessSnapshotBefore `
    -After $harnessSnapshotFinal `
    -Label 'Harness tree at report finalization'
$runnerHashFinal = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($runnerHashBefore -ne $runnerHashFinal) {
    throw 'Consumer runner changed before report finalization.'
}
$forbiddenPatternReports = @(
    foreach ($entry in $forbiddenLogPatterns.GetEnumerator()) {
        [ordered]@{ id = [string]$entry.Key; pattern = [string]$entry.Value }
    }
)
$report = [ordered]@{
    schemaVersion = 4
    reportType = 'profile'
    candidate = [ordered]@{
        sourceMode = $sourceLabel
        requestedCommit = $requestedRevision
        resolvedCommit = $resolvedRevision
        packageTreeHash = $canonicalPackageTreeHash
        packageTreeHashAlgorithm = 'sha256-path-nul-content-nul-v1'
        packageName = [string]$observedPackageManifest.name
        packageVersion = [string]$observedPackageManifest.version
        resolvedPackageSource = [string]$resolvedPackage.source
        localSourceDirty = if ($PackageSource -eq 'Local') { $candidateDirty } else { $null }
    }
    harness = [ordered]@{
        repositoryCommit = $repositoryCommit
        repositoryDirty = $repositoryDirty
        fixtureHash = $fixtureHashBefore
        controlledStateHash = $harnessHashBefore
        runnerHash = $runnerHashBefore
        runnerVersion = 4
        schemaVersion = 4
    }
    execution = [ordered]@{
        profile = $profileName
        unityVersion = $editorVersion
        platform = [ordered]@{
            operatingSystem = [System.Environment]::OSVersion.VersionString
            pointerSizeBits = [System.IntPtr]::Size * 8
            testPlatform = 'EditMode'
        }
        cleanLibrary = $cleanLibrary
        evidenceLevel = if ($PackageSource -eq 'PinnedGit') { 'E2-row' } else { 'E1' }
        eligibleForMatrix = $eligibleForMatrix
        result = 'passed'
        failureClassification = $null
        resolvedPackageTreeHash = [string]$boundaryReport.package.treeHash
        resolvedPackageTreeHashAlgorithm = [string]$boundaryReport.package.treeHashAlgorithm
    }
    dependencyResolution = [ordered]@{
        source = $resolvedPackage.source
        declaredDependencyCount = @($observedPackageManifest.dependencies.PSObject.Properties).Count
        optionalPackagesAbsent = ($profileName -eq 'base')
        optionalGateAssemblyCounts = $optionalGateAssemblyCounts
        staticBoundaryPolicyVersion = [int]$boundaryReport.policy.version
        staticBoundaryReadinessGapCount = [int]$boundaryReport.result.readinessGapCount
    }
    compile = [ordered]@{
        passed = $true
    }
    tests = [ordered]@{
        expectedFullNames = @($expectedTests)
        observedFullNames = $observedFullNames
        total = $testTotal
        passed = $passedTestCount
        failed = $failedTestCount
        skipped = $skippedTestCount
        complete = ($observedFullNames.Count -eq $expectedTests.Count -and
            @($expectedTests | Where-Object { $_ -notin $observedFullNames }).Count -eq 0)
    }
    optionalIntegrations = $optionalIntegrationReports
    player = [ordered]@{
        buildRequested = [bool]$IncludePlayerBuild
        buildPassed = if ($IncludePlayerBuild) { [bool]$playerBuildReport.passed } else { $null }
        runRequested = $false
        runPassed = $null
        buildReportHash = if ($null -ne $playerBuildArtifact) { [string]$playerBuildArtifact.sha256 } else { $null }
        runReportHash = $null
        target = if ($IncludePlayerBuild) { [string]$playerBuildReport.target } else { $null }
    }
    artifacts = $artifacts
    mutationGuard = [ordered]@{
        policyVersion = 1
        allowedChanges = @(
            'consumer-generated/Library/**',
            'consumer-generated/Logs/**',
            'consumer-generated/Temp/**',
            'consumer-generated/UserSettings/**',
            'consumer-generated/Packages/manifest.json',
            'consumer-generated/Packages/packages-lock.json',
            'consumer-generated/ProjectSettings/VFXManager.asset (with-vfx-graph only)'
        )
        packageCandidate = [ordered]@{
            beforeHash = Get-SnapshotHash -Snapshot $packageTreeSnapshotBefore
            afterHash = Get-SnapshotHash -Snapshot $packageTreeSnapshotFinal
            verdict = 'passed'
        }
        harnessRunner = [ordered]@{
            beforeHash = $runnerHashBefore
            afterHash = $runnerHashFinal
            verdict = 'passed'
        }
        harnessControlledTree = [ordered]@{
            beforeHash = $harnessHashBefore
            afterHash = Get-SnapshotHash -Snapshot $harnessSnapshotFinal
            verdict = 'passed'
        }
        consumerFixture = [ordered]@{
            beforeHash = $fixtureHashBefore
            afterHash = Get-SnapshotHash -Snapshot $fixtureSnapshotFinal
            verdict = 'passed'
        }
        generatedConsumerAuthority = [ordered]@{
            beforeHash = Get-TextHash -Text ((Get-SnapshotHash -Snapshot $consumerAssetsSnapshotBefore) + "`n" + (Get-SnapshotHash -Snapshot $consumerProjectSettingsSnapshotBefore -ExcludedPaths $projectSettingsAllowlist))
            afterHash = Get-TextHash -Text ((Get-SnapshotHash -Snapshot $consumerAssetsSnapshotFinal) + "`n" + (Get-SnapshotHash -Snapshot $consumerProjectSettingsSnapshotFinal -ExcludedPaths $projectSettingsAllowlist))
            verdict = 'passed'
        }
        verdict = 'passed'
    }
    logPolicy = [ordered]@{
        policy = 'upm-consumer-forbidden-log-patterns'
        version = 1
        patterns = $forbiddenPatternReports
        allowedInfrastructureSignatures = @(
            'unity-urp-probe-volume-urtshader-host-type-import'
        )
        checkedArtifactIds = @($artifacts | Where-Object { $_.type -eq 'unity-log' } | ForEach-Object { $_.id })
        hits = @()
        verdict = 'passed'
    }
}

$reportJson = $report | ConvertTo-Json -Depth 12
Write-Utf8WithoutBom -Path $jsonReport -Content $reportJson
$markdownLines = @(
    '# MxFramework UPM Consumer Smoke',
    '',
    "- Schema: $($report.schemaVersion)",
    "- Profile: $($report.execution.profile)",
    "- Evidence level: $($report.execution.evidenceLevel)",
    "- Source mode: $($report.candidate.sourceMode)",
    "- Unity: $($report.execution.unityVersion)",
    "- Package: $($report.candidate.packageName) $($report.candidate.packageVersion)",
    "- Requested revision: $($report.candidate.requestedCommit)",
    "- Resolved revision: $($report.candidate.resolvedCommit)",
    "- Repository commit: $($report.harness.repositoryCommit)",
    "- Repository dirty: $($report.harness.repositoryDirty.ToString().ToLowerInvariant())",
    "- Dependency resolution: $($report.dependencyResolution.source), $($report.dependencyResolution.declaredDependencyCount) declared dependencies",
    "- Optional packages absent: $($report.dependencyResolution.optionalPackagesAbsent.ToString().ToLowerInvariant())",
    "- Optional asmdef closure: FMOD $($optionalGateAssemblyCounts.fmod), FairyGUI $($optionalGateAssemblyCounts.fairygui), Unity Transport $($optionalGateAssemblyCounts.'unity-transport'), VFX Graph $($optionalGateAssemblyCounts.'vfx-graph')",
    "- Static boundary: policy $($report.dependencyResolution.staticBoundaryPolicyVersion), $($report.dependencyResolution.staticBoundaryReadinessGapCount) readiness gaps",
    "- Package tree hash: $($report.candidate.packageTreeHashAlgorithm) $($report.candidate.packageTreeHash)",
    '- Compile: passed',
    "- Tests: $($report.tests.passed)/$($report.tests.total) passed",
    "- Player Build requested: $($IncludePlayerBuild.ToString().ToLowerInvariant())"
)
if ($IncludePlayerBuild) {
    $markdownLines += @(
        "- Player Build: $($playerBuildReport.target) $($playerBuildReport.result)",
        "- Player Build duration: $($playerBuildReport.durationSeconds) seconds",
        "- Player Build warnings: $($playerBuildReport.warnings)"
    )
}
if ($profileName -ne 'base') {
    $selectedOptionalReport = @($optionalIntegrationReports | Where-Object { $_.requested }) |
        Select-Object -First 1
    $markdownLines += @(
        "- Optional package: $($selectedOptionalReport.packageName) $($selectedOptionalReport.resolvedVersion) ($($selectedOptionalReport.resolvedSource))",
        "- Adapter assembly: $($selectedOptionalReport.adapterAssemblies -join ', ') compiled",
        "- Adapter behavior: $($selectedOptionalReport.behaviorTest) passed"
    )
}
Write-Utf8WithoutBom -Path $markdownReport -Content ($markdownLines -join [System.Environment]::NewLine)

Write-Host 'UPM external consumer smoke passed.'
Write-Host "Report: $jsonReport"
