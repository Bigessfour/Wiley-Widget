# Wiley Widget - Merge Queue Configuration Guide
# Complete setup for Trunk Merge Queue integration

## 🚀 Overview
This guide provides step-by-step instructions for enabling and configuring GitHub Merge Queue with Trunk CI/CD integration for the Wiley Widget project.

## 📋 Prerequisites
- ✅ GitHub repository with merge queue enabled
- ✅ `merge-queue-cicd.yml` workflow configured
- ✅ Trunk CLI installed and authenticated
- ✅ Required GitHub secrets configured

## 🔧 Step 1: Enable Merge Queue in GitHub

### Via GitHub Web Interface:
1. Go to your repository: https://github.com/Bigessfour/Wiley-Widget
2. Navigate to **Settings** → **General** → **Pull Requests**
3. Check **Allow merge queue**
4. Choose merge method (recommend: **Merge commit** or **Squash and merge**)
5. Set **Merge queue conditions**:
   - ✅ Require branches to be up to date
   - ✅ Require status checks to pass
   - ✅ Require branches to be up to date before merging

### Required Status Checks:
Make sure these are required:
- `CI/CD with Merge Queue Support / Quality`
- `CI/CD with Merge Queue Support / Build & Test`
- `CI/CD with Merge Queue Support / Package & Deploy`

## ⚙️ Step 2: Configure Branch Protection Rules

### For Main Branch:
1. Go to **Settings** → **Branches** → **Add rule**
2. Branch name pattern: `main`
3. Enable:
   - ✅ Require a pull request before merging
   - ✅ Require status checks to pass
   - ✅ Require branches to be up to date
   - ✅ Include administrators
   - ✅ Restrict pushes that create matching branches

### Status Checks to Require:
```
CI/CD with Merge Queue Support / Quality
CI/CD with Merge Queue Support / Build & Test
CI/CD with Merge Queue Support / Package & Deploy
trunk-check
```

## 🔗 Step 3: Trunk Merge Queue Integration

### Current Configuration Status:
- ✅ `merge-queue-cicd.yml` workflow exists
- ✅ `merge_group` trigger configured
- ✅ Trunk security scans integrated
- ✅ Test result uploads configured

### Additional Trunk Configurations:

#### 1. Enable Merge Queue Actions
```yaml
# Add to .trunk/trunk.yaml actions section
actions:
  enabled:
    - trunk-announce
    - trunk-check-pre-push
    - trunk-fmt-pre-commit
    - trunk-upgrade-available
    - trunk-cache-prune
    - trunk-merge-queue-status  # Enable merge queue status reporting
```

#### 2. Configure Merge Queue Notifications
```yaml
# Add to trunk.yaml
notifications:
  merge_queue:
    enabled: true
    on_queue_entry: true
    on_merge: true
    on_failure: true
```

## 📊 Step 4: Monitor Merge Queue Performance

### Key Metrics to Track:
- **Queue Length**: Number of PRs waiting
- **Merge Time**: Average time from queue entry to merge
- **Success Rate**: Percentage of successful merges
- **Failure Rate**: Common failure patterns

### Monitoring Commands:
```powershell
# Check merge queue status
gh pr list --state open --json number,title,mergeStateStatus

# View recent merge activity
git log --oneline --since="1 week ago" --grep="Merge pull request"

# Check Trunk merge queue analytics
trunk actions history trunk-merge-queue-status
```

## 🚦 Step 5: Using Merge Queue

### For Contributors:
1. **Create PR**: Push your feature branch
2. **Add to Queue**: Use "Add to merge queue" button on PR
3. **Monitor Progress**: Watch status checks in real-time
4. **Auto-Merge**: PR merges automatically when all checks pass

### For Maintainers:
1. **Monitor Queue**: Check queue status regularly
2. **Handle Failures**: Investigate and fix failed merges
3. **Optimize Performance**: Adjust queue settings based on metrics

## 🔧 Step 6: Advanced Configuration

### Queue Settings:
```yaml
# In merge-queue-cicd.yml
merge_queue:
  # Maximum concurrent builds
  max_concurrent: 3
  # Timeout for stuck jobs (minutes)
  timeout: 60
  # Retry failed jobs
  retry_on_failure: true
  # Maximum retries
  max_retries: 2
```

### Custom Merge Conditions:
```yaml
# Additional requirements
merge_requirements:
  - coverage_minimum: 80
  - security_scan_passed: true
  - performance_tests_passed: true
  - documentation_updated: true
```

## 📈 Step 7: Performance Optimization

### Best Practices:
1. **Parallel Processing**: Enable concurrent job execution
2. **Smart Caching**: Use build caches to speed up builds
3. **Selective Testing**: Skip unnecessary tests for documentation changes
4. **Resource Optimization**: Adjust runner sizes based on workload

### Performance Monitoring:
```powershell
# Monitor queue performance
.\scripts\trunk-cicd-monitor.ps1 -AnalyzeHistory -PerformanceMetrics

# Check build times
gh run list --workflow="merge-queue-cicd.yml" --json createdAt,updatedAt,status
```

## 🐛 Troubleshooting

### Common Issues:

#### Queue Not Starting:
- ✅ Check branch protection rules
- ✅ Verify required status checks
- ✅ Ensure workflow has `merge_group` trigger

#### Builds Failing:
- ✅ Check runner availability
- ✅ Verify secrets are configured
- ✅ Review error logs in GitHub Actions

#### Slow Performance:
- ✅ Enable build caching
- ✅ Optimize test parallelization
- ✅ Review resource allocation

## 📞 Support

### Getting Help:
1. Check GitHub Actions logs
2. Review merge queue status
3. Monitor Trunk analytics
4. Consult this guide

### Emergency Procedures:
1. **Disable Queue**: Temporarily disable merge queue in settings
2. **Manual Merges**: Use standard merge process
3. **Re-enable**: Fix issues and re-enable queue

---

## 🎯 Success Metrics

**Target Performance:**
- ✅ **Queue Time**: < 30 minutes average
- ✅ **Success Rate**: > 95%
- ✅ **Build Time**: < 15 minutes
- ✅ **Coverage**: > 80%

**Monitor Regularly:**
- Daily queue performance
- Weekly success rate analysis
- Monthly optimization review

---

*Merge Queue Configuration - Enterprise CI/CD Integration*
