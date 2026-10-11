param(
    [string]$BlenderVersion = "5.2"
)

$ErrorActionPreference = "Stop"
$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $sourceRoot "..\..\..")).Path
$blenderRoot = Join-Path $env:APPDATA "Blender Foundation\Blender\$BlenderVersion\scripts\addons"
$targetRoot = Join-Path $blenderRoot "WGamePaletteTools"
$legacyFile = Join-Path $blenderRoot "wgame_palette_tools.py"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"

New-Item -ItemType Directory -Force -Path $blenderRoot | Out-Null
if (Test-Path -LiteralPath $targetRoot) {
    $resolvedTarget = [System.IO.Path]::GetFullPath($targetRoot)
    $resolvedBlenderRoot = [System.IO.Path]::GetFullPath($blenderRoot)
    if (-not $resolvedTarget.StartsWith($resolvedBlenderRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace add-on outside Blender add-ons directory: $resolvedTarget"
    }
    Move-Item -LiteralPath $targetRoot -Destination "$targetRoot.backup-$stamp"
}
if (Test-Path -LiteralPath $legacyFile) {
    Move-Item -LiteralPath $legacyFile -Destination "$legacyFile.backup-$stamp"
}

New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $sourceRoot "__init__.py") -Destination (Join-Path $targetRoot "__init__.py")
Copy-Item -LiteralPath (Join-Path $sourceRoot "hair_flow.py") -Destination (Join-Path $targetRoot "hair_flow.py")
Copy-Item -LiteralPath (Join-Path $sourceRoot "hair_authoring.py") -Destination (Join-Path $targetRoot "hair_authoring.py")
Copy-Item -LiteralPath (Join-Path $sourceRoot "baked_workflow.py") -Destination (Join-Path $targetRoot "baked_workflow.py")
Copy-Item -LiteralPath (Join-Path $sourceRoot "baked_progress.py") -Destination (Join-Path $targetRoot "baked_progress.py")
Copy-Item -LiteralPath (Join-Path $sourceRoot "baked_preview.py") -Destination (Join-Path $targetRoot "baked_preview.py")
Copy-Item -LiteralPath (Join-Path $sourceRoot "baked_hair_preview.py") -Destination (Join-Path $targetRoot "baked_hair_preview.py")
@{
    format = "mx.wgamePaletteToolsDeployment.v1"
    repositoryRoot = $repositoryRoot
    installedUtc = [DateTime]::UtcNow.ToString("o")
    blenderVersion = $BlenderVersion
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $targetRoot "deployment.json") -Encoding utf8

Write-Host "已为 Blender $BlenderVersion 安装 WGamePaletteTools"
Write-Host "仓库权威源码：$sourceRoot"
Write-Host "本机部署副本：$targetRoot"
Write-Host "请重启 Blender 或重新加载脚本，然后启用「WGame 角色画板与表面材质」。"
