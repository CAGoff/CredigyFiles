# verify.ps1 — Run all quality checks.
# Claude Code uses this for self-iteration.
$ErrorActionPreference = "Stop"

Write-Host "?? Running quality gates..." -ForegroundColor Cyan

# Uncomment/modify for your stack:
# npm run typecheck;  if ($?) { Write-Host "? Types OK" -ForegroundColor Green }
# npm run lint;       if ($?) { Write-Host "? Lint OK" -ForegroundColor Green }
# npm run test;       if ($?) { Write-Host "? Tests OK" -ForegroundColor Green }

Write-Host "?? All checks passed!" -ForegroundColor Green
