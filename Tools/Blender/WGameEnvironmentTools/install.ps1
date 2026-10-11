param([string]$BlenderVersion = '5.2')
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $sourceRoot '..\..\..')).Path
$addonsRoot = Join-Path $env:APPDATA "Blender Foundation\Blender\$BlenderVersion\scripts\addons"
$targetRoot = Join-Path $addonsRoot 'WGameEnvironmentTools'
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
$legacyRoot = Join-Path $repositoryRoot 'Temp\EnvironmentSurface\deployed-legacy'
foreach ($legacy in @('build_environment.py','catalog.py','test_catalog.py','test_blender.py','style_pass.py','textures.py')) {
    $legacyPath = Join-Path $targetRoot $legacy
    if (Test-Path -LiteralPath $legacyPath) {
        New-Item -ItemType Directory -Force -Path $legacyRoot | Out-Null
        Move-Item -LiteralPath $legacyPath -Destination (Join-Path $legacyRoot $legacy) -Force
    }
}
Get-ChildItem -LiteralPath $sourceRoot -Filter '*.py' | Where-Object Name -NotLike 'test_*' | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetRoot $_.Name)
}
@{ repositoryRoot = $repositoryRoot; format = 'mx.environmentToolsDeployment.v1' } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $targetRoot 'deployment.json') -Encoding utf8
Write-Host "已安装 WGame 场景画板：$targetRoot"
Write-Host '若 Blender 已打开，请保存制作文件后重启 Blender 以加载更新。'
