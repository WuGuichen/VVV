param(
    [string]$BlenderVersion = "5.2"
)

$ErrorActionPreference = "Stop"
$source = Join-Path $PSScriptRoot "__init__.py"
$addonsRoot = Join-Path $env:APPDATA "Blender Foundation\Blender\$BlenderVersion\scripts\addons"
$target = Join-Path $addonsRoot "wgame_rigging_assistant.py"
$deployment = Join-Path $addonsRoot "wgame_rigging_assistant.deployment.json"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
New-Item -ItemType Directory -Path $addonsRoot -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $target -Force
@{
    format = "mx.wgameRiggingAssistantDeployment.v1"
    repositoryRoot = $repositoryRoot
    blenderVersion = $BlenderVersion
    installedUtc = [DateTime]::UtcNow.ToString("o")
} | ConvertTo-Json | Set-Content -LiteralPath $deployment -Encoding utf8
Write-Host "Installed WGame Rigging Assistant: $target"
