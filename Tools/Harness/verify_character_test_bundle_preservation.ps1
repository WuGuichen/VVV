param([Parameter(Mandatory)][string]$ProjectPath,[Parameter(Mandatory)][string]$BuildOutputRoot,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$EvidenceDirectoryName='BundlePreservationEvidence')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {throw 'PowerShell 7 is required for metadata-only PEReader validation.'}
$project=[IO.Path]::GetFullPath($ProjectPath)
$output=[IO.Path]::GetFullPath($BuildOutputRoot)
$config=Get-Content (Join-Path $output 'CharacterTestPerformancePlayerBuildRunReport.configuration.json') -Raw | ConvertFrom-Json
$manifestDirectory=Join-Path $output 'BuildAnalysis/AssetBundles'
$receipt=Get-Content (Join-Path $manifestDirectory 'receipt.json') -Raw | ConvertFrom-Json
$rootManifest=Join-Path $manifestDirectory ([IO.Path]::GetFileName($receipt.manifestPath))
if ((Get-FileHash $rootManifest).Hash.ToLowerInvariant() -ne $receipt.manifestSha256) {throw 'Archived master manifest hash differs from validated Player manifest receipt.'}
$destination=Join-Path $output $EvidenceDirectoryName
if (Test-Path $destination) {throw 'Bundle preservation evidence exists; preserve prior evidence.'}
New-Item -ItemType Directory -Path $destination | Out-Null
$managed=Join-Path (Join-Path $output $config.symbolsDirectory) 'Il2CppSources/Managed'
$typeAssemblies=@{}
$assemblyHashes=@()
foreach ($dll in Get-ChildItem $managed -Filter '*.dll' -File) {
    $stream=[IO.File]::OpenRead($dll.FullName)
    $pe=[Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $md=[Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        function Resolve-TypeName($handle) {
            $def=$md.GetTypeDefinition($handle); $name=$md.GetString($def.Name); $parent=$def.GetDeclaringType()
            if (-not $parent.IsNil) {return (Resolve-TypeName $parent)+'/'+$name}
            $ns=$md.GetString($def.Namespace)
            if ([string]::IsNullOrEmpty($ns)) {return $name}
            return $ns+'.'+$name
        }
        foreach ($handle in $md.TypeDefinitions) {
            $name=Resolve-TypeName $handle
            if (-not $typeAssemblies.ContainsKey($name)) {$typeAssemblies[$name]=@()}
            $typeAssemblies[$name]+=$dll.BaseName
        }
    } finally {$pe.Dispose();$stream.Dispose()}
    $sha=(Get-FileHash $dll.FullName).Hash.ToLowerInvariant()
    $expected=@($config.symbols | Where-Object {$_.archivePath -eq ('Il2CppSources/Managed/'+$dll.Name)})
    if ($expected.Count -ne 1 -or $sha -ne $expected[0].sha256) {throw ('Stripped assembly hash not bound to build receipt: '+$dll.Name)}
    $assemblyHashes+=[ordered]@{assembly=$dll.BaseName;sha256=$sha;relativePath=[IO.Path]::GetRelativePath($output,$dll.FullName)}
}
$scriptRefs=@{}
$serializeRefs=@{}
$manifestHashes=@()
foreach ($file in Get-ChildItem $manifestDirectory -Filter '*.manifest' -File) {
    $text=[IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($text,'Script: \{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: 3\}')) {
        if ($match.Groups[1].Value -ne '11500000') {throw ('Unsupported compiled-plugin MonoScript identifier; require explicit mapping: '+$match.Value)}
        $guid=$match.Groups[2].Value
        if (-not $scriptRefs.ContainsKey($guid)) {$scriptRefs[$guid]=@()}
        $scriptRefs[$guid]+=$file.Name
    }
    foreach ($match in [regex]::Matches($text,'(?m)^- AssemblyName: ([^\r\n]+)\r?\n  ClassName: ([^\r\n]+)')) {
        $assembly=$match.Groups[1].Value.Trim();$class=$match.Groups[2].Value.Trim();$key=$assembly+'|'+$class
        if (-not $serializeRefs.ContainsKey($key)) {$serializeRefs[$key]=[ordered]@{assembly=$assembly;type=$class;manifests=@()}}
        $serializeRefs[$key].manifests+=$file.Name
    }
    $manifestHashes+=[ordered]@{name=$file.Name;sha256=(Get-FileHash $file.FullName).Hash.ToLowerInvariant()}
}
$guidSources=@{}
$snapshotPath=Join-Path $project 'SourceSnapshot.json'
if (Test-Path $snapshotPath) {
    $snapshot=Get-Content $snapshotPath -Raw | ConvertFrom-Json
    $metaPaths=@($snapshot.files | Where-Object {$_.path.EndsWith('.cs.meta')} | ForEach-Object {Join-Path $project $_.path})
} else {
    $metaPaths=@(@('Assets','Packages') | ForEach-Object {Get-ChildItem (Join-Path $project $_) -Filter '*.cs.meta' -File -Recurse | Select-Object -ExpandProperty FullName})
}
$cache=Join-Path $project 'Library/PackageCache'
if (Test-Path $cache) {$metaPaths+=@(Get-ChildItem $cache -Filter '*.cs.meta' -File -Recurse | Select-Object -ExpandProperty FullName)}
foreach ($path in $metaPaths) {
    if (-not (Test-Path $path -PathType Leaf)) {continue}
    $match=[regex]::Match([IO.File]::ReadAllText($path),'(?m)^guid: ([a-f0-9]{32})\s*$')
    if ($match.Success -and $scriptRefs.ContainsKey($match.Groups[1].Value)) {
        $guid=$match.Groups[1].Value
        if ($guidSources.ContainsKey($guid) -and $guidSources[$guid] -ne $path) {throw ('Ambiguous script GUID: '+$guid)}
        $guidSources[$guid]=$path
    }
}
$scripts=@();$serialized=@();$issues=@()
foreach ($guid in $scriptRefs.Keys | Sort-Object) {
    if (-not $guidSources.ContainsKey($guid)) {$issues+='Script GUID source unresolved: '+$guid;continue}
    $source=$guidSources[$guid].Substring(0,$guidSources[$guid].Length-5)
    $stem=[IO.Path]::GetFileNameWithoutExtension($source)
    $sourceText=[IO.File]::ReadAllText($source)
    $namespaces=@([regex]::Matches($sourceText,'\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique)
    $candidates=@($typeAssemblies.Keys | Where-Object {$_ -eq $stem -or $_.EndsWith('.'+$stem,[StringComparison]::Ordinal)} | Where-Object {
        $name=$_;$namespaces.Count -eq 0 -or @($namespaces | Where-Object {$name -eq ($_+'.'+$stem)}).Count -gt 0
    })
    if ($candidates.Count -ne 1 -or $typeAssemblies[$candidates[0]].Count -ne 1) {$issues+='Missing or ambiguous compiled MonoScript type: '+$source;continue}
    $scripts+=[ordered]@{guid=$guid;sourcePath=[IO.Path]::GetRelativePath($project,$source).Replace('\','/');sourceSha256=(Get-FileHash $source).Hash.ToLowerInvariant();metaSha256=(Get-FileHash $guidSources[$guid]).Hash.ToLowerInvariant();type=$candidates[0];assemblies=$typeAssemblies[$candidates[0]];manifests=@($scriptRefs[$guid] | Sort-Object -Unique)}
}
foreach ($entry in $serializeRefs.Values) {
    $present=$typeAssemblies.ContainsKey($entry.type) -and $entry.assembly -in $typeAssemblies[$entry.type]
    if (-not $present) {$issues+='SerializeReference type missing: '+$entry.assembly+'|'+$entry.type}
    $serialized+=[ordered]@{assembly=$entry.assembly;type=$entry.type;present=$present;manifests=@($entry.manifests | Sort-Object -Unique)}
}
[ordered]@{status=if ($issues.Count -eq 0) {'Passed'} else {'Failed'};buildGuid=$config.buildGuid;experiment=$config.experiment;managedStrippingLevel=$config.managedStrippingLevel;masterManifestSha256=$receipt.manifestSha256;manifestCount=$manifestHashes.Count;scriptCount=$scriptRefs.Count;serializeReferenceTypeCount=$serializeRefs.Count;scripts=$scripts;serializeReferenceTypes=$serialized;manifests=$manifestHashes;strippedAssemblyHashes=$assemblyHashes;issues=$issues;scope='Presence of actual Bundle-declared MonoScript and SerializeReference types in the build-bound archived stripped managed assemblies. Script resolution uses source GUID, filename/declared namespace and unique actual metadata type; ambiguous cases fail. Does not claim every Bundle behavior has been exercised in Player; use runtime probes separately.'} | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $destination 'BundleTypes.json') -Encoding utf8
if ($issues.Count -gt 0) {throw ($issues -join '; ')}
Write-Output ('PASS: '+$scriptRefs.Count+' Bundle MonoScript types and '+$serializeRefs.Count+' SerializeReference types present; '+$manifestHashes.Count+' manifests; build '+$config.buildGuid)
