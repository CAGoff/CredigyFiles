# verify.ps1 — Run all quality checks.
# Claude Code uses this for self-iteration.
$ErrorActionPreference = "Stop"
$solutionRoot = Join-Path $PSScriptRoot ".."

Write-Host "Running quality gates..." -ForegroundColor Cyan

# .NET build
Write-Host "`n--- dotnet build ---" -ForegroundColor Yellow
dotnet build "$solutionRoot\SecureFileTransfer.sln" --verbosity minimal
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }
Write-Host "Build OK" -ForegroundColor Green

# .NET tests
Write-Host "`n--- dotnet test ---" -ForegroundColor Yellow
dotnet test "$solutionRoot\SecureFileTransfer.sln" --no-build --verbosity minimal
if ($LASTEXITCODE -ne 0) { Write-Host "TESTS FAILED" -ForegroundColor Red; exit 1 }
Write-Host "Tests OK" -ForegroundColor Green

Write-Host "`nAll checks passed!" -ForegroundColor Green
