param(
    [string]$BlenderVersion = "5.2"
)

$ErrorActionPreference = "Stop"
$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $sourceRoot "..\..\..")).Path
$blenderRoot = Join-Path $env:APPDATA "Blender Foundation\Blender\$BlenderVersion\scripts\addons"
$targetRoot = Join-Path $blenderRoot "WGameWeaponTools"
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

New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
Get-ChildItem -LiteralPath $sourceRoot -Filter '*.py' -File |
    Where-Object { $_.Name -notlike 'test_*' } |
    Copy-Item -Destination $targetRoot
@{
    format = "mx.wgameWeaponToolsDeployment.v1"
    repositoryRoot = $repositoryRoot
    installedUtc = [DateTime]::UtcNow.ToString("o")
    blenderVersion = $BlenderVersion
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $targetRoot "deployment.json") -Encoding utf8

Write-Host "Installed WGameWeaponTools for Blender $BlenderVersion" -ForegroundColor Green
Write-Host "Source: $sourceRoot"
Write-Host "Deployed: $targetRoot"
Write-Host "In Blender: Edit -> Preferences -> Add-ons -> Enable 'WGame Weapon Tools'"
Write-Host "Then check 3D View Sidebar (N-panel) -> 'WGame Weapon' tab."
