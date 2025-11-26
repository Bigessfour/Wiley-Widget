# Wiley Widget - WinForms Relaunch Git Commands
# Strategic pivot from WinUI 3 to WinForms production release
# Date: November 25, 2025

Write-Host "=== Wiley Widget v1.0-winforms-relaunch ===" -ForegroundColor Cyan
Write-Host ""

# Check git status
Write-Host "📊 Current Git Status:" -ForegroundColor Yellow
git status --short

Write-Host ""
Write-Host "The following changes will be committed:" -ForegroundColor Green
Write-Host "  ✅ Removed WinUI 3 project from solution" -ForegroundColor White
Write-Host "  ✅ Cleaned up legacy/archive folders" -ForegroundColor White
Write-Host "  ✅ Updated README.md with honest WinForms narrative" -ForegroundColor White
Write-Host "  ✅ Created docs/migration-plan.md decision documentation" -ForegroundColor White
Write-Host "  ✅ Updated CHANGELOG.md with v1.0.0-winforms entry" -ForegroundColor White
Write-Host "  ✅ Created .github/workflows/build-winforms.yml CI pipeline" -ForegroundColor White
Write-Host "  ✅ Removed WinUI packages from Directory.Packages.props" -ForegroundColor White
Write-Host ""

# Ask for confirmation
$confirm = Read-Host "Commit these changes? (y/n)"
if ($confirm -ne 'y') {
    Write-Host "❌ Aborted." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📝 Staging changes..." -ForegroundColor Yellow

# Stage all changes
git add .

Write-Host "✅ Changes staged" -ForegroundColor Green
Write-Host ""

Write-Host "💾 Creating commit..." -ForegroundColor Yellow

# Create commit with detailed message
git commit -m "feat: Strategic pivot to WinForms as production UI framework

BREAKING CHANGE: WinUI 3 removed, WinForms established as mainline

After 6+ weeks battling silent XamlCompiler crashes (Microsoft.UI.Xaml #10027)
in unpackaged Windows App SDK 1.6-1.8 on .NET 9, we made the pragmatic call:

- WinForms + .NET 9 + Syncfusion = stable, fast, production-ready
- 5-10x faster load times vs WinUI 3
- Zero XAML toolchain drama
- Mature Syncfusion ecosystem (20+ years)

Changes:
- Removed src/WileyWidget.WinUI/ project and all WinUI dependencies
- Cleaned up archive/, temp/, temp_test/, src/WileyWidget.Legacy/ folders
- Updated WileyWidget.sln to remove WinUI project references
- Rewrote README.md with honest WinForms pivot narrative
- Created docs/migration-plan.md with decision matrix and re-evaluation criteria
- Updated CHANGELOG.md with v1.0.0-winforms entry
- Created .github/workflows/build-winforms.yml CI pipeline
- Removed WinUI packages from Directory.Packages.props

See docs/migration-plan.md for full technical rationale.

We ship software, not toolchain drama.

Tag: v1.0-winforms-relaunch"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Commit created successfully" -ForegroundColor Green
    Write-Host ""
    
    # Show commit details
    Write-Host "📋 Commit Details:" -ForegroundColor Yellow
    git log -1 --stat
    
    Write-Host ""
    Write-Host "🏷️  Creating tag v1.0-winforms-relaunch..." -ForegroundColor Yellow
    
    # Create annotated tag
    git tag -a v1.0-winforms-relaunch -m "Strategic pivot: WinUI 3 → WinForms production release

WinForms + .NET 9 + Syncfusion established as stable mainline UI framework.

Key improvements:
- 5-10x faster load times
- Zero XAML compiler issues
- Production-ready deployment
- Mature Syncfusion WinForms ecosystem

WinUI 3 archived (Git history preserved) due to:
- Silent XamlCompiler crashes (Microsoft.UI.Xaml #10027)
- Unpackaged Windows App SDK instability
- 6+ weeks lost to toolchain issues

See docs/migration-plan.md for complete decision rationale.

Will revisit modern UI frameworks (WinUI 3, Avalonia) in 2026-2027."

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Tag created successfully" -ForegroundColor Green
        Write-Host ""
        
        # Show tag details
        Write-Host "🏷️  Tag Details:" -ForegroundColor Yellow
        git tag -n99 v1.0-winforms-relaunch
        
        Write-Host ""
        Write-Host "🚀 Ready to push!" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "To push changes and tag to remote, run:" -ForegroundColor Yellow
        Write-Host "  git push origin main" -ForegroundColor White
        Write-Host "  git push --tags" -ForegroundColor White
        Write-Host ""
        Write-Host "Or push everything at once:" -ForegroundColor Yellow
        Write-Host "  git push origin main --tags" -ForegroundColor White
        Write-Host ""
        
        # Ask if user wants to push now
        $pushNow = Read-Host "Push to remote now? (y/n)"
        if ($pushNow -eq 'y') {
            Write-Host ""
            Write-Host "📤 Pushing to remote..." -ForegroundColor Yellow
            git push origin main --tags
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host ""
                Write-Host "🎉 SUCCESS! Wiley Widget v1.0-winforms-relaunch deployed!" -ForegroundColor Green
                Write-Host ""
                Write-Host "Next steps:" -ForegroundColor Cyan
                Write-Host "  1. Verify GitHub Actions build: https://github.com/Bigessfour/Wiley-Widget/actions" -ForegroundColor White
                Write-Host "  2. Review migration plan: docs/migration-plan.md" -ForegroundColor White
                Write-Host "  3. Update project documentation as needed" -ForegroundColor White
                Write-Host ""
            } else {
                Write-Host ""
                Write-Host "⚠️  Push failed. Check remote configuration and try manually." -ForegroundColor Red
                Write-Host "  git push origin main --tags" -ForegroundColor White
                Write-Host ""
            }
        } else {
            Write-Host ""
            Write-Host "✅ Changes committed and tagged locally." -ForegroundColor Green
            Write-Host "   Push when ready with: git push origin main --tags" -ForegroundColor Yellow
            Write-Host ""
        }
        
    } else {
        Write-Host "❌ Tag creation failed" -ForegroundColor Red
        exit 1
    }
    
} else {
    Write-Host "❌ Commit failed" -ForegroundColor Red
    exit 1
}
