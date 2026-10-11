param([Parameter(Mandatory=$true)][string]$Binary,
      [Parameter(Mandatory=$true)][string]$Pdb,
      [Parameter(Mandatory=$true)][string]$Output,
      [Parameter(Mandatory=$true)][string]$Cache)
$ErrorActionPreference = 'Stop'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installations = & $vswhere -products '*' -property installationPath
$dia = $installations | ForEach-Object { Join-Path $_ 'DIA SDK\bin\amd64\msdia140.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$tlb = Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\x64\TlbImp.exe'
if (-not $dia -or -not (Test-Path -LiteralPath $tlb)) { throw 'DIA SDK or .NET Framework TlbImp unavailable.' }
New-Item -ItemType Directory -Force -Path $Cache | Out-Null
$interop = Join-Path $Cache 'DiaInterop.dll'
if (-not (Test-Path -LiteralPath $interop)) {
    & $tlb $dia "/out:$interop" /namespace:DiaInterop /silent
    if ($LASTEXITCODE -ne 0) { throw 'DIA interop generation failed.' }
}
Add-Type -Path $interop
Add-Type -ReferencedAssemblies $interop,System.Web.Extensions -Path (Join-Path $PSScriptRoot 'PackageNativeSizeAnalysis.cs')
[PackageNativeSizeAnalysis]::Analyze($Binary,$Pdb,$Output)
Write-Output "Native size evidence: $Output"
