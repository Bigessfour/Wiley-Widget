# Dashboard Feature PR Merge Checklist

## Pre-Merge Validation

### CI/CD Requirements
- [ ] CI pipeline passes with 0 errors (build + test + publish)
- [ ] All 12 E2E tests passing in CI environment
- [ ] Build artifacts successfully uploaded (`wiley-widget-winforms.zip`)
- [ ] No blocking code analysis warnings (CA1xxx, NETSDK1xxx)
- [ ] Test coverage report generated (optional but recommended)

### Code Quality
- [ ] Local build succeeds: `dotnet build WileyWidget.sln --configuration Release`
- [ ] All unit tests pass: `dotnet test --no-build --configuration Release`
- [ ] Zero compiler warnings in Release build
- [ ] Trunk security scan clean: `trunk check --all --ci`
- [ ] Problems panel shows 0 errors/warnings in VS Code

### Dashboard Feature Validation
- [ ] DashboardViewModel implements async loading with error handling
- [ ] DashboardService integrates with repository pattern
- [ ] E2E tests cover: budget, revenue, expenses, metrics, caching
- [ ] Fiscal year logic uses current year (`DateTime.Now.Year`)
- [ ] Revenue accounts use correct prefix (3xx) in repository logic
- [ ] Dashboard UI renders without exceptions (manual smoke test)

### Documentation
- [ ] LICENSE file added (MIT) ✅
- [ ] README updated with dashboard feature description
- [ ] CHANGELOG entry for dashboard implementation
- [ ] API documentation for new services (`IDashboardService`, `IDashboardRepository`)
- [ ] Architecture decision recorded for Syncfusion usage

### Dependencies & Security
- [ ] No new security vulnerabilities introduced (osv-scanner clean)
- [ ] Syncfusion license compliance documented
- [ ] NuGet packages up-to-date (no critical CVEs)
- [ ] Docker images for MCP servers updated if needed

### Branch Hygiene
- [ ] Branch rebased on latest `main` (or merge main into feature branch)
- [ ] Commit messages follow conventional commits format
- [ ] No merge conflicts with `main`
- [ ] Squash commits if needed (group related fixes)

## Merge Process

1. **Final CI Check**: Ensure latest commit has green checkmark
2. **Review PR Description**: Summarize changes, link related issues
3. **Request Reviews**: Tag team members for code review
4. **Address Feedback**: Make requested changes, re-run CI
5. **Merge Strategy**: Use "Squash and merge" for clean history
6. **Post-Merge**: Delete feature branch, verify main CI passes

## Rollback Plan

If issues arise post-merge:
1. **Quick Fix**: Push hotfix commit to main if < 15 min fix
2. **Revert**: Use `git revert` to rollback merge commit
3. **New Branch**: Create fix branch from main, re-test, re-merge

## Success Criteria

- ✅ CI pipeline green with 95%+ success rate
- ✅ Dashboard loads in < 2 seconds with test data
- ✅ No console errors or exceptions during normal operation
- ✅ Artifacts downloadable and runnable on Windows 10/11
- ✅ Code coverage >= 70% for new dashboard services
- ✅ Zero high-severity security issues

## Contact

For merge approval: @Bigessfour
CI/CD issues: Check `.github/workflows/build-winforms.yml`
Test failures: Review `tests/WileyWidget.Services.Tests/Integration/DashboardE2ETests.cs`
