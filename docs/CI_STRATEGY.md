# CI/CD Workflow Strategy - Polish Phase

**Last Updated:** January 23, 2026  
**Status:** Active  
**Scope:** Wiley-Widget PR #36 (Polish) and ongoing

## Overview

This document outlines the recommended CI/CD workflows for the Wiley-Widget project, focused on measurable improvements during the Polish phase. All workflows are configured for GitHub Actions with emphasis on speed, reliability, and code quality validation.

---

## 🎯 Recommended Workflows

### 1. **Fast PR Feedback** (`fast-pr-feedback.yml`)
**Trigger:** Pull requests to `main` or `develop`  
**Runs on:** Windows (required for WinForms)  
**Duration:** ~15-20 minutes  
**Concurrency:** Cancels previous runs for the same PR

**What it does:**
- ✅ Format check (whitespace only, fail-fast)
- ✅ NuGet restore (with caching)
- ✅ Quick build of core abstractions
- ✅ Service unit tests
- ✅ WinForms theme regression tests (filtered)
- ✅ Uploads test results as artifacts

**Measurable Benefits:**
- Quick feedback loop (< 20 min)
- Caches reduce restore time by ~60%
- Filtered tests focus on high-risk areas (theming, UI)
- Artifact uploads enable manual review

**When to use:**
- Every PR to ensure no regressions
- Pre-merge validation gate

---

### 2. **Grok Code Review** (`main.yml`)
**Trigger:** Pull requests with C# changes to `main` or `develop`  
**Runs on:** Ubuntu  
**Duration:** ~5-10 minutes  
**Concurrency:** Cancels previous reviews for same PR

**What it does:**
- ✅ Extracts PR diff (C# and tests only)
- ✅ Skips reviews for minor changes (< 10 lines)
- ✅ Validates `XAI_API_KEY` secret availability
- ✅ Calls Grok API with tailored code review prompt
- ✅ Posts review as PR comment
- ✅ Handles API failures gracefully

**Review Focus (Prompt):**
- **Theme consistency:** No manual `Color.FromArgb`, all via `SfSkinManager`
- **Syncfusion API:** Correct properties, events, docking
- **Testability:** Interface-based mocking, no parameterless ctors
- **WinForms:** Proper disposal, no blocking calls, STA-friendly
- **Security:** No hard-coded keys/paths
- **Performance:** No heavy UI-thread ops
- **.NET 10:** Modern C# features encouraged

**Measurable Benefits:**
- Automated code review reduces human cycle time
- Grok catches Syncfusion/WinForms violations
- Comments appear directly on PR (GitHub integration)
- Graceful degradation if API unavailable

**When to use:**
- All C# PRs for consistent feedback
- Pairs with human reviews for confidence

**Prerequisites:**
- `XAI_API_KEY` secret configured in GitHub Settings
- Verify at: Settings → Secrets and variables → Actions → New repository secret

---

### 3. **.NET Build & Test** (`dotnet.yml`)
**Trigger:** Pushes or PRs to `main` or `develop`  
**Runs on:** Windows  
**Duration:** ~30-40 minutes  
**Concurrency:** Cancels previous runs for same ref

**What it does:**
- ✅ Restores NuGet packages (with caching)
- ✅ Full Release build
- ✅ Runs all tests with code coverage collection
- ✅ Generates HTML coverage report
- ✅ Publishes test results via GitHub Checks
- ✅ Uploads artifacts (test results, coverage)

**Measurable Benefits:**
- Comprehensive test validation (all test suites)
- Code coverage reports (track >85% target)
- GitHub Checks integration (native PR status)
- Artifact uploads for historical tracking
- 7-day retention for compliance

**When to use:**
- All pushes to main/develop
- Nightly full validation
- Release validation gate

**Outputs:**
- `test-results-{run_id}/` - TRX files for each project
- `coverage-report-{run_id}/` - HTML coverage (open `coverage/index.html`)

---

### 4. **Syncfusion Theming Compliance** (`syncfusion-theming.yml`)
**Trigger:** PR/push to `main`/`develop` affecting WinForms code  
**Runs on:** Ubuntu  
**Duration:** ~5 minutes  
**Concurrency:** Cancels previous runs for same ref

**What it does:**
- ✅ Checks for Syncfusion theme violations
- ✅ Validates no manual color assignments
- ✅ Ensures `SfSkinManager` is sole authority
- ✅ Reports compliance status

**Violations Detected:**
- Direct `BackColor`/`ForeColor` assignments (except semantic status)
- Custom color dictionaries/properties
- `Color.FromArgb()` manual assignments
- Competing theme managers

**Measurable Benefits:**
- Prevents theme inconsistencies
- Automated enforcement of SfSkinManager rules
- Fast feedback (< 5 min)
- Supports PR merge gates

**When to use:**
- All WinForms PRs
- Ensures consistent UI polish

---

### 5. **Polish Validation** (`polish-validation.yml`) ⭐ NEW
**Trigger:** PR/push with code changes to `main`/`develop`  
**Runs on:** Windows  
**Duration:** ~40-50 minutes  
**Concurrency:** Cancels previous runs for same ref

**Purpose:** Comprehensive validation during Polish phase focusing on UI, data, theming, and MSSQL integration.

**What it does:**
- ✅ Full build (Release)
- ✅ UI/Theme tests (filtered, `Category=UI|Theme`)
- ✅ Data/Integration tests (filtered, `Category=Data|Integration|Database`)
- ✅ Syncfusion theming compliance check
- ✅ MSSQL connection validation (setup guide)
- ✅ Test results upload & GitHub Checks integration

**Measurable Benefits:**
- Single workflow for Polish-phase validation
- Focused test filtering (faster than full suite)
- Theme compliance built-in
- MSSQL setup guidance (actionable next steps)
- Full artifact trail for debugging

**When to use:**
- All Polish phase PRs
- Before merging to main
- Final validation gate

**Outputs:**
- UI/Theme test results → `TestResults/UITheme/`
- Data/Integration results → `TestResults/DataIntegration/`
- GitHub Checks with summary
- Artifacts retained 7 days

---

## 📋 Workflow Selection Matrix

| Scenario | Fast PR Feedback | Grok Review | .NET Build & Test | Theme Check | Polish Validation |
|----------|-----------------|-------------|-------------------|-------------|-------------------|
| **Quick PR feedback** | ✅ Yes | ✅ Yes | ❌ (too slow) | ✅ Yes | ❌ (too slow) |
| **Full test coverage** | ❌ (filtered) | ❌ (code only) | ✅ Yes | ❌ (code only) | ✅ Partial |
| **Code review insights** | ❌ (no AI) | ✅ Yes | ❌ (no AI) | ❌ (binary) | ❌ (no AI) |
| **Theme validation** | ❌ (no theme test) | ✅ (prompt) | ❌ (no check) | ✅ Yes | ✅ Yes |
| **MSSQL validation** | ❌ No | ❌ No | ❌ No | ❌ No | ✅ Setup guide |
| **Data flow checks** | ❌ No | ❌ No | ✅ Full | ❌ No | ✅ Filtered |

---

## 🚀 Recommended PR Workflow

1. **Push to branch** → Triggers all workflows
2. **~5 min:** Grok review posted (code insights)
3. **~15 min:** Fast PR Feedback complete (quick gate)
   - If failures → Fix and push
   - If success → Ready for review
4. **~40 min:** .NET Build & Test + Polish Validation complete
   - Coverage reports uploaded
   - All tests validated
5. **Merge:** All checks passing + human review approval

---

## 🔧 Configuration & Secrets

### Required Secrets
- **`XAI_API_KEY`** (for Grok review)
  - Get from: https://console.x.ai
  - Set at: GitHub Settings → Secrets and variables → Actions
  - Verify: Run PR to main/develop, Grok should comment

### Optional Secrets
- **MSSQL credentials** (for Data/Integration tests)
  - If needed: Add `MSSQL_CONNECTION_STRING` secret
  - Used by: Polish Validation (MSSQL validation step)

### Caching Strategy
All workflows use NuGet package caching:
```yaml
- uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: nuget-${{ runner.os }}-${{ hashFiles('**/packages.lock.json') }}
```
**Impact:** Saves ~3-5 minutes per restore on cache hit

---

## 📊 Metrics to Track

### Build Success Rate
- Target: > 95% (catch pre-merge issues)
- Track: GitHub Actions dashboard
- Action: Investigate > 1 consecutive failure

### Test Pass Rate
- Target: > 90% (allow for flaky tests)
- Track: Test results artifacts
- Action: Quarantine flaky tests, investigate failures

### Code Coverage
- Target: > 85% (focus on business logic)
- Track: Coverage reports (HTML or Codecov)
- Action: Add tests for new code if coverage drops

### Grok Review Completion
- Target: 100% (for C# PRs with > 10 lines changed)
- Track: PR comments
- Action: Verify `XAI_API_KEY` if reviews missing

### Theming Compliance
- Target: 100% pass rate
- Track: Syncfusion theming workflow status
- Action: Fix violations before merge

---

## 🐛 Troubleshooting

### Grok Review Not Appearing
**Symptom:** PR has C# changes but no Grok comment  
**Likely cause:** 
- `XAI_API_KEY` not set or expired
- Changes < 10 lines (skipped intentionally)
- API rate limit hit

**Fix:**
1. Check secret: Settings → Secrets → Verify `XAI_API_KEY`
2. Check workflow logs: Actions tab → workflow run → Grok step
3. Wait 1 hour if rate limited, then retry

### Fast PR Feedback Timeout
**Symptom:** Workflow runs > 25 minutes  
**Likely cause:**
- NuGet restore cache miss (first run or packages.lock.json changed)
- WinForms tests stuck

**Fix:**
1. Check "Cache hit": Look for cache step output
2. Retry (cache should hit on 2nd run)
3. If stuck: Check WinForms test output for hangs

### Theme Compliance Check Failing
**Symptom:** Workflow red, message about manual colors  
**Fix:**
1. Read check output (view workflow run)
2. Identify files with violations
3. Remove manual `BackColor`/`ForeColor` assignments
4. Use `SfSkinManager.SetVisualStyle()` instead
5. Push to re-run workflow

### MSSQL Validation Failing
**Symptom:** Data/Integration tests time out or fail  
**Fix:**
1. Verify MSSQL is running locally (if testing)
2. Check `MSSQL_CONNECTION_STRING` environment variable
3. Run import scripts manually: `./scripts/import-budget-data.ps1`
4. Validate FY2026 budget data exists in DB

---

## 📝 Next Steps

1. **Verify workflows are enabled:**
   - Go to: GitHub repo → Actions tab
   - Check: All workflows have green checkmarks

2. **Test Grok review:**
   - Push small PR to `develop` with C# changes
   - Wait ~5 min for Grok comment
   - If missing, check `XAI_API_KEY` secret

3. **Run Polish Validation locally:**
   - Branch: `main` or `develop`
   - Command: `dotnet build && dotnet test`
   - Compare with GitHub Actions results

4. **Monitor metrics:**
   - Set up GitHub Actions dashboard alerts
   - Track coverage reports weekly
   - Review test failures bi-weekly

---

## 🎯 Success Criteria

✅ **Fast PR Feedback:** < 20 min, < 5 test failures  
✅ **Grok Review:** Posted on 100% of PRs (XAI_API_KEY configured)  
✅ **Build & Test:** All tests passing, > 85% coverage  
✅ **Theme Compliance:** 100% pass rate  
✅ **Polish Validation:** All filtered tests passing  

---

**Document Version:** 1.0  
**Last Updated:** 2026-01-23  
**Maintainer:** GitHub Copilot (AI-assisted)
