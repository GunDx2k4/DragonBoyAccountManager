param(
    [Parameter(Mandatory = $true)]
    [string]$Name
)

$ErrorActionPreference = "Stop"

$cleanName = ($Name -replace '\s+', '-').ToLowerInvariant()
if (-not $cleanName.StartsWith("feat/")) {
    $branchName = "feat/$cleanName"
} else {
    $branchName = $cleanName
}

Write-Host "[GIT] Creating topic branch: $branchName" -ForegroundColor Cyan
git checkout -b $branchName
if ($LASTEXITCODE -ne 0) {
    Write-Error "[FAIL] Failed to create branch $branchName"
    exit 1
}

Write-Host "[OK] Switched to branch $branchName" -ForegroundColor Green
