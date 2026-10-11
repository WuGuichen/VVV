# Standalone preflight regression checks; never launches Unity or imports assets.
param([string]$TemporaryDirectory = [IO.Path]::GetTempPath())
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../character_test_build_identity.ps1')
$testRoot = Join-Path $TemporaryDirectory ('WGF-BuildIdentityTests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $testRoot 'ProjectSettings') -Force | Out-Null
$passed = 0
function Assert-Rejected {
    param([scriptblock]$Action, [string]$MessageFragment)
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notlike "*$MessageFragment*") { throw }
        $script:passed++
        return
    }
    throw "Expected rejection containing: $MessageFragment"
}
try {
    @'
m_EditorVersion: 6000.6.3f1
m_EditorVersionWithRevision: 6000.6.3f1 (45d8eee7de74)
'@ | Set-Content -LiteralPath (Join-Path $testRoot 'ProjectSettings/ProjectVersion.txt')
    $expected = Read-CharacterTestProjectVersion -ProjectRoot $testRoot
    if ($expected.Version -ne '6000.6.3f1' -or $expected.Revision -ne '45d8eee7de74') { throw 'Project pin read failed.' }
    Assert-CharacterTestUnityVersion -Expected $expected -ProductVersion '6000.6.3f1_45d8eee7de74' | Out-Null
    $passed++
    Assert-Rejected { Assert-CharacterTestUnityVersion $expected '6000.5.0f1_88b47c5e7076' } 'does not match'
    Assert-Rejected { Assert-CharacterTestUnityVersion $expected '6000.6.3f1_000000000000' } 'does not match'
    Assert-Rejected { Assert-CharacterTestUnityVersion $expected '6000.6.3f1' } 'does not match'
    Assert-Rejected { Assert-CharacterTestUnityVersion $expected '6000.6.3.4577518' } 'Unrecognized'

    $outside = Join-Path $TemporaryDirectory ('WGF-TestOutput-' + [Guid]::NewGuid().ToString('N'))
    Assert-CharacterTestBuildPaths $testRoot $outside
    $passed++
    Assert-Rejected { Assert-CharacterTestBuildPaths $testRoot $testRoot } 'project root'
    foreach ($directory in @('Assets', 'Packages', 'ProjectSettings', 'Library', 'Temp')) {
        Assert-Rejected { Assert-CharacterTestBuildPaths $testRoot (Join-Path $testRoot "$directory/BuildOutput") } "inside $directory"
    }
    $existing = Join-Path $testRoot 'ExistingEvidence'
    New-Item -ItemType Directory -Path $existing | Out-Null
    'keep me' | Set-Content -LiteralPath (Join-Path $existing 'summary.txt')
    Assert-Rejected { Assert-CharacterTestBuildPaths $testRoot $existing } 'existing evidence is preserved'
    if ((Get-Content -LiteralPath (Join-Path $existing 'summary.txt')) -ne 'keep me') { throw 'Existing evidence changed.' }
    New-Item -ItemType Directory -Path (Join-Path $testRoot 'Temp') | Out-Null
    'editor lock fixture' | Set-Content -LiteralPath (Join-Path $testRoot 'Temp/UnityLockfile')
    Assert-Rejected { Assert-CharacterTestBuildPaths $testRoot $outside } 'Editor lock'

    $log = Join-Path $testRoot 'unity.log'
    "Built from '6000.6/staging' branch; Version is '6000.6.3f1 (45d8eee7de74) revision 4577518'" | Set-Content -LiteralPath $log
    Assert-CharacterTestUnityLogVersion $log $expected
    $passed++
    "Built from '6000.5/staging' branch; Version is '6000.5.0f1 (88b47c5e7076) revision 8959100'" | Set-Content -LiteralPath $log
    Assert-Rejected { Assert-CharacterTestUnityLogVersion $log $expected } 'does not match'
    'a report with no engine version' | Set-Content -LiteralPath $log
    Assert-Rejected { Assert-CharacterTestUnityLogVersion $log $expected } 'no actual Editor version'
    Remove-Item -LiteralPath (Join-Path $testRoot 'Temp/UnityLockfile')
    '{"status":"Copying"}' | Set-Content -LiteralPath (Join-Path $testRoot 'SourceSnapshotAttempt.json')
    Assert-Rejected { Assert-CharacterTestBuildPaths $testRoot $outside } 'incomplete or rejected'
    '{"status":"Rejected"}' | Set-Content -LiteralPath (Join-Path $testRoot 'SourceSnapshotAttempt.json')
    Assert-Rejected { Assert-CharacterTestBuildPaths $testRoot $outside } 'incomplete or rejected'
    '{"status":"Verified"}' | Set-Content -LiteralPath (Join-Path $testRoot 'SourceSnapshotAttempt.json')
    Assert-Rejected { Assert-CharacterTestBuildPaths $testRoot $outside } 'incomplete or rejected'
    '{"status":"Rejected"}' | Set-Content -LiteralPath (Join-Path $testRoot 'SourceSnapshot.json')
    Assert-Rejected { Assert-CharacterTestBuildPaths $testRoot $outside } 'not verified'
    '{"status":"Verified"}' | Set-Content -LiteralPath (Join-Path $testRoot 'SourceSnapshot.json')
    Assert-CharacterTestBuildPaths $testRoot $outside
    $passed++
    Write-Output "PASS: $passed build identity/path/log checks. Unity was not launched."
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $temporaryRoot = [IO.Path]::GetFullPath($TemporaryDirectory).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'WGF-BuildIdentityTests-*') { throw 'Unsafe fixture cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
