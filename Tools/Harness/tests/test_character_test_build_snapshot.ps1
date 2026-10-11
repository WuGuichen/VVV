param([string]$TemporaryDirectory = [IO.Path]::GetTempPath())
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../new_character_test_build_snapshot.ps1')
$fixture = Join-Path $TemporaryDirectory ('WGF-SnapshotTests-' + [Guid]::NewGuid().ToString('N'))
$passed = 0
function Assert-SnapshotRejected {
    param([scriptblock]$Action, [string]$MessageFragment)
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notlike "*$MessageFragment*") { throw }
        $script:passed++
        return
    }
    throw "Expected rejection: $MessageFragment"
}
try {
    $source = Join-Path $fixture 'Source'
    $target = Join-Path $fixture 'Target'
    foreach ($path in @('Assets/Formal', 'Packages', 'ProjectSettings', 'Tools/src', 'Tools/bin', 'Library', '.agent')) {
        New-Item -ItemType Directory -Path (Join-Path $source $path) -Force | Out-Null
    }
    foreach ($path in @('Assets/Formal/test.asset', 'Assets/Formal/test.asset.meta', 'Assets/Formal/untracked.cs',
        'Packages/manifest.json', 'ProjectSettings/ProjectVersion.txt', 'Tools/src/source.cs',
        'Tools/bin/cache.dll', 'Library/cache.asset', '.agent/local-state.json')) {
        $path | Set-Content -LiteralPath (Join-Path $source $path)
    }
    $before = @(Get-CharacterTestSnapshotInventory $source)
    if ($before.Count -ne 6 -or @($before | Where-Object path -eq 'Assets/Formal/test.asset.meta').Count -ne 1) { throw 'Snapshot scope must retain assets, meta and untracked sources while excluding caches.' }
    $passed++
    $fingerprint = Get-CharacterTestSnapshotFingerprint $before
    if ($fingerprint -ne (Get-CharacterTestSnapshotFingerprint @($before | Sort-Object path -Descending))) { throw 'Fingerprint depends on enumeration order.' }
    $passed++
    Assert-CharacterTestSnapshotDestination $source $target | Out-Null
    $passed++
    Assert-SnapshotRejected { Assert-CharacterTestSnapshotDestination $source $source } 'non-nested'
    Assert-SnapshotRejected { Assert-CharacterTestSnapshotDestination $source (Join-Path $source 'Copy') } 'non-nested'
    Assert-SnapshotRejected { Assert-CharacterTestSnapshotDestination $source $fixture } 'non-nested'
    New-Item -ItemType Directory -Path $target | Out-Null
    Assert-SnapshotRejected { Assert-CharacterTestSnapshotDestination $source $target } 'already exist'
    foreach ($entry in $before) {
        $path = Join-Path $target $entry.path
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($path)) -Force | Out-Null
        [IO.File]::Copy((Join-Path $source $entry.path), $path)
    }
    $copied = @(Get-CharacterTestSnapshotInventory $target)
    Assert-CharacterTestSnapshotInventory $before $copied 'fixture-copy'
    $passed++
    'same-length replacement' | Set-Content -LiteralPath (Join-Path $source 'Assets/Formal/test.asset')
    $after = @(Get-CharacterTestSnapshotInventory $source)
    Assert-SnapshotRejected { Assert-CharacterTestSnapshotInventory $before $after 'source-changed' } 'content changed'
    'new file' | Set-Content -LiteralPath (Join-Path $source 'Assets/late.cs')
    $after = @(Get-CharacterTestSnapshotInventory $source)
    Assert-SnapshotRejected { Assert-CharacterTestSnapshotInventory $before $after 'file-added' } 'file set changed'
    Remove-Item -LiteralPath (Join-Path $target 'Assets/Formal/test.asset.meta')
    $copied = @(Get-CharacterTestSnapshotInventory $target)
    Assert-SnapshotRejected { Assert-CharacterTestSnapshotInventory $before $copied 'meta-missing' } 'file set changed'
    Write-Output "PASS: $passed snapshot scope, provenance, path and mutation checks. Unity was not launched."
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    $base = [IO.Path]::GetFullPath($TemporaryDirectory).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($base, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'WGF-SnapshotTests-*') { throw 'Unsafe snapshot fixture cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
