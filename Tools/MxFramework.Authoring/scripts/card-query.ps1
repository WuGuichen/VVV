param(
    [string]$Name, [string]$Contains, [string]$Id, [string]$InstanceId,
    [ValidateSet('disk','generated','live')][string]$Layer='generated',
    [string]$Locale='zh-CN', [string]$SaveJson, [string]$SnapshotJson, [string]$RuntimeAssembly,
    [string]$ProjectRoot, [string]$ToolDll
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
if(!$ProjectRoot){$ProjectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))}
if(!$ToolDll){
    $candidates=@((Join-Path $ProjectRoot 'Temp/MxFramework/AuthoringQuery/cli/authoring.dll'))
    $candidates+=@('Release','Debug') | ForEach-Object { Join-Path $PSScriptRoot "../src/MxFramework.Authoring.Cli/bin/$_/net9.0/authoring.dll" }
    $ToolDll=$candidates | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
}
if(!$ToolDll -or !(Test-Path -LiteralPath $ToolDll)){throw 'Authoring CLI is not built. Build Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli once, or provide -ToolDll. Queries never build Unity or the CLI.'}
$queryArgs=@($ToolDll,'tactical-card','query','--project-root',$ProjectRoot,'--layer',$Layer,'--locale',$Locale)
foreach($option in @(@('--name',$Name),@('--contains',$Contains),@('--id',$Id),@('--instance-id',$InstanceId),@('--save-json',$SaveJson),@('--snapshot-json',$SnapshotJson),@('--runtime-assembly',$RuntimeAssembly))){
    if($option[1]){$queryArgs+=@($option[0],$option[1])}
}
& dotnet @queryArgs
exit $LASTEXITCODE
