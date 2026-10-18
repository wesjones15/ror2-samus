param(
    [string]$RoR2Dir=(Join-Path $PSScriptRoot '..\2026 Mod Stuff\RiskOfRain2_DLC4'),
    [string]$CommonDir=(Join-Path $PSScriptRoot '..\ror2-common'),
    [switch]$Deploy,
    [switch]$SkipTests
)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$libraryRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot '..\libssbm'))
$gameRoot=[IO.Path]::GetFullPath($RoR2Dir)
$commonRoot=[IO.Path]::GetFullPath($CommonDir)
if(!(Test-Path -LiteralPath (Join-Path $commonRoot 'src\MeleeSelectDisplay.cs'))) { throw 'CommonDir must contain the shared Melee selection implementation.' }
if(!(Test-Path -LiteralPath (Join-Path $gameRoot 'Risk of Rain 2_Data\Managed\RoR2.dll'))) { throw 'RoR2Dir must name a DLC4 game installation.' }
Push-Location $libraryRoot
try {
    if(!$SkipTests) { & cargo test --workspace --offline; if($LASTEXITCODE) { throw 'libssbm tests failed.' } }
    # The extractor is part of ssbm.dll. CLI tools are development-only.
    & cargo build -p libssbm-ffi --release --offline
    if($LASTEXITCODE) { throw 'Native build failed.' }
} finally { Pop-Location }
& dotnet build (Join-Path $repoRoot 'Samus\Samus.csproj') -c Release "-p:RoR2Dir=$gameRoot" "-p:Ror2CommonDir=$commonRoot" --ignore-failed-sources
if($LASTEXITCODE) { throw 'Samus plugin build failed.' }
& dotnet run --project (Join-Path $repoRoot 'StartupCheck') -c Release "-p:RoR2Dir=$gameRoot" -- (Join-Path $repoRoot 'Samus\bin\Release\netstandard2.1\Samus.dll')
if($LASTEXITCODE) { throw 'Samus startup boundary check failed.' }
if(!$SkipTests) {
    & dotnet run --project (Join-Path $commonRoot 'tests\MeleeHostCheck') -c Release
    if($LASTEXITCODE) { throw 'Shared Melee arrival/camera checks failed.' }
    & dotnet run --project (Join-Path $commonRoot 'tests\UnstuckCheck') -c Release
    if($LASTEXITCODE) { throw 'Shared early-void checks failed.' }
    & dotnet run --project (Join-Path $commonRoot 'tests\MeleeRecoveryCheck') -c Release
    if($LASTEXITCODE) { throw 'Shared Melee recovery checks failed.' }
    & dotnet run --project (Join-Path $repoRoot 'PadCheck') -c Release
    if($LASTEXITCODE) { throw 'Samus input checks failed.' }
    & dotnet run --project (Join-Path $repoRoot 'NativeCheck') -c Release -- (Join-Path $libraryRoot 'target\release\ssbm.dll') (Join-Path $libraryRoot 'assets')
    if($LASTEXITCODE) { throw 'Native C# boundary check failed.' }
}
$name='Samus-modern-0.2.14-win-x64'
$packageRoot=Join-Path $repoRoot ('dist\'+$name)
$pluginRoot=Join-Path $packageRoot 'BepInEx\plugins\MeleeSamus'
$distRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'dist'))
if(![IO.Path]::GetFullPath($packageRoot).StartsWith($distRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Package path is outside dist.' }
# Remove only the obsolete helpers from previous local builds of this package.
# Never recursively clean a package/cache or touch an installed user's assets.
foreach($obsolete in @('ssbm-extract.exe','setup-assets.ps1')) {
    $obsoletePath=Join-Path $packageRoot $obsolete
    if(Test-Path -LiteralPath $obsoletePath -PathType Leaf) { Remove-Item -LiteralPath $obsoletePath }
}
New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
# Explicit source allowlist: no assets, ROM/disc files, game DLLs or local path hints.
Copy-Item -LiteralPath (Join-Path $repoRoot 'Samus\bin\Release\netstandard2.1\Samus.dll') -Destination $pluginRoot
Copy-Item -LiteralPath (Join-Path $libraryRoot 'target\release\ssbm.dll') -Destination $pluginRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md'),(Join-Path $repoRoot 'CHANGELOG.md') -Destination $packageRoot
$payload=@('BepInEx/plugins/MeleeSamus/Samus.dll','BepInEx/plugins/MeleeSamus/ssbm.dll','README.md','CHANGELOG.md')
$commits=[ordered]@{}
foreach($source in @('libssbm','ror2-samus','ror2-common')) {
    $commits[$source]=(& git -C (Join-Path $repoRoot ('..\'+$source)) rev-parse HEAD)
    if($LASTEXITCODE) { throw "Cannot identify source revision: $source" }
}
$manifest=[ordered]@{
    schema=1; character='Samus'; version='0.2.14'; nativeAbi=4; channel='modern'
    game='Risk of Rain 2'; gameVersion='DLC4 (2026)'; unity='2021.3.45f2'; platform='win-x64'
    sourceCommits=$commits
    files=@($payload | ForEach-Object { [ordered]@{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $packageRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant() } })
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packageRoot 'release.json') -Encoding utf8
$allowed=$payload+@('release.json')
foreach($file in Get-ChildItem -LiteralPath $packageRoot -File -Recurse) {
    $relative=[IO.Path]::GetRelativePath($packageRoot,$file.FullName).Replace('\','/')
    if($allowed -notcontains $relative) { throw "Package contains unexpected file: $relative" }
}
$zip=Join-Path $repoRoot ('dist\'+$name+'.zip')
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zip -Force
$checksum=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$checksum  $name.zip" | Set-Content -LiteralPath ($zip+'.sha256') -Encoding ascii
if($Deploy) {
    # The user copies staged files into their chosen game installation.
    $destination=Join-Path $repoRoot '..\target\BepInEx\plugins\MeleeSamus'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach($dll in @('Samus.dll','ssbm.dll')) {
        $source=Join-Path $pluginRoot $dll
        $target=Join-Path $destination $dll
        Copy-Item -LiteralPath $source -Destination $target -Force
        if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {throw "Staged checksum mismatch: $dll"}
    }
    Write-Host "Staged Samus for manual installation at $destination"
}
Write-Host "Built and audited $zip"
