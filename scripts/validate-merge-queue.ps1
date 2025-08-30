# Validate Merge Queue Setup
# This script checks if your repository is properly configured for Trunk Merge Queue

param(
    [switch]$Fix,
    [switch]$Verbose
)

Write-Host "🔍 Validating Trunk Merge Queue Setup..." -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Yellow

$checks = @()
$errors = @()

# Check 1: GitHub Secrets
Write-Host "`n1. Checking GitHub Secrets..." -ForegroundColor Green
try {
    $secrets = gh secret list 2>$null
    if ($secrets -match "TRUNK_ORG_URL_SLUG") {
        $checks += "✅ TRUNK_ORG_URL_SLUG secret found"
    } else {
        $errors += "❌ TRUNK_ORG_URL_SLUG secret missing"
    }

    if ($secrets -match "TRUNK_API_TOKEN") {
        $checks += "✅ TRUNK_API_TOKEN secret found"
    } else {
        $errors += "❌ TRUNK_API_TOKEN secret missing"
    }
} catch {
    $errors += "❌ Cannot access GitHub CLI (run 'gh auth login' first)"
}

# Check 2: Branch Protection
Write-Host "`n2. Checking Branch Protection..." -ForegroundColor Green
try {
    # Get repository information from git remote
    $remoteUrl = git remote get-url origin 2>$null
    if ($remoteUrl -match "github\.com[\/:]([^\/]+)\/(.+)\.git$") {
        $owner = $matches[1]
        $repo = $matches[2]
        Write-Host "   Repository: $owner/$repo" -ForegroundColor Gray

        $protection = gh api repos/$owner/$repo/branches/main/protection 2>$null | ConvertFrom-Json
        if ($protection -and $protection.PSObject.Properties.Name -contains "required_status_checks") {
            if ($protection.required_status_checks) {
                $checks += "✅ Branch protection enabled"
                $checks += "✅ Required status checks configured"
            } else {
                $errors += "❌ Branch protection enabled but no required status checks"
            }
        } elseif ($protection -and $protection.message -eq "Branch not protected") {
            $errors += "❌ Branch protection not configured"
        } else {
            $errors += "❌ Branch protection status unknown"
        }
    } else {
        $errors += "❌ Cannot determine repository from git remote"
    }
} catch {
    $errorMessage = $_.Exception.Message
    if ($errorMessage -match "404" -or $errorMessage -match "Branch not protected") {
        $errors += "❌ Branch protection not configured"
    } else {
        $errors += "❌ Cannot check branch protection (API error: $errorMessage)"
    }
}

# Check 3: Workflow Files
Write-Host "`n3. Checking Workflow Configuration..." -ForegroundColor Green
$workflowPath = ".github\workflows\merge-queue-cicd.yml"
if (Test-Path $workflowPath) {
    $checks += "✅ Merge queue workflow exists"

    $content = Get-Content $workflowPath -Raw
    if ($content -match "merge_group") {
        $checks += "✅ Merge group trigger configured"
    } else {
        $errors += "❌ Merge group trigger missing"
    }

    if ($content -match "trunk-io/analytics-uploader") {
        $checks += "✅ Trunk analytics uploader configured"
    } else {
        $errors += "❌ Trunk analytics uploader not found"
    }
} else {
    $errors += "❌ Merge queue workflow missing"
}

# Check 4: Trunk Configuration
Write-Host "`n4. Checking Trunk Configuration..." -ForegroundColor Green
$trunkConfig = ".trunk\trunk.yaml"
if (Test-Path $trunkConfig) {
    $checks += "✅ Trunk configuration exists"

    $content = Get-Content $trunkConfig -Raw
    if ($content -match "trunk-announce") {
        $checks += "✅ Trunk announce action enabled"
    }
} else {
    $errors += "❌ Trunk configuration missing"
}

# Summary
Write-Host "`n📊 Validation Summary" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Yellow

if ($checks.Count -gt 0) {
    Write-Host "`n✅ Passed Checks:" -ForegroundColor Green
    foreach ($check in $checks) {
        Write-Host "   $check" -ForegroundColor Green
    }
}

if ($errors.Count -gt 0) {
    Write-Host "`n❌ Issues Found:" -ForegroundColor Red
    foreach ($error in $errors) {
        Write-Host "   $error" -ForegroundColor Red
    }

    if ($Fix) {
        Write-Host "`n🔧 Attempting to fix issues..." -ForegroundColor Yellow

        # Try to fix missing workflow
        if ($errors -match "Merge queue workflow missing") {
            Write-Host "   Creating merge queue workflow..." -ForegroundColor Yellow
            # Note: This would require copying the workflow file
            Write-Host "   ⚠️  Manual step required: Copy merge-queue-cicd.yml to .github\workflows\" -ForegroundColor Yellow
        }
    } else {
        Write-Host "`n💡 Run with -Fix parameter to attempt automatic fixes" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n🎉 All checks passed! Your merge queue is ready." -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "   1. Create a test PR" -ForegroundColor White
    Write-Host "   2. Verify CI passes all checks" -ForegroundColor White
    Write-Host "   3. Add PR to merge queue in Trunk dashboard" -ForegroundColor White
    Write-Host "   4. Monitor the merge process" -ForegroundColor White
}

if ($Verbose) {
    Write-Host "`n🔍 Detailed Information:" -ForegroundColor Cyan
    Write-Host "   Total checks: $($checks.Count + $errors.Count)" -ForegroundColor White
    Write-Host "   Passed: $($checks.Count)" -ForegroundColor Green
    Write-Host "   Failed: $($errors.Count)" -ForegroundColor Red
}
