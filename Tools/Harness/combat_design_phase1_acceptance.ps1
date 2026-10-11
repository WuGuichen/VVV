param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$CliDll,
    [string]$EvidenceRoot = (Join-Path $env:TEMP ('combat-design-acceptance-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null
$runKey = 'phase1/' + [guid]::NewGuid().ToString('N')
function Invoke-Gateway([string]$verb, [object]$body, [string]$operationId) {
    $callArgs = @('exec', $CliDll, 'combat-design', $verb, '--project-root', $ProjectRoot)
    if ($body) {
        $requestPath = Join-Path $EvidenceRoot ([guid]::NewGuid().ToString('N') + '.request.json')
        $body | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $requestPath -Encoding utf8
        $callArgs += @('--request', $requestPath)
    }
    if ($operationId) { $callArgs += @('--operation-id', $operationId) }
    $output = & dotnet @callArgs
    if ($LASTEXITCODE -ne 0) { throw "Gateway $verb exited $LASTEXITCODE" }
    return ($output -join "`n" | ConvertFrom-Json)
}
function Submit-AndWait([object]$body) {
    $result = Invoke-Gateway 'submit' $body ''
    $deadline = [DateTime]::UtcNow.AddSeconds(45) # Tooling deadline only; never enters a combat source/hash.
    while ($result.status -eq 'PendingEditor' -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $result = Invoke-Gateway 'status' $null $result.operationId
    }
    if ($result.status -ne 'Succeeded') { throw ($result | ConvertTo-Json -Depth 8) }
    return $result
}
function Read-View([string]$handle) {
    $query = Invoke-Gateway 'query' $null ''
    return ($query.snapshots | Where-Object {$_.data.handle -eq $handle} | Select-Object -First 1)
}
function Assert([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }

Write-Host 'Creating isolated fixture through the actual Editor owner.'
$created = Submit-AndWait @{operation='create-fixture'; idempotencyKey="$runKey/create"}
$handle = $created.handle
$before = Read-View $handle
$control = $before.data.cards | Where-Object {-not $_.dependsOnFacing} | Select-Object -First 1
Assert (($before.data.cards | Where-Object {$_.dependsOnFacing}).Count -eq 2) 'Expected two automatically discovered consumers.'
$planned = Invoke-Gateway 'plan' @{handle=$handle; snapshotHash=$before.snapshotHash; thresholdDegrees=8; maximumFrames=45; rationale='Agent proposal for isolated acceptance: relax facing alignment to 8 degrees while retaining an independent 45-frame budget.'} ''
$commit = @{operation='commit'; idempotencyKey="$runKey/commit"; planToken=$planned.planToken}
$committed = Submit-AndWait $commit
for ($attempt=0; $attempt -lt 10; $attempt++) {
    $retry = Invoke-Gateway 'submit' $commit ''
    Assert ($retry.operationId -eq $committed.operationId -and $retry.status -eq 'Succeeded') 'Identical retry changed the command identity/outcome.'
}
$edited = Read-View $handle
Assert (-not $edited.evidenceCurrent) 'A mechanism mutation must invalidate old evidence.'
Assert ($edited.data.contractFacts.thresholdDegrees -eq 8 -and $edited.data.contractFacts.maximumFrames -eq 45) 'Typed edit was not applied to real source facts.'
$controlAfter = $edited.data.cards | Where-Object {$_.id -eq $control.id} | Select-Object -First 1
Assert ($controlAfter.revision -eq $control.revision) 'Unrelated card revision changed.'
Write-Host 'Checking the old snapshot conflicts without replaying its write.'
$stalePath = Join-Path $EvidenceRoot 'stale.request.json'
@{operation='commit'; idempotencyKey="$runKey/stale"; planToken=$planned.planToken} | ConvertTo-Json | Set-Content -LiteralPath $stalePath -Encoding utf8
& dotnet exec $CliDll combat-design submit --project-root $ProjectRoot --request $stalePath 2> (Join-Path $EvidenceRoot 'stale.stderr.txt') | Out-Null
Assert ($LASTEXITCODE -ne 0) 'Expected old plan rejection.'

Submit-AndWait @{operation='validate'; idempotencyKey="$runKey/validate"; handle=$handle; snapshotHash=$edited.snapshotHash} | Out-Null
$validated = Read-View $handle
Assert $validated.evidenceCurrent 'Validation did not bind evidence to current source.'
Write-Host 'Publishing only the isolated catalog, then reloading and recompiling.'
Submit-AndWait @{operation='publish'; idempotencyKey="$runKey/publish"; handle=$handle; snapshotHash=$validated.snapshotHash} | Out-Null
$published = Read-View $handle
Assert ($published.data.evidence.publication -eq 'isolated-catalog-committed-reloaded-recompiled') 'Publication/reload evidence missing.'
$revisions = @($published.data.cards | ForEach-Object {$_.publishedRevision}) -join ','
Submit-AndWait @{operation='publish'; idempotencyKey="$runKey/publish-repeat"; handle=$handle; snapshotHash=$published.snapshotHash} | Out-Null
$repeat = Read-View $handle
Assert ((@($repeat.data.cards | ForEach-Object {$_.publishedRevision}) -join ',') -eq $revisions) 'Identical publication created another revision.'
$report = @{status='Passed'; scope='actual-owner-isolated-fixture'; handle=$handle; referenceIdsHandEntered=0; duplicateHumanDataSynchronization=0; identicalCommitRetries=10;
    impactConsumers=2; unrelatedRevisionPreserved=$true; staleEvidenceAutomaticallyMarked=$true; stalePlanRejected=$true;
    publishReloadRecompile=$true; repeatPublishNewRevisions=0; play='not-run'; engineCardFaceScreenshot='not-run'; finalSnapshot=$repeat}
$report | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $EvidenceRoot 'result.json') -Encoding utf8
Write-Host "PASS isolated owner acceptance. Evidence: $EvidenceRoot"
