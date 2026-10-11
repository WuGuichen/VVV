param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$BuildOutputRoot,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$EvidenceDirectoryName = 'PreservationEvidence'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'This metadata-only verifier requires the installed PowerShell 7; it never loads Player assemblies.' }
$project = [IO.Path]::GetFullPath($ProjectPath)
$output = [IO.Path]::GetFullPath($BuildOutputRoot)
$configuration = Get-Content (Join-Path $output 'CharacterTestPerformancePlayerBuildRunReport.configuration.json') -Raw | ConvertFrom-Json
$linkPath = Join-Path $project 'Assets/Scripts/WGameFrameworkProject/CharacterTest/link.xml'
[xml]$link = Get-Content -LiteralPath $linkPath -Raw
$stripped = Join-Path $project 'Library/Bee/artifacts/WinPlayerBuildProgram/ManagedStripped'
$unstripped = Join-Path $project 'Library/Bee/PlayerScriptAssemblies'
$destination = Join-Path $output $EvidenceDirectoryName
if (Test-Path $destination) { throw "Preservation evidence already exists; preserve it: $destination" }
# Reject stale Library evidence before creating output: every inspected stripped
# DLL must match the archived, hashed managed assembly for this particular build.
foreach ($assembly in $link.linker.assembly) {
    $name = $assembly.fullname + '.dll'
    $expected = @($configuration.symbols | Where-Object { $_.archivePath -eq ('Il2CppSources/Managed/' + $name) })
    $archived = Join-Path (Join-Path $output $configuration.symbolsDirectory) ('Il2CppSources/Managed/' + $name)
    $current = Join-Path $stripped $name
    if ($expected.Count -ne 1 -or -not (Test-Path $archived -PathType Leaf) -or -not (Test-Path $current -PathType Leaf) -or
        (Get-FileHash $archived).Hash.ToLowerInvariant() -ne $expected[0].sha256 -or
        (Get-FileHash $current).Hash.ToLowerInvariant() -ne $expected[0].sha256) {
        throw "Library stripped assembly is not bound to this build's archived evidence: $name. Run JSON verification immediately after building, before another build."
    }
}
New-Item -ItemType Directory -Path $destination | Out-Null
Copy-Item -LiteralPath $linkPath -Destination (Join-Path $destination 'link.xml')
$missing = New-Object System.Collections.Generic.List[string]
$assemblies = New-Object System.Collections.Generic.List[object]
$typeCount = 0
$compiledTypes = 0
$notCompiled = New-Object System.Collections.Generic.List[string]
function Read-AssemblyTypes([string]$assemblyPath) {
    $stream = [IO.File]::OpenRead($assemblyPath)
    $reader = [Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($reader)
        $names = @{}
        function Resolve-TypeName($handle) {
            $definition = $metadata.GetTypeDefinition($handle)
            $name = $metadata.GetString($definition.Name)
            $parent = $definition.GetDeclaringType()
            if (-not $parent.IsNil) { return (Resolve-TypeName $parent) + '/' + $name }
            $namespace = $metadata.GetString($definition.Namespace)
            if ([string]::IsNullOrEmpty($namespace)) { return $name }
            return $namespace + '.' + $name
        }
        foreach ($handle in $metadata.TypeDefinitions) { $names[(Resolve-TypeName $handle)] = $true }
        return $names
    }
    finally { $reader.Dispose(); $stream.Dispose() }
}
foreach ($assembly in $link.linker.assembly) {
    $path = Join-Path $stripped ($assembly.fullname + '.dll')
    if (-not (Test-Path $path -PathType Leaf)) { throw "Stripped assembly missing: $path" }
    $inputPath = Join-Path $unstripped ($assembly.fullname + '.dll')
    if (-not (Test-Path $inputPath -PathType Leaf)) { throw "Unstripped Player assembly missing: $inputPath" }
    $names = Read-AssemblyTypes $path
    $inputNames = Read-AssemblyTypes $inputPath
        foreach ($type in $assembly.type) {
            $typeCount++
            if (-not $inputNames.ContainsKey([string]$type.fullname)) {
                $notCompiled.Add($assembly.fullname + '|' + $type.fullname)
                continue
            }
            $compiledTypes++
            if (-not $names.ContainsKey([string]$type.fullname)) { $missing.Add($assembly.fullname + '|' + $type.fullname) }
        }
        $archived = Join-Path $destination ([IO.Path]::GetFileName($path))
        Copy-Item -LiteralPath $path -Destination $archived
        New-Item -ItemType Directory -Path (Join-Path $destination 'BeforeStripping') -Force | Out-Null
        $archivedInput = Join-Path (Join-Path $destination 'BeforeStripping') ([IO.Path]::GetFileName($inputPath))
        Copy-Item -LiteralPath $inputPath -Destination $archivedInput
        $assemblies.Add([ordered]@{assembly=$assembly.fullname;sha256=(Get-FileHash $archived).Hash.ToLowerInvariant();inputSha256=(Get-FileHash $archivedInput).Hash.ToLowerInvariant();expectedTypes=@($assembly.type).Count})
}
[ordered]@{status=if ($missing.Count -eq 0) {'Passed'} else {'Failed'};buildGuid=$configuration.buildGuid;
    experiment=$configuration.experiment;managedStrippingLevel=$configuration.managedStrippingLevel;
    linkXmlSha256=(Get-FileHash $linkPath).Hash.ToLowerInvariant();declaredTypes=$typeCount;compiledPlayerTypes=$compiledTypes;notCompiledInPlayer=@($notCompiled.ToArray());
    missingTypes=@($missing.ToArray());assemblies=@($assemblies.ToArray());
    scope='Metadata-only presence of explicit JSON linker types in the actual stripped assemblies; runtime behavior is covered separately by the build-and-run acceptance reports.'
} | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $destination 'JsonTypes.json') -Encoding UTF8
if ($missing.Count -gt 0) { throw "Stripped JSON types missing: $($missing -join ', ')" }
Write-Output "PASS: $compiledTypes compiled Player JSON types retained in $($assemblies.Count) stripped assemblies; $($notCompiled.Count) linker declarations absent before stripping; build $($configuration.buildGuid)."
