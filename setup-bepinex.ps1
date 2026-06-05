# setup-bepinex.ps1 - Install the vendored BepInEx (third_party/bepinex) into the
# game folder and guarantee the Unity-5.3 entrypoint fix. Idempotent: safe to
# re-run. After this, run build.ps1 to deploy the mod plugin.

$ErrorActionPreference = "Stop"

# --- Locate the game install (same resolution as build.ps1) ---
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

$Vendor = "$PSScriptRoot\third_party\bepinex"
if (-not (Test-Path "$Vendor\winhttp.dll")) {
    Write-Host "ERROR: vendored BepInEx not found at $Vendor" -ForegroundColor Red
    exit 1
}

Write-Host "Installing BepInEx into $Game ..." -ForegroundColor Cyan

# --- Copy the loader proxy + doorstop config + core ---
foreach ($f in @("winhttp.dll", "doorstop_config.ini", "changelog.txt", ".doorstop_version")) {
    if (Test-Path "$Vendor\$f") { Copy-Item "$Vendor\$f" "$Game\$f" -Force }
}
$CoreDest = "$Game\BepInEx\core"
New-Item -ItemType Directory -Path $CoreDest -Force | Out-Null
Copy-Item "$Vendor\BepInEx\core\*" $CoreDest -Force
New-Item -ItemType Directory -Path "$Game\BepInEx\plugins" -Force | Out-Null

# --- Guarantee the MonoBehaviour entrypoint ---
$CfgDir  = "$Game\BepInEx\config"
$CfgPath = "$CfgDir\BepInEx.cfg"
if (-not (Test-Path $CfgPath)) {
    # No config yet: drop in the seed (already set to MonoBehaviour).
    New-Item -ItemType Directory -Path $CfgDir -Force | Out-Null
    Copy-Item "$Vendor\BepInEx\config\BepInEx.cfg" $CfgPath -Force
    Write-Host "Seeded BepInEx.cfg with MonoBehaviour entrypoint." -ForegroundColor Green
} else {
    # Config exists (BepInEx wrote it on a prior run): patch the entrypoint in place.
    $cfg = Get-Content $CfgPath -Raw
    if ($cfg -match '(?ms)\[Preloader\.Entrypoint\].*?^\s*Type\s*=') {
        $patched = [regex]::Replace($cfg, '(?ms)(\[Preloader\.Entrypoint\].*?^\s*Type\s*=\s*)\S+', '${1}MonoBehaviour', 1)
        [System.IO.File]::WriteAllText($CfgPath, $patched)
        Write-Host "Patched existing BepInEx.cfg entrypoint to MonoBehaviour." -ForegroundColor Green
    } else {
        # Section/key not present: append the seed block.
        Add-Content $CfgPath "`r`n[Preloader.Entrypoint]`r`nAssembly = UnityEngine.dll`r`nType = MonoBehaviour`r`nMethod = .cctor`r`n"
        Write-Host "Appended MonoBehaviour entrypoint to BepInEx.cfg." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "BepInEx installed. Now run build.ps1 to deploy the mod." -ForegroundColor Cyan
