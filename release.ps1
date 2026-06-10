# release.ps1 - Assemble the player release zip. The zip extracts directly into
# the game folder (the one with "Hand of Fate.exe") and contains everything the
# mod needs: the BepInEx loader preconfigured with the Unity-5.3 MonoBehaviour
# entrypoint, the mod DLLs, the Tolk and SAPI native runtimes, and the sound
# files. The layout is staged fresh each run from the repo's canonical sources
# (third_party\bepinex, third_party\tolk\dist\x86, native\hofsapi, sounds), so
# it can never drift from what setup-bepinex.ps1 + build.ps1 deploy.

param(
    [switch]$NoBuild,
    [switch]$SkipTests,
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\release.ps1 [-NoBuild] [-SkipTests] [-Help]"
    Write-Host "  -NoBuild    Skip building; package the last built DLLs"
    Write-Host "  -SkipTests  Skip the offline test suite"
    Write-Host "Output: release\HandOfFateAccess-v<version>.zip"
    exit 0
}

$ErrorActionPreference = "Stop"

# --- Version (shared MSBuild property) ---
$props = Get-Content "$PSScriptRoot\Directory.Build.props" -Raw
if ($props -notmatch '<Version>([^<]+)</Version>') {
    Write-Host "ERROR: no <Version> in Directory.Build.props" -ForegroundColor Red
    exit 1
}
$Version = $Matches[1]

# --- Build & test ---
if (-not $NoBuild) {
    Write-Host "Building HandOfFateAccess v$Version (Release)..." -ForegroundColor Cyan
    dotnet build "$PSScriptRoot\HandOfFateAccess\HandOfFateAccess.csproj" -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build FAILED." -ForegroundColor Red
        exit 1
    }
}
if (-not $SkipTests) {
    Write-Host "Running the offline test suite..." -ForegroundColor Cyan
    dotnet test "$PSScriptRoot\HandOfFateAccess.Tests\HandOfFateAccess.Tests.csproj"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Tests FAILED; not packaging." -ForegroundColor Red
        exit 1
    }
}

# --- Stage the game-folder layout ---
$ReleaseDir = "$PSScriptRoot\release"
$Stage      = "$ReleaseDir\staging"
if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
$PluginsDir = "$Stage\BepInEx\plugins"
New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null

# BepInEx loader: doorstop proxy at the game root, core, and the config seed
# whose [Preloader.Entrypoint] is already MonoBehaviour (mandatory on Unity 5.3;
# the default Application entrypoint corrupts NGUI scene deserialization).
$Vendor = "$PSScriptRoot\third_party\bepinex"
foreach ($f in @("winhttp.dll", "doorstop_config.ini", ".doorstop_version")) {
    Copy-Item "$Vendor\$f" "$Stage\$f"
}
New-Item -ItemType Directory -Path "$Stage\BepInEx\core", "$Stage\BepInEx\config" -Force | Out-Null
Copy-Item "$Vendor\BepInEx\core\*" "$Stage\BepInEx\core\"
Copy-Item "$Vendor\BepInEx\config\BepInEx.cfg" "$Stage\BepInEx\config\BepInEx.cfg"

# Mod DLLs from the plugin's build output (Core lands there transitively).
$BuildDir = "$PSScriptRoot\HandOfFateAccess\bin\Release"
foreach ($name in @("HandOfFateAccess.dll", "HandOfFateAccess.Core.dll")) {
    Copy-Item "$BuildDir\$name" "$PluginsDir\$name"
}

# Native runtimes: the x86 Tolk screen-reader stack (DLLs + their .ini
# companions, no dev headers) and the vendored x86 SAPI shim.
Get-ChildItem "$PSScriptRoot\third_party\tolk\dist\x86" -Include *.dll, *.ini -File -Recurse |
    Copy-Item -Destination $PluginsDir
Copy-Item "$PSScriptRoot\native\hofsapi\HofSapi.dll" "$PluginsDir\HofSapi.dll"

# Authored sound cues, loaded at runtime from a sounds folder beside the DLL.
New-Item -ItemType Directory -Path "$PluginsDir\sounds" -Force | Out-Null
Copy-Item "$PSScriptRoot\sounds\*.wav" "$PluginsDir\sounds\"

# --- Verify the staged layout before zipping ---
$required = @(
    "winhttp.dll",
    "doorstop_config.ini",
    "BepInEx\core\BepInEx.dll",
    "BepInEx\core\BepInEx.Preloader.dll",
    "BepInEx\core\0Harmony.dll",
    "BepInEx\config\BepInEx.cfg",
    "BepInEx\plugins\HandOfFateAccess.dll",
    "BepInEx\plugins\HandOfFateAccess.Core.dll",
    "BepInEx\plugins\Tolk.dll",
    "BepInEx\plugins\nvdaControllerClient32.dll",
    "BepInEx\plugins\HofSapi.dll",
    "BepInEx\plugins\sounds\walltone_left.wav"
)
$missing = $required | Where-Object { -not (Test-Path "$Stage\$_") }
if ($missing) {
    Write-Host "ERROR: staged release is missing:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}
$cfg = Get-Content "$Stage\BepInEx\config\BepInEx.cfg" -Raw
if ($cfg -notmatch '(?ms)\[Preloader\.Entrypoint\].*?^\s*Type\s*=\s*MonoBehaviour') {
    Write-Host "ERROR: staged BepInEx.cfg entrypoint is not MonoBehaviour." -ForegroundColor Red
    exit 1
}

# --- Zip ---
$Zip = "$ReleaseDir\HandOfFateAccess-v$Version.zip"
if (Test-Path $Zip) { Remove-Item $Zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($Stage, $Zip)

$size = "{0:N1}" -f ((Get-Item $Zip).Length / 1MB)
Write-Host ""
Write-Host "Release ready: $Zip ($size MB)" -ForegroundColor Green
Write-Host "Players extract it directly into the game folder and play." -ForegroundColor Cyan
