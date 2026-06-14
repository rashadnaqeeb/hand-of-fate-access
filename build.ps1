# build.ps1 - Build HandOfFateAccess and deploy the plugin DLL plus the x86
# Tolk runtime into the game's BepInEx\plugins folder.

param(
    [switch]$NoBuild,
    [switch]$Fmod,
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-NoBuild] [-Fmod] [-Help]"
    Write-Host "  -NoBuild  Skip building; just redeploy the last built DLL and native deps"
    Write-Host "  -Fmod     Build with the FMOD audio backend (needs the vendored FMOD SDK;"
    Write-Host "            see third_party\fmod\README.md)"
    Write-Host "  -Help     Show this help"
    exit 0
}

$ErrorActionPreference = "Stop"

# --- Locate the game install ---
# HOF_GAME env var wins; otherwise auto-detect from Steam library folders;
# otherwise fall back to the default location.
$Game = $env:HOF_GAME
if (-not $Game) {
    $RegSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    $DefaultSteam = if ($RegSteam) { $RegSteam } else { "C:\Program Files (x86)\Steam" }
    $SteamPaths = @()
    if (Test-Path "$DefaultSteam\steamapps") { $SteamPaths += $DefaultSteam }
    $LibFolders = "$DefaultSteam\steamapps\libraryfolders.vdf"
    if (Test-Path $LibFolders) {
        $content = Get-Content $LibFolders -Raw
        [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object {
            $p = $_.Groups[1].Value -replace '\\\\', '\'
            if ($p -ne $DefaultSteam -and (Test-Path "$p\steamapps")) { $SteamPaths += $p }
        }
    }
    foreach ($steam in $SteamPaths) {
        $candidate = "$steam\steamapps\common\Hand of Fate"
        if (Test-Path "$candidate\Hand of Fate.exe") { $Game = $candidate; break }
    }
    if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\Hand of Fate" }
}

if (-not (Test-Path "$Game\Hand of Fate.exe")) {
    Write-Host "ERROR: Hand of Fate not found at: $Game" -ForegroundColor Red
    Write-Host "Set the HOF_GAME environment variable to the game folder." -ForegroundColor Red
    exit 1
}
$env:HOF_GAME = $Game

$PluginsDir = "$Game\BepInEx\plugins"
if (-not (Test-Path "$Game\BepInEx")) {
    Write-Host "ERROR: BepInEx is not installed at $Game\BepInEx." -ForegroundColor Red
    Write-Host "Install BepInEx 5.x (x86) into the game folder first." -ForegroundColor Red
    exit 1
}

$ProjectDir  = "$PSScriptRoot\HandOfFateAccess"
$BuildOutput = "$ProjectDir\bin\Release\HandOfFateAccess.dll"

# --- Build ---
if (-not $NoBuild) {
    Write-Host "Building HandOfFateAccess (game: $Game)..." -ForegroundColor Cyan
    $BuildArgs = @("build", "$ProjectDir\HandOfFateAccess.csproj", "-c", "Release")
    if ($Fmod) {
        $FmodBinding = "$PSScriptRoot\third_party\fmod\binding\fmod.cs"
        if (-not (Test-Path $FmodBinding)) {
            Write-Host "ERROR: -Fmod set but the FMOD binding is missing at $FmodBinding" -ForegroundColor Red
            Write-Host "Drop the SDK's fmod.cs there (see third_party\fmod\README.md)." -ForegroundColor Red
            exit 1
        }
        $BuildArgs += "-p:HofFmod=true"
        Write-Host "FMOD backend enabled (HofFmod=true)." -ForegroundColor Cyan
    }
    dotnet @BuildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build FAILED." -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $BuildOutput)) {
    Write-Host "ERROR: DLL not found at $BuildOutput" -ForegroundColor Red
    exit 1
}

# --- Deploy ---
if (-not (Test-Path $PluginsDir)) {
    New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
}
# Deploy the plugin and its Core dependency (BepInEx resolves Core.dll from the
# plugins folder). Both land in the plugin's build output.
$BuildDir = Split-Path $BuildOutput -Parent
foreach ($name in @("HandOfFateAccess.dll", "HandOfFateAccess.Core.dll")) {
    Copy-Item "$BuildDir\$name" "$PluginsDir\$name" -Force
}
Write-Host "Deployed HandOfFateAccess.dll + Core to $PluginsDir" -ForegroundColor Green

# Deploy the x86 Tolk runtime (Tolk.dll + screen-reader client DLLs + their
# .ini companions). Co-located with the plugin so NativeLoader can find them.
$TolkSrc = "$PSScriptRoot\third_party\tolk\dist\x86"
$TolkFiles = Get-ChildItem -Path $TolkSrc -Include *.dll, *.ini -File -Recurse
foreach ($f in $TolkFiles) {
    Copy-Item $f.FullName "$PluginsDir\$($f.Name)" -Force
}
Write-Host "Deployed $($TolkFiles.Count) Tolk runtime file(s) to $PluginsDir" -ForegroundColor Green

# Deploy the vendored x86 SAPI shim (HofSapi.dll). Built separately from native\hofsapi
# via build.bat; the gambit's spoken card statuses render through it. Co-located with the
# plugin so NativeLoader can find it.
$SapiDll = "$PSScriptRoot\native\hofsapi\HofSapi.dll"
if (Test-Path $SapiDll) {
    Copy-Item $SapiDll "$PluginsDir\HofSapi.dll" -Force
    Write-Host "Deployed HofSapi.dll to $PluginsDir" -ForegroundColor Green
} else {
    Write-Host "WARNING: HofSapi.dll not found at $SapiDll; build it via native\hofsapi\build.bat" -ForegroundColor Yellow
}

# Deploy the vendored x86 FMOD runtime (fmod.dll) when present, so the FMOD backend's
# P/Invoke resolves it via NativeLoader. Only the runtime dll is shipped, never the SDK.
$FmodDll = "$PSScriptRoot\third_party\fmod\lib\x86\fmod.dll"
if (Test-Path $FmodDll) {
    Copy-Item $FmodDll "$PluginsDir\fmod.dll" -Force
    Write-Host "Deployed fmod.dll to $PluginsDir" -ForegroundColor Green
} elseif ($Fmod) {
    Write-Host "WARNING: -Fmod set but fmod.dll not found at $FmodDll" -ForegroundColor Yellow
}

# Deploy the authored audio cues (wall tones, etc). The plugin loads these at
# runtime from a sounds folder beside its DLL, so mirror the repo's sounds folder.
$SoundsSrc = "$PSScriptRoot\sounds"
if (Test-Path $SoundsSrc) {
    $SoundsDst = "$PluginsDir\sounds"
    if (-not (Test-Path $SoundsDst)) { New-Item -ItemType Directory -Path $SoundsDst -Force | Out-Null }
    $SoundFiles = Get-ChildItem -Path $SoundsSrc -Filter *.wav -File
    foreach ($f in $SoundFiles) {
        Copy-Item $f.FullName "$SoundsDst\$($f.Name)" -Force
    }
    Write-Host "Deployed $($SoundFiles.Count) sound file(s) to $SoundsDst" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Launch the game and listen for the startup line." -ForegroundColor Cyan
