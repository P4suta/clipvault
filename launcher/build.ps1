<#
.SYNOPSIS
    Compiles the ClipVault root launcher (ClipVault.exe) with the MSVC toolchain.

.DESCRIPTION
    Locates Visual Studio's C++ build tools via vswhere, then compiles ClipVault.Launcher.rc and
    ClipVault.Launcher.cpp into a small standalone native exe. The launcher is intentionally outside
    the .NET solution and uses no runtime, so the produced exe runs with no install.

    CI calls this directly (the toolchain is always present on windows runners). Local `just publish`
    passes -SkipIfMissingToolchain so a .NET-only dev box still produces the app\ tree, just without
    the root launcher (mirrors how `just sign` no-ops without a cert).

.PARAMETER OutputPath
    Full path of the exe to produce (default: <repo>/artifacts/launcher/ClipVault.exe).

.PARAMETER Version
    Version to stamp into the exe's version resource, e.g. "1.2.3" (default: "0.0.0").

.PARAMETER SkipIfMissingToolchain
    Warn and exit 0 instead of failing when the MSVC C++ tools are not installed.
#>
[CmdletBinding()]
param(
    [string]$OutputPath,
    [string]$Version = "0.0.0",
    [switch]$SkipIfMissingToolchain
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "artifacts/launcher/ClipVault.exe"
}

# Resolve the MSVC toolchain. vswhere ships with every VS 2017+ installer at a fixed location.
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio/Installer/vswhere.exe"
$vsPath = $null
if (Test-Path $vswhere) {
    $vsPath = & $vswhere -latest -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -property installationPath 2>$null | Select-Object -First 1
}

$vcvars = if ($vsPath) { Join-Path $vsPath "VC/Auxiliary/Build/vcvars64.bat" } else { $null }
if (-not $vcvars -or -not (Test-Path $vcvars)) {
    $msg = "MSVC C++ build tools not found (install the 'Desktop development with C++' workload)."
    if ($SkipIfMissingToolchain) {
        Write-Warning "$msg Skipping the root launcher build; the app\ tree will have no ClipVault.exe."
        exit 0
    }
    throw $msg
}

# Split "1.2.3[-suffix]" into integer parts; missing/non-numeric parts default to 0.
$parts = ($Version -split '[-+]')[0] -split '\.'
function Part([int]$i) { if ($parts.Count -gt $i -and $parts[$i] -match '^\d+$') { [int]$parts[$i] } else { 0 } }
$maj = Part 0; $min = Part 1; $pat = Part 2

$outDir = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$rc = Join-Path $scriptDir "ClipVault.Launcher.rc"
$cpp = Join-Path $scriptDir "ClipVault.Launcher.cpp"
$res = Join-Path $outDir "ClipVault.Launcher.res"
$obj = Join-Path $outDir "ClipVault.Launcher.obj"

# Compile the resources, then the launcher, inside the vcvars environment in a single cmd session so
# rc.exe and cl.exe are on PATH. Warnings are errors (/WX) to keep the tiny launcher clean.
$rcArgs = "/nologo /fo `"$res`" /dCLIPVAULT_VERSION_MAJOR=$maj /dCLIPVAULT_VERSION_MINOR=$min /dCLIPVAULT_VERSION_PATCH=$pat `"$rc`""
$clArgs = "/nologo /std:c++17 /W4 /WX /O1 /GS /EHsc /DUNICODE /D_UNICODE /Fo`"$obj`" `"$cpp`" `"$res`" /Fe`"$OutputPath`" /link /SUBSYSTEM:WINDOWS user32.lib"
$cmd = "call `"$vcvars`" >nul && rc.exe $rcArgs && cl.exe $clArgs"

& cmd.exe /c $cmd
if ($LASTEXITCODE -ne 0) {
    throw "Launcher build failed (exit $LASTEXITCODE)."
}

# Drop intermediates so only the exe remains in the output dir.
Remove-Item -Force -ErrorAction SilentlyContinue $res, $obj
Write-Host "Launcher built: $OutputPath (version $maj.$min.$pat)"
