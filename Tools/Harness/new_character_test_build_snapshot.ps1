# Copy and verify disk authority without launching Unity or copying its local state.
param([string]$SourceRoot = '', [string]$DestinationRoot = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CharacterTestSnapshotFiles {
    param([Parameter(Mandatory)][string]$Root)
    $base = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]'\/')
    $roots = @('Assets', 'Packages', 'ProjectSettings', 'Authoring', 'CharacterActionSource',
        'FGUIProject', 'FMOD', 'ArtSource', 'StorySource', 'WeaponSource', 'Tools', 'Docs')
    $localDirectories = @('.git', '.agent', '.agents', '.agentsroom', '.codex', '.cache', '__pycache__', 'node_modules')
    $files = [Collections.Generic.List[IO.FileInfo]]::new()
    foreach ($name in $roots) {
        $path = Join-Path $base $name
        if (-not (Test-Path -LiteralPath $path -PathType Container)) { continue }
        $pending = [Collections.Generic.Stack[IO.DirectoryInfo]]::new()
        $pending.Push([IO.DirectoryInfo]::new($path))
        while ($pending.Count -gt 0) {
            $directory = $pending.Pop()
            if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Snapshot cannot follow a directory link: $($directory.FullName)"
            }
            foreach ($child in $directory.EnumerateDirectories()) {
                if ($child.Name -in $localDirectories) { continue }
                if ($name -eq 'Tools' -and $child.Name -in @('bin', 'obj', 'Temp')) { continue }
                $pending.Push($child)
            }
            foreach ($file in $directory.EnumerateFiles()) {
                if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "Snapshot cannot follow a file link: $($file.FullName)"
                }
                if ($file.Name.EndsWith('.pyc', [StringComparison]::OrdinalIgnoreCase)) { continue }
                $files.Add($file)
            }
        }
    }
    foreach ($name in @('AGENTS.md', '.gitignore', '.gitattributes', '.editorconfig',
        'package.json', 'package-lock.json', 'Directory.Build.props', 'Directory.Build.targets', 'global.json')) {
        $path = Join-Path $base $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { $files.Add([IO.FileInfo]::new($path)) }
    }
    return @($files | Sort-Object FullName)
}

function Get-CharacterTestSnapshotInventory {
    param([Parameter(Mandatory)][string]$Root)
    $base = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]'\/')
    $inventory = [Collections.Generic.List[object]]::new()
    foreach ($file in Get-CharacterTestSnapshotFiles -Root $base) {
        $file.Refresh()
        $bytes = $file.Length
        $timestamp = $file.LastWriteTimeUtc.Ticks
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $file.Refresh()
        if ($bytes -ne $file.Length -or $timestamp -ne $file.LastWriteTimeUtc.Ticks) {
            throw "Source changed while hashing: $($file.FullName)"
        }
        $inventory.Add([pscustomobject]@{
            path = $file.FullName.Substring($base.Length + 1).Replace('\', '/')
            bytes = $bytes; sha256 = $hash
        })
    }
    return @($inventory.ToArray())
}

function Assert-CharacterTestSnapshotInventory {
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Expected,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Actual, [string]$Stage)
    $lookup = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Actual) { $lookup.Add([string]$entry.path, $entry) }
    if ($Expected.Count -ne $Actual.Count) { throw "Snapshot file set changed at ${Stage}: expected $($Expected.Count), actual $($Actual.Count)" }
    foreach ($entry in $Expected) {
        if (-not $lookup.ContainsKey([string]$entry.path)) { throw "Snapshot file missing at ${Stage}: $($entry.path)" }
        $other = $lookup[[string]$entry.path]
        if ($entry.bytes -ne $other.bytes -or $entry.sha256 -ne $other.sha256) {
            throw "Snapshot file content changed at ${Stage}: $($entry.path)"
        }
    }
}

function Get-CharacterTestSnapshotFingerprint {
    param([Parameter(Mandatory)][object[]]$Inventory)
    $content = [Text.StringBuilder]::new()
    foreach ($entry in $Inventory | Sort-Object path) {
        [void]$content.Append($entry.path).Append([char]0).Append($entry.bytes).Append([char]0).Append($entry.sha256).Append("`n")
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($content.ToString()))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Read-CharacterTestSnapshotGitState {
    param([string]$Root)
    $commit = & git -C $Root rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record source Git commit.' }
    $status = @(& git -C $Root -c core.quotepath=false status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record source Git worktree status.' }
    return [ordered]@{ commit = [string]$commit; status = $status }
}

function Assert-CharacterTestSnapshotDestination {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    $sourcePath = [IO.Path]::GetFullPath($Source).TrimEnd([char[]]'\/')
    $destinationPath = [IO.Path]::GetFullPath($Destination).TrimEnd([char[]]'\/')
    if ($destinationPath.Equals($sourcePath, [StringComparison]::OrdinalIgnoreCase) -or
        $destinationPath.StartsWith($sourcePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        $sourcePath.StartsWith($destinationPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Snapshot source and destination must be separate, non-nested directories.'
    }
    if (Test-Path -LiteralPath $destinationPath) { throw 'Snapshot destination must not already exist.' }
    for ($ancestor = [IO.Path]::GetDirectoryName($destinationPath); -not [string]::IsNullOrWhiteSpace($ancestor); $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
        if (Test-Path -LiteralPath $ancestor) {
            if (([IO.File]::GetAttributes($ancestor) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Snapshot destination must not traverse a link: $ancestor"
            }
        }
    }
    return $destinationPath
}

function New-CharacterTestBuildSnapshot {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    $sourcePath = [IO.Path]::GetFullPath($Source)
    $destinationPath = Assert-CharacterTestSnapshotDestination $sourcePath $Destination
    foreach ($name in @('Assets', 'Packages', 'ProjectSettings')) {
        if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $name) -PathType Container)) { throw "Source has no $name directory." }
    }
    $started = [DateTime]::UtcNow.ToString('o')
    $sourceGitBefore = Read-CharacterTestSnapshotGitState $sourcePath
    Write-Host 'Snapshot: hashing source inputs before copy.'
    $before = @(Get-CharacterTestSnapshotInventory $sourcePath)
    $manifest = Get-Content -LiteralPath (Join-Path $sourcePath 'Packages/manifest.json') -Raw | ConvertFrom-Json
    foreach ($dependency in $manifest.dependencies.PSObject.Properties) {
        if ([string]$dependency.Value -notlike 'file:*') { continue }
        $packagePath = [IO.Path]::GetFullPath((Join-Path (Join-Path $sourcePath 'Packages') ([string]$dependency.Value).Substring(5)))
        $packageRoot = [IO.Path]::GetFullPath((Join-Path $sourcePath 'Packages')).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
        if (-not $packagePath.StartsWith($packageRoot, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $packagePath -PathType Container)) { throw "External or missing file package must be resolved before snapshot: $($dependency.Name)" }
    }
    [void][IO.Directory]::CreateDirectory($destinationPath)
    $attemptPath = Join-Path $destinationPath 'SourceSnapshotAttempt.json'
    $attempt = [ordered]@{ schemaVersion = 1; status = 'Copying'; startedUtc = $started; sourceRoot = $sourcePath; destinationRoot = $destinationPath }
    $attempt | ConvertTo-Json | Set-Content -LiteralPath $attemptPath -Encoding UTF8
    try {
        Write-Host "Snapshot: copying $($before.Count) files without links or shared Unity state."
        foreach ($entry in $before) {
            $target = Join-Path $destinationPath $entry.path
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            [IO.File]::Copy((Join-Path $sourcePath $entry.path), $target, $false)
        }
        Write-Host 'Snapshot: verifying copied bytes and source stability.'
        $copied = @(Get-CharacterTestSnapshotInventory $destinationPath)
        Assert-CharacterTestSnapshotInventory $before $copied 'copy'
        $after = @(Get-CharacterTestSnapshotInventory $sourcePath)
        Assert-CharacterTestSnapshotInventory $before $after 'source-after-copy'
        $sourceGitAfter = Read-CharacterTestSnapshotGitState $sourcePath
        if (($sourceGitBefore | ConvertTo-Json -Compress) -ne ($sourceGitAfter | ConvertTo-Json -Compress)) {
            throw 'Source Git state changed during snapshot; wait for a stable source.'
        }
        $receipt = [ordered]@{
            schemaVersion = 1; status = 'Verified'; startedUtc = $started; verifiedUtc = [DateTime]::UtcNow.ToString('o')
            sourceRoot = $sourcePath; destinationRoot = $destinationPath; sourceControl = $sourceGitBefore
            fileCount = $before.Count; fileBytes = ($before | Measure-Object bytes -Sum).Sum
            sourceFingerprint = Get-CharacterTestSnapshotFingerprint $before; files = $before
            verification = 'Source before copy, copied bytes, source after copy and Git state agree. Unity has not been launched.'
            exclusions = @('Root Library/Temp/Logs/UserSettings/Artifacts and personal state', 'Tools bin/obj/Temp', 'node_modules and tool caches', 'Generated IDE projects', 'Git metadata')
        }
        $receipt | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $destinationPath 'SourceSnapshot.json') -Encoding UTF8
        $attempt.status = 'Verified'
        $attempt | ConvertTo-Json | Set-Content -LiteralPath $attemptPath -Encoding UTF8
        Write-Host "Snapshot verified: $destinationPath ($($before.Count) files, $($receipt.fileBytes) bytes). Unity was not launched."
        return [pscustomobject]@{ Path = $destinationPath; Files = $before.Count; Bytes = $receipt.fileBytes; Fingerprint = $receipt.sourceFingerprint }
    }
    catch {
        $attempt.status = 'Rejected'; $attempt['issue'] = $_.Exception.Message
        $attempt | ConvertTo-Json | Set-Content -LiteralPath $attemptPath -Encoding UTF8
        throw
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')) }
    if ([string]::IsNullOrWhiteSpace($DestinationRoot)) { throw 'Pass a new, separate -DestinationRoot. This command does not launch Unity.' }
    New-CharacterTestBuildSnapshot -Source $SourceRoot -Destination $DestinationRoot
}
