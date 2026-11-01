<#
.SYNOPSIS
Builds, checks and packages the Melee (ror2-samus) plugin, reporting progress while it runs.

.DESCRIPTION
The full build spends most of its time inside cargo test (the asset-bound checks pose every
fighter and effect model) and the native bound checks, which print nothing for minutes. This
script streams every child command line by line, prints a step counter, per-step duration and
a heartbeat, and writes the same lines to dist\build.log so a stalled step is obvious.

Use -SkipTests for a fast iteration build (native + plugin + startup check + package).

.PARAMETER RoR2Dir
DLC4 installation to compile against. Defaults to ..\2026 Mod Stuff\RiskOfRain2_DLC4.

.PARAMETER CommonDir
Shared ror2-common checkout compiled into the plugin.

.PARAMETER Deploy
Stage Samus.dll/ssbm.dll under ..\target\BepInEx\plugins\Melee for manual installation.

.PARAMETER SkipTests
Skip cargo tests and the native/host boundary checks.

.PARAMETER ForceTests
Run the libssbm cargo tests even when libssbm is byte-identical to the last green test run.
Those tests are asset-bound and cost minutes; a C#-only change does not need them again.
#>
param(
    [string]$RoR2Dir=(Join-Path $PSScriptRoot '..\2026 Mod Stuff\RiskOfRain2_DLC4'),
    [string]$CommonDir=(Join-Path $PSScriptRoot '..\ror2-common'),
    [switch]$Deploy,
    [switch]$SkipTests,
    [switch]$ForceTests
)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$libraryRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot '..\libssbm'))
$gameRoot=[IO.Path]::GetFullPath($RoR2Dir)
$commonRoot=[IO.Path]::GetFullPath($CommonDir)
if($Deploy) {
    & (Join-Path $commonRoot 'tests/CheckReleaseBranches.ps1') -HostDir $repoRoot -NativeDir $libraryRoot -CommonDir $commonRoot -HostBranch main -NativeBranch main
}
if(!(Test-Path -LiteralPath (Join-Path $commonRoot 'src\MeleeSelectDisplay.cs'))) { throw 'CommonDir must contain the shared Melee selection implementation.' }
if(!(Test-Path -LiteralPath (Join-Path $gameRoot 'Risk of Rain 2_Data\Managed\RoR2.dll'))) { throw 'RoR2Dir must name a DLC4 game installation.' }

# --- progress reporting and live log -------------------------------------------------------
$distRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'dist'))
$logPath=Join-Path $distRoot 'build.log'
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
Set-Content -LiteralPath $logPath -Value '' -Encoding utf8
$script:logPath=$logPath
$script:stepCount=0
$stampPath=Join-Path $distRoot '.rust-tests-ok'
$script:rustFingerprint=''
$script:testNote=''

# libssbm's asset-bound tests dominate a build (314 s of a measured 331 s). They only need to run
# when the Rust tree that they exercise changed, so the last green run is stamped with a
# fingerprint of HEAD, the working-tree status and the manifests.
function Get-FileHashText([string]$Path) {
    if(!(Test-Path -LiteralPath $Path)) { return '' }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RustFingerprint {
    $head=(& git -C $libraryRoot rev-parse HEAD 2>$null)
    $status=((& git -C $libraryRoot status --porcelain=v1 2>$null) -join "`n")
    $raw='{0}|{1}|{2}|{3}' -f $head,$status,(Get-FileHashText (Join-Path $libraryRoot 'Cargo.toml')),(Get-FileHashText (Join-Path $libraryRoot 'Cargo.lock'))
    $sha=[Security.Cryptography.SHA256]::Create()
    try {
        $bytes=[Text.Encoding]::UTF8.GetBytes($raw)
        return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '')
    } finally { $sha.Dispose() }
}

$runChecks=(-not $SkipTests)
$runCargoTests=$runChecks
$stampFingerprint=''
if($runCargoTests -and !$SkipTests -and !$ForceTests -and (Test-Path -LiteralPath $stampPath)) {
    # A fingerprint failure (git missing, unreadable manifest) must never skip or fail the build:
    # fall back to running the tests.
    try { $stampFingerprint=Get-RustFingerprint } catch { $stampFingerprint='' }
}
if($SkipTests) {
    $script:testNote='skipped (-SkipTests)'
} elseif($ForceTests) {
    $script:testNote='on (-ForceTests)'
} elseif($stampFingerprint -ne '') {
    $saved=(Get-Content -LiteralPath $stampPath -TotalCount 1)
    if($saved -eq $stampFingerprint) {
        $runCargoTests=$false
        $script:testNote='skipped (libssbm unchanged since the last green run; -ForceTests reruns)'
    } else {
        $script:testNote='on (libssbm changed since the last green run)'
    }
} elseif(Test-Path -LiteralPath $stampPath) {
    $script:testNote='on (could not fingerprint libssbm)'
} else {
    $script:testNote='on (no green test stamp yet)'
}
if($script:testNote -eq '') { $script:testNote='on' }
$script:stepTotal=4
if($runCargoTests) { $script:stepTotal++ }
if($runChecks) { $script:stepTotal+=5 }
$script:stepLabel=''
$script:stepStart=Get-Date
$script:stepTimes=New-Object System.Collections.Generic.List[object]
# A redirected stdout (agents, CI, job runners) gets no Write-Progress bar, only the text steps.
$script:showProgressBar=(-not [Console]::IsOutputRedirected)
$buildStart=Get-Date

function Write-BuildLine {
    param([string]$Text,[System.ConsoleColor]$Color=[System.ConsoleColor]::Gray)
    $line='{0}  {1}' -f (Get-Date -Format 'HH:mm:ss'), $Text
    Write-Host $line -ForegroundColor $Color
    Add-Content -LiteralPath $script:logPath -Value $line -Encoding utf8
}

function Show-StepProgress([string]$Prefix) {
    if(!$script:showProgressBar) { return }
    $percent=[int](100*($script:stepCount-1)/$script:stepTotal)
    try { Write-Progress -Activity 'Melee build' -Status ('{0} {1}' -f $Prefix,$script:stepLabel) -PercentComplete $percent } catch { }
}

function Begin-Step {
    param([string]$Label)
    $script:stepCount++
    $script:stepLabel=$Label
    $script:stepStart=Get-Date
    $prefix='[{0}/{1}]' -f $script:stepCount,$script:stepTotal
    Write-BuildLine ('{0} {1}' -f $prefix,$Label) Cyan
    Show-StepProgress $prefix
    return $prefix
}

function End-Step {
    param([string]$Prefix,[int]$Code)
    $took=((Get-Date)-$script:stepStart).TotalSeconds
    $script:stepTimes.Add([pscustomobject]@{ Step=$script:stepLabel; Seconds=$took; Code=$Code })
    if($Code -eq 0) { Write-BuildLine ('{0} done in {1:N1} s' -f $Prefix,$took) DarkGreen }
    else { Write-BuildLine ('{0} FAILED after {1:N1} s (exit {2})' -f $Prefix,$took,$Code) Red }
    if($script:showProgressBar) { try { Write-Progress -Activity 'Melee build' -Completed } catch { } }
}

# Prints the child output that arrived since the previous poll, one complete line at a time.
function Write-ChildOutput {
    param([hashtable]$State,[string]$File)
    $entry=$State[$File]
    $text=''
    try { $text=[IO.File]::ReadAllText($File) } catch { return }
    if($text.Length -le $entry.Position) { return }
    $entry.Rest+=$text.Substring($entry.Position)
    $entry.Position=$text.Length
    $lastNewline=$entry.Rest.LastIndexOf("`n")
    if($lastNewline -lt 0) { return }
    $complete=$entry.Rest.Substring(0,$lastNewline)
    $entry.Rest=$entry.Rest.Substring($lastNewline+1)
    foreach($line in ($complete -split "`r`n|`n")) {
        if($line -match '\S') { Write-BuildLine ('    '+$line) }
    }
}

# Runs a command, streaming its stdout/stderr to the console and the log, with a heartbeat so a
# step that prints nothing for minutes still shows that it is alive.
function Invoke-Logged {
    param(
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Command,
        [string[]]$Arguments=@(),
        [int]$HeartbeatSeconds=15
    )
    $prefix=Begin-Step $Label
    $exe=(Get-Command -Name $Command -ErrorAction Stop).Source
    $quoted=foreach($a in $Arguments) { if($a -match '[\s"]') { '"'+$a.Replace('"','\"')+'"' } else { $a } }
    $argLine=$quoted -join ' '
    $stdoutFile=[IO.Path]::GetTempFileName()
    $stderrFile=[IO.Path]::GetTempFileName()
    $state=@{}
    foreach($file in @($stdoutFile,$stderrFile)) { $state[$file]=@{ Position=0; Rest='' } }
    try {
        $proc=Start-Process -FilePath $exe -ArgumentList $argLine -NoNewWindow -PassThru -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
        # Windows PowerShell only keeps ExitCode if the handle is opened while the process runs;
        # without this every failed step read as exit 0 and the build staged anyway.
        $null=$proc.Handle
        $lastBeat=Get-Date
        while(-not $proc.HasExited) {
            Start-Sleep -Milliseconds 200
            foreach($file in @($stdoutFile,$stderrFile)) { Write-ChildOutput -State $state -File $file }
            if(((Get-Date)-$lastBeat).TotalSeconds -ge $HeartbeatSeconds) {
                Write-BuildLine ('      ... {0} still running ({1:N0} s)' -f $Label,((Get-Date)-$script:stepStart).TotalSeconds) DarkGray
                $lastBeat=Get-Date
            }
        }
        $proc.WaitForExit()
        foreach($file in @($stdoutFile,$stderrFile)) { Write-ChildOutput -State $state -File $file }
        $code=$proc.ExitCode
        # An unknown exit code is a failure, never a pass.
        if($null -eq $code) { $code=-1 }
    } finally {
        Remove-Item -LiteralPath $stdoutFile,$stderrFile -ErrorAction SilentlyContinue
    }
    End-Step $prefix $code
    return $code
}

$testsLabel=$script:testNote
Write-BuildLine ('Melee build: {0} steps, tests {1}, log {2}' -f $script:stepTotal,$testsLabel,$script:logPath) White
# Several agents share this tree. A cargo already running against libssbm/target makes every cargo
# step here wait on the build-directory lock, which looks exactly like a hang.
$otherCargo=@(Get-Process -Name cargo -ErrorAction SilentlyContinue)
if($otherCargo.Count -gt 0) {
    Write-BuildLine ('Warning: {0} cargo process(es) already running; if one of them is using libssbm\target this build waits on its lock.' -f $otherCargo.Count) Yellow
}

# --- build ---------------------------------------------------------------------------------
Push-Location $libraryRoot
try {
    if($runCargoTests) {
        try { $script:rustFingerprint=Get-RustFingerprint } catch { $script:rustFingerprint='' }
        $code=Invoke-Logged -Label 'libssbm tests (asset-bound checks; the slow step)' -Command 'cargo' -Arguments @('test','--workspace','--offline')
        if($code) { throw 'libssbm tests failed.' }
        if($script:rustFingerprint -ne '') {
            try { Set-Content -LiteralPath $stampPath -Value @($script:rustFingerprint,(Get-Date -Format o)) -Encoding ascii }
            catch { Write-BuildLine ('      could not write the test stamp: {0}' -f $_.Exception.Message) Yellow }
        }
    } elseif($runChecks) {
        Write-BuildLine ('      cargo tests {0}' -f $script:testNote) DarkGray
    }
    # The extractor is part of ssbm.dll. CLI tools are development-only.
    $code=Invoke-Logged -Label 'libssbm-ffi native release build' -Command 'cargo' -Arguments @('build','-p','libssbm-ffi','--release','--offline')
    if($code) { throw 'Native build failed.' }
} finally { Pop-Location }

$code=Invoke-Logged -Label 'Samus plugin' -Command 'dotnet' -Arguments @('build',(Join-Path $repoRoot 'Samus\Samus.csproj'),'-c','Release',"-p:RoR2Dir=$gameRoot","-p:Ror2CommonDir=$commonRoot",'--ignore-failed-sources')
if($code) { throw 'Samus plugin build failed.' }

$code=Invoke-Logged -Label 'Startup boundary check' -Command 'dotnet' -Arguments @('run','--project',(Join-Path $repoRoot 'StartupCheck'),'-c','Release',"-p:RoR2Dir=$gameRoot",'--',(Join-Path $repoRoot 'Samus\bin\Release\netstandard2.1\Samus.dll'))
if($code) { throw 'Samus startup boundary check failed.' }

if($runChecks) {
    $code=Invoke-Logged -Label 'Shared Melee arrival/camera checks' -Command 'dotnet' -Arguments @('run','--project',(Join-Path $commonRoot 'tests\MeleeHostCheck'),'-c','Release')
    if($code) { throw 'Shared Melee arrival/camera checks failed.' }

    $code=Invoke-Logged -Label 'Shared early-void checks' -Command 'dotnet' -Arguments @('run','--project',(Join-Path $commonRoot 'tests\UnstuckCheck'),'-c','Release')
    if($code) { throw 'Shared early-void checks failed.' }

    $code=Invoke-Logged -Label 'Shared Melee recovery checks' -Command 'dotnet' -Arguments @('run','--project',(Join-Path $commonRoot 'tests\MeleeRecoveryCheck'),'-c','Release')
    if($code) { throw 'Shared Melee recovery checks failed.' }

    $code=Invoke-Logged -Label 'Samus input checks' -Command 'dotnet' -Arguments @('run','--project',(Join-Path $repoRoot 'PadCheck'),'-c','Release')
    if($code) { throw 'Samus input checks failed.' }

    $code=Invoke-Logged -Label 'Native C# boundary check' -Command 'dotnet' -Arguments @('run','--project',(Join-Path $repoRoot 'NativeCheck'),'-c','Release','--',(Join-Path $libraryRoot 'target\release\ssbm.dll'),(Join-Path $libraryRoot 'assets'))
    if($code) { throw 'Native C# boundary check failed.' }
}

# --- package -------------------------------------------------------------------------------
$packagePrefix=Begin-Step 'package and audit'
$version=[regex]::Match((Get-Content -LiteralPath (Join-Path $repoRoot 'Samus\Plugin.cs') -Raw),'\[BepInPlugin\([^,]+,"Melee","([0-9.]+)"\)\]').Groups[1].Value
if(!$version) { throw 'Cannot read the Melee version from Samus\Plugin.cs.' }
$name="Melee-modern-$version-win-x64"
$packageRoot=Join-Path $repoRoot ('dist\'+$name)
$pluginRoot=Join-Path $packageRoot 'BepInEx\plugins\Melee'
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
$payload=@('BepInEx/plugins/Melee/Samus.dll','BepInEx/plugins/Melee/ssbm.dll','README.md','CHANGELOG.md')
$commits=[ordered]@{}
foreach($source in @('libssbm','ror2-samus','ror2-common')) {
    $commits[$source]=(& git -C (Join-Path $repoRoot ('..\'+$source)) rev-parse HEAD)
    if($LASTEXITCODE) { throw "Cannot identify source revision: $source" }
}
$manifest=[ordered]@{
    schema=1; character='Melee'; version=$version; nativeAbi=5; channel='modern'
    game='Risk of Rain 2'; gameVersion='DLC4 (2026)'; unity='2021.3.45f2'; platform='win-x64'
    sourceCommits=$commits
    files=@($payload | ForEach-Object { [ordered]@{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $packageRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant() } })
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packageRoot 'release.json') -Encoding utf8
$allowed=$payload+@('release.json')
foreach($file in Get-ChildItem -LiteralPath $packageRoot -File -Recurse) {
    $relative=$file.FullName.Substring($packageRoot.Length+1).Replace('\','/')
    if($allowed -notcontains $relative) { throw "Package contains unexpected file: $relative" }
}
$zip=Join-Path $repoRoot ('dist\'+$name+'.zip')
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zip -Force
$checksum=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$checksum  $name.zip" | Set-Content -LiteralPath ($zip+'.sha256') -Encoding ascii
End-Step $packagePrefix 0
if($Deploy) {
    # The user copies staged files into their chosen game installation.
    $destination=Join-Path $repoRoot '..\target\BepInEx\plugins\Melee'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach($dll in @('Samus.dll','ssbm.dll')) {
        $source=Join-Path $pluginRoot $dll
        $target=Join-Path $destination $dll
        Copy-Item -LiteralPath $source -Destination $target -Force
        if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {throw "Staged checksum mismatch: $dll"}
    }
    Write-BuildLine "Staged Melee for manual installation at $destination"
}
$total=((Get-Date)-$buildStart).TotalSeconds
Write-BuildLine ('Total {0:N1} s ({1:N1} min)' -f $total,($total/60)) White
Write-BuildLine "Built and audited $zip" Green
Write-BuildLine "Live log: $logPath" DarkGray
