param(
    [switch]$Detailed
)

$ErrorActionPreference = "Stop"

Write-Host "[VERIFY] Building solution DBAM.slnx..." -ForegroundColor Cyan
dotnet build DBAM.slnx --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "[FAIL] Solution compilation failed."
    exit 1
}

Write-Host "[VERIFY] Running test suite..." -ForegroundColor Cyan
if ($Detailed) {
    dotnet test DBAM.slnx --nologo
} else {
    dotnet test DBAM.slnx --nologo --verbosity normal
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "[FAIL] Test suite failed."
    exit 1
}

Write-Host "[OK] All builds and tests passed successfully." -ForegroundColor Green
exit 0
