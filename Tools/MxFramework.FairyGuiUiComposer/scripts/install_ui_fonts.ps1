# Install the project's unmodified font files for the current Windows user.
# Unity loads its bundled Resources copies; this installation is for FairyGUI Editor.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$sourceDir = Join-Path $projectRoot 'Assets/Resources/Fonts'
$targetDir = Join-Path $env:LOCALAPPDATA 'Microsoft/Windows/Fonts'
$registryPath = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Fonts'
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
New-Item -Path $registryPath -Force | Out-Null
if (-not ('MxProjectFontInstall' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MxProjectFontInstall {
    [DllImport("gdi32.dll", CharSet=CharSet.Unicode)] public static extern int AddFontResource(string path);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out IntPtr result);
}
'@
}
$fonts = (Get-Content -LiteralPath (Join-Path $sourceDir 'font-sources.json') -Raw | ConvertFrom-Json).files
foreach ($font in $fonts) {
    $source = Join-Path $sourceDir $font.file
    $destination = Join-Path $targetDir $font.file
    if (-not (Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
    New-ItemProperty -Path $registryPath -Name ($font.family + ' (OpenType)') -Value $destination -PropertyType String -Force | Out-Null
    $loaded = [MxProjectFontInstall]::AddFontResource($destination)
    if ($loaded -eq 0) { throw "Windows could not register $destination" }
    Write-Output ('Installed: ' + $font.family)
}
$messageResult = [IntPtr]::Zero
[MxProjectFontInstall]::SendMessageTimeout([IntPtr]0xffff, 0x001D, [IntPtr]::Zero, [IntPtr]::Zero, 2, 1000, [ref]$messageResult) | Out-Null
