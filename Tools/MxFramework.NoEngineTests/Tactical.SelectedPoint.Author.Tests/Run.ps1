param(
    [Parameter(Mandatory=$true)][string]$UnityProjectPath,
    [string]$OutputDirectory,
    [string]$TestProgramPath,
    [switch]$UseUnityMono,
    [string]$SourceOverlayRoot
)
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$dependencyRoot=(Resolve-Path -LiteralPath $UnityProjectPath).Path
if(!$OutputDirectory){$OutputDirectory=Join-Path $sourceRoot '.agent/SelectedPointAuthorValidation'}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if($SourceOverlayRoot){$SourceOverlayRoot=[IO.Path]::GetFullPath($SourceOverlayRoot)}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$config=Join-Path $out 'NuGet.Config'
[IO.File]::WriteAllText($config,'<configuration><packageSources><clear /></packageSources></configuration>')
$references=@{}
foreach($assembly in @('MxFramework.Game.CharacterTest','MxFramework.Game.CharacterTest.Editor')) {
    $xml=[xml][IO.File]::ReadAllText((Join-Path $dependencyRoot ($assembly+'.csproj')))
    foreach($node in @($xml.SelectNodes('//ProjectReference'))) {
        $name=[IO.Path]::GetFileNameWithoutExtension($node.Include)
        $ref=$xml.CreateElement('Reference'); $ref.SetAttribute('Include',$name)
        $hint=$xml.CreateElement('HintPath')
        $hint.InnerText=if($name -eq 'MxFramework.Game.CharacterTest'){Join-Path $out 'Temp/bin/Debug/MxFramework.Game.CharacterTest.dll'}else{Join-Path $dependencyRoot ('Library/ScriptAssemblies/'+$name+'.dll')}
        $ref.AppendChild($hint) | Out-Null; $node.ParentNode.ReplaceChild($ref,$node) | Out-Null
    }
    foreach($node in @($xml.SelectNodes('//Compile'))) {
        $relative=$node.GetAttribute('Include')
        $candidate=Join-Path $sourceRoot $relative
        if($SourceOverlayRoot) {
            $overlayCandidate=Join-Path $SourceOverlayRoot $relative
            if(Test-Path -LiteralPath $overlayCandidate -PathType Leaf){$candidate=$overlayCandidate}
        }
        $node.SetAttribute('Include', [string]$candidate)
    }
    foreach($node in @($xml.SelectNodes('//Reference/HintPath'))) {
        if(![IO.Path]::IsPathRooted($node.InnerText)){$node.InnerText=Join-Path $dependencyRoot $node.InnerText}
        $name=$node.ParentNode.Include
        if($name -notmatch '^(mscorlib|netstandard|System($|\.))'){$references[$name]=$node.InnerText}
    }
    foreach($node in @($xml.SelectNodes('//Analyzer'))) {$node.ParentNode.RemoveChild($node) | Out-Null}
    $project=Join-Path $out ($assembly+'.csproj'); $xml.Save($project)
    dotnet restore $project --configfile $config --verbosity quiet *> (Join-Path $out ($assembly+'.restore.log'))
    if($LASTEXITCODE){throw "Restore failed: $assembly"}
    dotnet build $project --nologo --verbosity quiet --no-restore *> (Join-Path $out ($assembly+'.build.log'))
    if($LASTEXITCODE){throw "Compilation failed: $assembly (see build log)"}
    Write-Output "Compiled $assembly from isolated sources."
}
if($UseUnityMono) {
    if(!$TestProgramPath){$TestProgramPath=Join-Path $PSScriptRoot 'Program.cs'}
    $testSource=[IO.Path]::GetFullPath($TestProgramPath)
    $runtimeSpec=[xml][IO.File]::ReadAllText((Join-Path $out 'MxFramework.Game.CharacterTest.csproj'))
    $monoRefs=@($runtimeSpec.SelectNodes('//Reference/HintPath') | ForEach-Object {$_.InnerText})
    $monoRefs+=Join-Path $out 'Temp/bin/Debug/MxFramework.Game.CharacterTest.dll'
    $monoRefs+=Join-Path $out 'Temp/bin/Debug/MxFramework.Game.CharacterTest.Editor.dll'
    $monoPath=[IO.Path]::GetFullPath((Join-Path (Split-Path $references['UnityEngine.CoreModule']) '../../MonoBleedingEdge/bin/mono.exe'))
    if(!(Test-Path -LiteralPath $monoPath)){throw "Unity Mono runner missing: $monoPath"}
    $sdk=(dotnet --list-sdks | Select-Object -Last 1) -replace '^([\d.]+) \[(.*)\]$', '$2\$1'
    $csc=Join-Path $sdk 'Roslyn/bincore/csc.dll'
    $exe=Join-Path $out 'MxFramework.Tests.exe'
    $rsp=Join-Path $out 'mono-tests.rsp'
    $lines=@('/noconfig','/nostdlib','/langversion:9.0','/target:exe',('/out:"'+$exe+'"'))
    $lines+=@($monoRefs | Select-Object -Unique | ForEach-Object {'/reference:"'+$_+'"'})
    $lines+='"'+$testSource+'"'
    [IO.File]::WriteAllLines($rsp,$lines)
    dotnet $csc ('@'+$rsp) *> (Join-Path $out 'tests.compile.log')
    if($LASTEXITCODE){Get-Content (Join-Path $out 'tests.compile.log'); throw 'Mono test compilation failed'}
    foreach($name in @('MxFramework.Game.CharacterTest','MxFramework.Game.CharacterTest.Editor')) {
        Copy-Item -LiteralPath (Join-Path $out ('Temp/bin/Debug/'+$name+'.dll')) -Destination (Join-Path $out ($name+'.dll')) -Force
    }
    $taskPreviousMonoPath=$env:MONO_PATH
    try {
        $env:MONO_PATH=($out+';'+((@($monoRefs | ForEach-Object {Split-Path $_}) | Select-Object -Unique) -join ';'))
        & $monoPath $exe $dependencyRoot $sourceRoot *> (Join-Path $out 'tests.log')
        $taskMonoExit=$LASTEXITCODE
    } finally { $env:MONO_PATH=$taskPreviousMonoPath }
    Get-Content (Join-Path $out 'tests.log')
    if($taskMonoExit){throw 'Selected point automatic consumption tests failed'}
    return
}
$references['MxFramework.Game.CharacterTest']=Join-Path $out 'Temp/bin/Debug/MxFramework.Game.CharacterTest.dll'
$references['MxFramework.Game.CharacterTest.Editor']=Join-Path $out 'Temp/bin/Debug/MxFramework.Game.CharacterTest.Editor.dll'
$runner=Join-Path $out 'Runner'; New-Item -ItemType Directory -Force -Path $runner | Out-Null
$project=[xml]'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><AssemblyName>MxFramework.Tests</AssemblyName><EnableDefaultItems>false</EnableDefaultItems><Nullable>disable</Nullable><GenerateAssemblyInfo>false</GenerateAssemblyInfo></PropertyGroup><ItemGroup /></Project>'
$group=$project.SelectSingleNode('//ItemGroup')
if(!$TestProgramPath){$TestProgramPath=Join-Path $PSScriptRoot 'Program.cs'}
$compile=$project.CreateElement('Compile'); $compile.SetAttribute('Include',[IO.Path]::GetFullPath($TestProgramPath)); $group.AppendChild($compile) | Out-Null
foreach($pair in $references.GetEnumerator()) {
    if(!(Test-Path -LiteralPath $pair.Value)){throw "Dependency missing: $($pair.Value)"}
    $ref=$project.CreateElement('Reference'); $ref.SetAttribute('Include',$pair.Key)
    $hint=$project.CreateElement('HintPath'); $hint.InnerText=$pair.Value
    $ref.AppendChild($hint) | Out-Null; $group.AppendChild($ref) | Out-Null
}
$runnerProject=Join-Path $runner 'Runner.csproj'; $project.Save($runnerProject)
dotnet restore $runnerProject --configfile $config --verbosity quiet *> (Join-Path $out 'tests.restore.log')
if($LASTEXITCODE){throw 'Test restore failed'}
dotnet run --project $runnerProject --no-restore -- $dependencyRoot $sourceRoot *> (Join-Path $out 'tests.log')
if($LASTEXITCODE){Get-Content -LiteralPath (Join-Path $out 'tests.log'); throw 'Selected point author tests failed'}
Get-Content -LiteralPath (Join-Path $out 'tests.log')
