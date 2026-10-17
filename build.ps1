param(
    [string]$RoR2Dir=(Join-Path $PSScriptRoot '..\2026 Mod Stuff\RiskOfRain2_DLC4'),
    [switch]$Deploy,
    [switch]$SkipTests
)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$libraryRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot '..\libssbm'))
$gameRoot=[IO.Path]::GetFullPath($RoR2Dir)
if(!(Test-Path -LiteralPath (Join-Path $gameRoot 'Risk of Rain 2_Data\Managed\RoR2.dll'))) { throw 'RoR2Dir must name a DLC4 game installation.' }
Push-Location $libraryRoot
try {
    if(!$SkipTests) { & cargo test --workspace --offline; if($LASTEXITCODE) { throw 'libssbm tests failed.' } }
    & cargo build -p libssbm-ffi -p libssbm-assets --release --offline
    if($LASTEXITCODE) { throw 'Native build failed.' }
} finally { Pop-Location }
& dotnet build (Join-Path $repoRoot 'Samus\Samus.csproj') -c Release "-p:RoR2Dir=$gameRoot" --ignore-failed-sources
if($LASTEXITCODE) { throw 'Samus plugin build failed.' }
if(!$SkipTests) {
    & dotnet run --project (Join-Path $repoRoot 'NativeCheck') -c Release -- (Join-Path $libraryRoot 'target\release\ssbm.dll') (Join-Path $libraryRoot 'assets')
    if($LASTEXITCODE) { throw 'Native C# boundary check failed; extract your disc to libssbm/assets first.' }
}
$name='Samus-modern-0.1.0-win-x64'
$packageRoot=Join-Path $repoRoot ('dist\'+$name)
$pluginRoot=Join-Path $packageRoot 'BepInEx\plugins\MeleeSamus'
New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
# Explicit source allowlist: no assets, ROM/disc files, game DLLs or local path hints.
Copy-Item -LiteralPath (Join-Path $repoRoot 'Samus\bin\Release\netstandard2.1\Samus.dll') -Destination $pluginRoot
Copy-Item -LiteralPath (Join-Path $libraryRoot 'target\release\ssbm.dll') -Destination $pluginRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md'),(Join-Path $repoRoot 'CHANGELOG.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $libraryRoot 'target\release\ssbm-extract.exe') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'setup-assets.ps1') -Destination $packageRoot
$allowed=@('BepInEx/plugins/MeleeSamus/Samus.dll','BepInEx/plugins/MeleeSamus/ssbm.dll','README.md','CHANGELOG.md','ssbm-extract.exe','setup-assets.ps1')
foreach($file in Get-ChildItem -LiteralPath $packageRoot -File -Recurse) {
    $relative=[IO.Path]::GetRelativePath($packageRoot,$file.FullName).Replace('\','/')
    if($allowed -notcontains $relative) { throw "Package contains unexpected file: $relative" }
}
$zip=Join-Path $repoRoot ('dist\'+$name+'.zip')
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zip -Force
if($Deploy) {
    if(Get-Process -Name 'Risk of Rain 2' -ErrorAction SilentlyContinue) { throw 'Close Risk of Rain 2 before deploying DLLs.' }
    $destination=Join-Path $gameRoot 'BepInEx\plugins\MeleeSamus'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $pluginRoot 'Samus.dll'),(Join-Path $pluginRoot 'ssbm.dll') -Destination $destination
    # Local development only; deliberately outside the release package.
    Set-Content -LiteralPath (Join-Path $destination 'assets_dir.txt') -Value (Join-Path $libraryRoot 'assets') -Encoding utf8
    Write-Host "Deployed Samus to $destination"
}
Write-Host "Built and audited $zip"
