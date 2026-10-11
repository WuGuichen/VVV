# Static evidence only. This script never imports, edits or deletes Unity assets.
param([Parameter(Mandatory)][string]$ProjectRoot, [Parameter(Mandatory)][string]$DetailsPath,
    [Parameter(Mandatory)][string]$OutputPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Read-AuditProperty {
    param($Value, [string]$Name)
    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -ne $property) { return $property.Value }
    return $null
}
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([char[]]'\/')
$output = [IO.Path]::GetFullPath($OutputPath)
if ($output.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $output.Equals($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Write the audit outside the project snapshot.' }
if (Test-Path -LiteralPath $output) { throw 'Audit output already exists; preserve prior evidence.' }
$snapshotPath = Join-Path $root 'SourceSnapshot.json'
$snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json
if ($snapshot.status -ne 'Verified') { throw 'Audit requires a verified isolated source snapshot.' }
$snapshotFiles = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($entry in $snapshot.files) { $snapshotFiles.Add([string]$entry.path, $entry) }
function Assert-AuditSnapshotFile {
    param([string]$RelativePath)
    if (-not $snapshotFiles.ContainsKey($RelativePath)) { throw "Input is absent from snapshot receipt: $RelativePath" }
    $entry = $snapshotFiles[$RelativePath]
    $file = Join-Path $root $RelativePath
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Snapshot changed before audit: $RelativePath" }
}
$details = Get-Content -LiteralPath $DetailsPath -Raw | ConvertFrom-Json
$profilePath = Join-Path $root 'Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json'
Assert-AuditSnapshotFile 'Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json'
$profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
$catalogPath = Join-Path $root 'Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json'
Assert-AuditSnapshotFile 'Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json'
$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
$rows = [Collections.Generic.List[object]]::new()
$targets = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($asset in @($details.assets | Where-Object { $_.PSObject.Properties.Name -contains 'bundleDeclaration' })) {
    $path = [string]$asset.path
    $physical = [IO.Path]::GetFullPath((Join-Path $root $path))
    if (-not $physical.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Asset path escapes snapshot: $path" }
    if (Test-Path -LiteralPath $physical -PathType Leaf) { Assert-AuditSnapshotFile $path }
    $guid = ''
    if (Test-Path -LiteralPath ($physical + '.meta') -PathType Leaf) {
        Assert-AuditSnapshotFile ($path + '.meta')
        $meta = [IO.File]::ReadAllText($physical + '.meta')
        if ($meta -match '(?m)^guid:\s*([0-9a-f]{32})') { $guid = $Matches[1] }
    }
    $mapping = @($profile.entries | Where-Object {
        $locator = Read-AuditProperty $_ 'sourceLocator'
        (Read-AuditProperty $locator 'cachedPath') -eq $path -or
            (-not [string]::IsNullOrWhiteSpace($guid) -and (Read-AuditProperty $locator 'unityGuid') -eq $guid)
    } | ForEach-Object { $_.resourceKey })
    $declarations = @($catalog.entries | Where-Object {
        (Read-AuditProperty (Read-AuditProperty $_ 'providerData') 'assetPath') -eq $path
    } | Select-Object id,type,variant,packageId,provider,address)
    $row = [pscustomobject]@{
        path = $path; group = $(if ($path.StartsWith('Assets/MxFrameworkGenerated/CharacterPackages/')) { 'Character' } else { 'VFX' })
        existsInSnapshot = (Test-Path -LiteralPath $physical -PathType Leaf); guid = $guid
        historicalPackedBytes = $asset.bytes; historicalBundleDeclaration = $asset.bundleDeclaration
        historicalOrigins = $asset.origins; currentMappingKeys = $mapping; snapshotCatalogDeclarations = $declarations
        yamlDirectReferencers = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    }
    $rows.Add($row)
    if (-not [string]::IsNullOrWhiteSpace($guid)) { $targets.Add($guid, $row) }
}
$yamlFiles = 0
$unsupportedFiles = [Collections.Generic.List[string]]::new()
foreach ($entry in $snapshot.files) {
    $path = [string]$entry.path
    if (-not ($path.StartsWith('Assets/') -or $path.StartsWith('Packages/'))) { continue }
    if ([IO.Path]::GetExtension($path) -notin @('.unity', '.prefab', '.asset', '.mat', '.controller', '.overrideController', '.anim')) { continue }
    $file = Join-Path $root $path
    # Confirm inspected YAML belongs to the saved snapshot, rather than a later overlay.
    Assert-AuditSnapshotFile $path
    $text = [IO.File]::ReadAllText($file)
    if (-not $text.StartsWith('%YAML')) { $unsupportedFiles.Add($path); continue }
    $yamlFiles++
    foreach ($match in [regex]::Matches($text, 'guid:\s*([0-9a-f]{32})')) {
        $guid = $match.Groups[1].Value
        if ($targets.ContainsKey($guid)) { [void]$targets[$guid].yamlDirectReferencers.Add($path) }
    }
}
foreach ($row in $rows) {
    $row.yamlDirectReferencers = @($row.yamlDirectReferencers | Sort-Object)
    $row | Add-Member -NotePropertyName generatedResourcesMirrorReferencers -NotePropertyValue @($row.yamlDirectReferencers | Where-Object { $_.Contains('/runtime_resources/Resources/') })
}
$result = [ordered]@{
    schemaVersion = 1; utc = [DateTime]::UtcNow.ToString('o'); projectRoot = $root
    sourceSnapshotFingerprint = $snapshot.sourceFingerprint
    historicalDetailsPath = [IO.Path]::GetFullPath($DetailsPath)
    historicalDetailsSha256 = (Get-FileHash -LiteralPath $DetailsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    historicalBuildGuid = $details.buildGuid; inspectedYamlFiles = $yamlFiles; unsupportedSerializedFiles = @($unsupportedFiles.ToArray())
    rowCount = $rows.Count; rows = @($rows.ToArray())
    evidenceScope = 'Historical Player inclusion plus verified snapshot mapping/catalog and direct YAML GUID references. No Unity dependency traversal, FBX/binary parsing or dynamic loading proof.'
    decision = 'Review each generator/composition path and validate in Unity before changing inclusion. This audit does not prove any asset is unused or calculate compressed savings.'
}
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $output -Encoding UTF8
Write-Output "Static inclusion audit: $($rows.Count) historical dual-entry paths, $yamlFiles YAML files; $output. No Unity operation or asset change."
