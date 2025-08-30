# Trunk Flaky Tests Integration

This document explains how to set up and use Trunk's Flaky Tests detection in the Wiley Widget project.

## Overview

Trunk Flaky Tests automatically detects and tracks flaky tests in your CI/CD pipeline by analyzing test results over time. This helps prevent unreliable tests from affecting your development workflow.

## Current Setup

✅ **Modern Analytics Uploader**: Both `comprehensive-cicd.yml` and `release-new.yml` workflows use the recommended `trunk-io/analytics-uploader@v1` action
✅ **Test Report Generation**: Tests generate JUnit XML format for Trunk analysis
✅ **Automated Upload**: Test results are uploaded after every CI run, even when tests fail
✅ **Error Handling**: Upload step uses `continue-on-error: true` to prevent CI failures

## Setup Instructions

### 1. Get Your Trunk Organization Credentials

1. Go to [Trunk Settings > Manage > Organization](https://app.trunk.io/settings/manage/organization)
2. Copy your **Organization Slug** (not the organization name)
3. Generate or copy your **API Token**

### 2. Configure GitHub Secrets

Add these secrets to your GitHub repository:

- `TRUNK_ORG_URL_SLUG`: Your organization slug from step 1
- `TRUNK_API_TOKEN`: Your API token from step 1

**Note:** The secret name should be `TRUNK_ORG_URL_SLUG` (with "URL" in the name) as shown in the Trunk documentation.

**To add secrets:**
1. Go to your repository on GitHub
2. Navigate to Settings > Secrets and variables > Actions
3. Click "New repository secret"
4. Add both secrets listed above

### 3. Validate Setup Locally

Use the setup script to validate your configuration:

```powershell
# Validate test reports locally
.\scripts\setup-flaky-tests.ps1 -Validate

# Test upload with your credentials
.\scripts\setup-flaky-tests.ps1 -TestUpload -OrgSlug "your-org-slug" -ApiToken "your-token"
```

## How It Works

### CI/CD Integration

The workflows now perform these steps:

1. **Run Tests**: Execute tests with JUnit XML output for Trunk analysis
2. **Upload Results**: Send test results to Trunk using the modern analytics uploader action
3. **Continue Processing**: Coverage reports and other steps continue as normal

### Benefits of the Modern Approach

The `trunk-io/analytics-uploader@v1` action provides several advantages:

- **Simplified Configuration**: No need to manually download and configure the Trunk CLI
- **Better Error Handling**: Built-in retry logic and error reporting
- **Consistent Updates**: Automatic updates when new versions are released
- **Official Support**: Maintained by Trunk.io with official documentation

### Test Result Processing

- **Multiple Uploads Required**: Trunk needs several test runs to accurately detect flaky tests
- **Processing Time**: Results appear in your Trunk dashboard within 1-2 hours
- **Analysis Period**: Trunk analyzes patterns over time to identify truly flaky tests

## Monitoring and Management

### View Results

1. Go to [Trunk Flaky Tests Dashboard](https://app.trunk.io/flaky-tests)
2. View detected flaky tests and their failure patterns
3. Monitor test reliability trends over time

### Quarantining Flaky Tests

Once Trunk identifies flaky tests, you can:

1. **Quarantine Tests**: Prevent flaky tests from failing builds
2. **Set Up Alerts**: Get notified when new flaky tests are detected
3. **Track Improvements**: Monitor how test reliability changes over time

## Troubleshooting

### Common Issues

**Upload Fails:**
- Verify `TRUNK_ORG_SLUG` and `TRUNK_API_TOKEN` secrets are set correctly
- Check that test results are generated (JUnit XML files exist)

**No Tests Detected:**
- Ensure tests are running and generating JUnit XML output
- Verify the XML format is correct (use validation script)

**Results Not Appearing:**
- Wait at least 1 hour for processing
- Multiple test runs are needed for accurate detection

### Validation Commands

```powershell
# Check if Trunk CLI is working
curl -fsSLO --retry 3 https://trunk.io/releases/trunk
chmod +x trunk
./trunk flakytests -V

# Validate test reports
./trunk flakytests validate --junit-paths "TestResults/*/test-results.xml"
```

## Advanced Configuration

### Custom Test Commands

If you need to customize the test command in CI:

```yaml
- name: Run Tests with Trunk Upload
  run: |
    # Your custom test command
    dotnet test --logger "junit;LogFileName=test-results.xml" [your-options]

- name: Upload Test Results to Trunk Flaky Tests
  if: always()  # Run even if tests fail
  continue-on-error: true  # Don't fail the job if upload fails
  uses: trunk-io/analytics-uploader@v1
  with:
    junit-paths: "TestResults/*/test-results.xml"
    org-slug: ${{ secrets.TRUNK_ORG_URL_SLUG }}
    token: ${{ secrets.TRUNK_API_TOKEN }}
```

### Legacy CLI Method (Deprecated)

The older manual CLI approach is still supported but deprecated:

```yaml
- name: Upload Test Results to Trunk Flaky Tests (Legacy)
  if: always()
  run: |
    curl -fsSLO --retry 3 https://trunk.io/releases/trunk
    chmod +x trunk
    ./trunk flakytests upload --junit-paths "TestResults/*/test-results.xml" --org-url-slug ${{ secrets.TRUNK_ORG_URL_SLUG }} --token ${{ secrets.TRUNK_API_TOKEN }}
```

### Environment Variables

You can also set these as environment variables instead of secrets:

```yaml
env:
  TRUNK_ORG_SLUG: ${{ secrets.TRUNK_ORG_SLUG }}
  TRUNK_API_TOKEN: ${{ secrets.TRUNK_API_TOKEN }}
```

## Resources

- [Trunk Flaky Tests Documentation](https://docs.trunk.io/flaky-tests)
- [Uploader CLI Reference](https://docs.trunk.io/flaky-tests/uploader)
- [Getting Started Guide](https://docs.trunk.io/flaky-tests/get-started)
- [Trunk Dashboard](https://app.trunk.io)

## Support

For issues with Trunk Flaky Tests:
- Check the [Trunk Knowledge Base](https://docs.trunk.io/)
- Contact Trunk support through their dashboard
- Review CI/CD logs for upload errors
