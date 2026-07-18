# Release completo de Lag Fighters — correr SOLO con el OK explícito de Patricio.
#   pwsh Tools/release.ps1 -Version 0.4.0
# Hace: verify → build standalone → build WebGL → push a GitHub → butler a itch.
# Prerrequisitos: editor Unity CERRADO, CHANGELOG.md ya actualizado, butler logueado.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$unity = "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe"
$butler = "D:\Lag Fighters\Tools\butler\butler.exe"

if ((Get-Process Unity -ErrorAction SilentlyContinue) -or (Test-Path "$root\Temp\UnityLockfile")) {
    Write-Host "ABORT: el editor Unity está abierto (o el proyecto lockeado). Cerralo y volvé a correr." -ForegroundColor Red
    exit 1
}

if (-not $SkipVerify) {
    Write-Host "== verify ==" -ForegroundColor Cyan
    pwsh -NoProfile -File "$root\Tools\verify.ps1"
    if ($LASTEXITCODE -ne 0) { Write-Host "ABORT: verificación con fallos." -ForegroundColor Red; exit 1 }
}

Write-Host "== build standalone ==" -ForegroundColor Cyan
& $unity -batchmode -nographics -projectPath $root -executeMethod BuildScript.Build -logFile "$root\Tools\release_standalone.log" | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "ABORT: build standalone falló (ver Tools/release_standalone.log)." -ForegroundColor Red; exit 1 }

Write-Host "== build WebGL ==" -ForegroundColor Cyan
& $unity -batchmode -nographics -projectPath $root -executeMethod BuildScript.BuildWebGL -logFile "$root\Tools\release_webgl.log" | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "ABORT: build WebGL falló (ver Tools/release_webgl.log)." -ForegroundColor Red; exit 1 }

Write-Host "== push a GitHub ==" -ForegroundColor Cyan
git -C $root push origin main
if ($LASTEXITCODE -ne 0) { Write-Host "ABORT: git push falló." -ForegroundColor Red; exit 1 }

Write-Host "== butler a itch.io ==" -ForegroundColor Cyan
& $butler push --ignore="*_BurstDebugInformation_DoNotShip*" "D:\Lag Fighters\Builds\LagFightersWeb" patochaos/lag-fighters:html5 --userversion $Version
if ($LASTEXITCODE -ne 0) { Write-Host "ABORT: butler push falló." -ForegroundColor Red; exit 1 }

Write-Host "`nRELEASE $Version COMPLETO: standalone + web + GitHub + itch.io" -ForegroundColor Green
