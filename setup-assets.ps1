param([Parameter(Mandatory=$true)][string]$Disc,[string]$AssetsDir)
$ErrorActionPreference='Stop'
if(!$AssetsDir) { $AssetsDir=Join-Path $PSScriptRoot 'BepInEx\plugins\MeleeSamus\assets' }
& (Join-Path $PSScriptRoot 'ssbm-extract.exe') ([IO.Path]::GetFullPath($Disc)) ([IO.Path]::GetFullPath($AssetsDir))
if($LASTEXITCODE) { throw 'Asset extraction failed. Use your GALE01 revision 2 Melee ISO/GCM/CISO.' }
Write-Host 'Your local Samus cache is ready. Keep it out of shared archives.'
