# GitHub PR Creation Script

## Step 1: Create PR using GitHub CLI

```powershell
cd C:\Users\biges\Desktop\Wiley-Widget

# Ensure you have GitHub CLI installed
# If not: choco install gh  (or download from https://github.com/cli/cli/releases)

# Create the PR with automatic title from commit and body from description
gh pr create `
  --base main `
  --head fix/memorycache-disposal-and-theme-initialization `
  --title "feat: Complete QuickBooks integration with Polly v8 resilience and all 14 methods" `
  --body-file PR_DESCRIPTION.md `
  --draft

# Or if you want it ready immediately (not draft):
gh pr create `
  --base main `
  --head fix/memorycache-disposal-and-theme-initialization `
  --title "feat: Complete QuickBooks integration with Polly v8 resilience and all 14 methods" `
  --body-file PR_DESCRIPTION.md
```

## Step 2: After PR is Created

Once the PR is created, you can:

```powershell
# View the PR
gh pr view

# Add labels
gh pr edit --add-label "feature,testing,backend,quickbooks"

# Add assignees
gh pr edit --assignee "@me"

# Request review from Grok
gh pr comment -b "@grok Please review this PR with focus on security, resilience, and Intuit API compliance. See PR description for review checklist."
```

## Step 3: Enable CI/CD

The PR will automatically trigger:

- ✅ Build workflow (dotnet build)
- ✅ Test workflows (xUnit tests)
- ✅ Code quality checks
- ✅ Security scanning (if configured)

## Manual Steps (if GitHub CLI not available)

1. Go to: https://github.com/Bigessfour/Wiley-Widget
2. Click "Compare & pull request" (should appear automatically)
3. Set:
   - Base: `main`
   - Compare: `fix/memorycache-disposal-and-theme-initialization`
4. Copy/paste PR_DESCRIPTION.md content into description
5. Click "Create pull request"
6. Add labels: feature, testing, backend, quickbooks
7. Add comment: `@grok Review requested - see description for checklist`

---

## Expected Result

✅ PR created and linked to branch  
✅ CI/CD workflows triggered automatically  
✅ Grok notified for review  
✅ Ready for approval and merge
