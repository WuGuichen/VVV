param([string]$BlenderVersion = "5.2")
$ErrorActionPreference = "Stop"
$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$addonsRoot = Join-Path $env:APPDATA "Blender Foundation\Blender\$BlenderVersion\scripts\addons"
$targetRoot = Join-Path $addonsRoot "WGameUVTools"
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
foreach ($name in @('__init__.py', 'islands.py', 'face_authoring.py', 'detail_lifecycle.py', 'hot_reload.py', 'uv_contract.py', 'decals.py', 'paint_workflow.py', 'canvas_editor.py', 'rounding.py', 'feather.py', 'README.md', 'REFERENCES.md', 'LICENSE')) {
    $targetFile = Join-Path $targetRoot $name
    if (Test-Path -LiteralPath $targetFile) {
        Copy-Item -LiteralPath $targetFile -Destination "$targetFile.backup-$(Get-Date -Format yyyyMMdd-HHmmss-fff)"
    }
    Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination $targetFile
}
@{ sourceRoot = $sourceRoot } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $targetRoot 'deployment.json') -Encoding utf8
Write-Host "Deployed to $targetRoot"
