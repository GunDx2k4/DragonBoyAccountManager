param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$currentBranch = (git branch --show-current).Trim()
if ($currentBranch -eq "main" -or $currentBranch -eq "master") {
    Write-Host "[WARN] Already on trunk branch '$currentBranch'." -ForegroundColor Yellow
    exit 0
}

Write-Host "[VERIFY] Running pre-merge verification on '$currentBranch'..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "verify-all.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Error "[FAIL] Verification failed. Cannot merge branch '$currentBranch'."
    exit 1
}

Write-Host "[GIT] Switching to main..." -ForegroundColor Cyan
git checkout main
if ($LASTEXITCODE -ne 0) {
    Write-Error "[FAIL] Failed to switch to main."
    exit 1
}

Write-Host "[GIT] Merging branch '$currentBranch' into main..." -ForegroundColor Cyan
git merge --ff-only $currentBranch
if ($LASTEXITCODE -ne 0) {
    Write-Host "[GIT] Fast-forward merge failed. Falling back to rebase/merge..." -ForegroundColor Yellow
    git merge $currentBranch -m "merge($currentBranch): integrate verified feature branch"
}

Write-Host "[GIT] Deleting topic branch '$currentBranch'..." -ForegroundColor Cyan
git branch -d $currentBranch

Write-Host "[OK] Feature branch '$currentBranch' merged and cleaned up successfully." -ForegroundColor Green
