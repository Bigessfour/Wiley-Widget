# Trunk Merge Queue Configuration Guide

## Overview
This guide will help you set up Trunk's Merge Queue for your Wiley Widget project. Merge queues help automate your PR merging process, ensuring quality and reducing manual work.

## Prerequisites
✅ **GitHub Repository**: Your project is already connected
✅ **CI/CD Workflows**: You have comprehensive CI/CD pipelines
✅ **Trunk Account**: You have access to app.trunk.io
✅ **Required Secrets**: TRUNK_ORG_URL_SLUG and TRUNK_API_TOKEN configured

## Step 1: Enable Merge Queue in GitHub

### GitHub Branch Protection Rules
1. Go to your repository Settings → Branches
2. Click "Add rule" for your main branch
3. Configure these settings:

**Required Status Checks:**
- [ ] Require branches to be up to date
- [ ] Require status checks to pass:
  - `test` (your test job)
  - `build` (your build job)
  - `security-scan` (Trunk security scan)
  - `lint` (code quality checks)

**Branch Protection:**
- [ ] Require a pull request before merging
- [ ] Require approvals: 1
- [ ] Dismiss stale pull request approvals
- [ ] Require review from Code Owners
- [ ] Restrict pushes that create matching branches

## Step 2: Configure Merge Queue in Trunk

### Basic Merge Queue Setup
1. Log in to [app.trunk.io](https://app.trunk.io)
2. Navigate to your Wiley Widget project
3. Go to **Settings → Merge Queue**
4. Configure these options:

**Queue Settings:**
- **Mode**: `Required` (PRs must go through queue)
- **Target Branch**: `main`
- **Required Checks**: Select all your CI jobs
- **Merge Method**: `Squash and merge`

**Test Requirements:**
- **Minimum Test Coverage**: 80%
- **Allow Flaky Tests**: No (wait for our flaky test detection)
- **Test Timeout**: 30 minutes

**Advanced Settings:**
- **Max Queue Size**: 10 PRs
- **Batch Size**: 1 PR at a time
- **Require Up-to-date Branches**: Yes

## Step 3: Update Your Workflows

Your existing workflows need minor updates for merge queue compatibility.

### Required Workflow Updates

1. **Add merge queue trigger** to your CI workflows
2. **Ensure proper status reporting**
3. **Add merge queue specific jobs**

Let me create an updated workflow for you:

```yaml
name: CI

on:
  push:
    branches:
      - main
  pull_request:
    branches:
      - main

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - checkout
      - run: npm install
      - run: npm test

  build:
    runs-on: ubuntu-latest
    steps:
      - checkout
      - run: npm install
      - run: npm run build

  'merge-queue':
    runs-on: ubuntu-latest
    needs: [test, build]
    steps:
      - checkout
      - run: echo "Merge queue job"
```

## Step 4: Test Your Merge Queue Setup

### Testing Checklist
- [ ] **Create a test PR** with a simple change
- [ ] **Verify CI passes** all required checks
- [ ] **Add PR to merge queue** in Trunk dashboard
- [ ] **Monitor queue progress** in real-time
- [ ] **Verify successful merge** and branch updates

### Common Issues & Solutions

**Issue: PR not eligible for merge queue**
- **Solution**: Ensure all required status checks are passing
- **Check**: Branch protection rules are correctly configured
- **Verify**: PR has required approvals

**Issue: Tests failing in queue**
- **Solution**: Check test output for specific failures
- **Check**: Test environment matches your local setup
- **Verify**: All dependencies are properly configured

**Issue: Merge conflicts**
- **Solution**: Trunk handles most conflicts automatically
- **Manual Fix**: Update PR branch with latest main
- **Prevention**: Keep PRs small and focused

## Step 5: Advanced Merge Queue Features

### Queue Management
- **Priority PRs**: Mark urgent PRs for faster processing
- **Queue Pausing**: Temporarily halt queue for maintenance
- **Batch Merging**: Group related PRs for efficiency

### Integration Features
- **Slack Notifications**: Get alerts for queue events
- **Dashboard Analytics**: Track queue performance metrics
- **Custom Rules**: Set up organization-specific requirements

### Monitoring & Analytics
- **Queue Health**: Overall system status
- **Throughput Metrics**: PRs merged per day/hour
- **Failure Analysis**: Common reasons for queue failures
- **Performance Trends**: Historical queue performance

## Step 6: Best Practices

### For Maintainers
1. **Keep main branch healthy** - fix issues quickly
2. **Review queue regularly** - address stuck PRs
3. **Monitor metrics** - track performance trends
4. **Communicate with team** - explain queue status

### For Contributors
1. **Write clear PR descriptions** - help reviewers
2. **Keep PRs focused** - smaller is better
3. **Test locally first** - catch issues early
4. **Monitor CI status** - fix failures promptly

### For Teams
1. **Establish SLAs** - set expectations for PR processing
2. **Define ownership** - who handles queue issues
3. **Document processes** - keep team informed
4. **Regular reviews** - improve queue configuration

## Resources

- [Trunk Merge Queue Documentation](https://docs.trunk.io/merge-queue)
- [GitHub Branch Protection](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/defining-the-mergeability-of-pull-requests/managing-a-branch-protection-rule)
- [Trunk Status Checks](https://docs.trunk.io/checks/status-checks)
- [Merge Queue Best Practices](https://docs.trunk.io/merge-queue/best-practices)

## Support

For help with merge queue setup:
1. Check [Trunk Documentation](https://docs.trunk.io)
2. Review [GitHub Actions Logs](https://github.com/your-org/wiley-widget/actions)
3. Contact Trunk support through your dashboard
4. Check this guide's troubleshooting section

---

**🎉 You're all set!** Your merge queue will help automate PR management, improve code quality, and speed up your development workflow.
