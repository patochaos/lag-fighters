# Verificación completa sin abrir Unity:
#   pwsh Tools/verify.ps1            (rápido: compile + tests)
#   pwsh Tools/verify.ps1 -Lab       (además corre el lab de balance)
param([switch]$Lab, [int]$Peleas = 2000)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fail = $false

Write-Host "== 1/3 compile-check (scripts del juego contra los DLL de Unity) ==" -ForegroundColor Cyan
dotnet build "$root\Tools\CompileCheck\check.csproj" -v q -nologo | Select-String "error" | ForEach-Object { $_.ToString() }
if ($LASTEXITCODE -ne 0) { $fail = $true; Write-Host "COMPILE-CHECK FALLÓ" -ForegroundColor Red }
else { Write-Host "compile ok" -ForegroundColor Green }

Write-Host "== 2/3 tests de framedata ==" -ForegroundColor Cyan
dotnet run --project "$root\Tools\SimTests" -c Release -v q
if ($LASTEXITCODE -ne 0) { $fail = $true; Write-Host "TESTS FALLARON" -ForegroundColor Red }

if ($Lab) {
    Write-Host "== 3/3 lab de balance ($Peleas peleas IA vs IA) ==" -ForegroundColor Cyan
    dotnet run --project "$root\Tools\SimHarness" -c Release -v q -- $Peleas
    if ($LASTEXITCODE -ne 0) { $fail = $true }
} else {
    Write-Host "== 3/3 lab de balance: omitido (usar -Lab) ==" -ForegroundColor DarkGray
}

if ($fail) { Write-Host "`nVERIFICACIÓN CON FALLOS" -ForegroundColor Red; exit 1 }
Write-Host "`nTODO VERDE" -ForegroundColor Green
