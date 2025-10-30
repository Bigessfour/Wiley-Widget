# CI/CD Integration Example for C# MCP Evaluation

## GitHub Actions Workflow

Add this to `.github/workflows/csharp-mcp-tests.yml`:

```yaml
name: C# MCP Quick Tests

on:
  push:
    paths:
      - 'scripts/examples/csharp/**'
      - 'WileyWidget.UI/**'
      - 'WileyWidget.ViewModels/**'
  pull_request:
    paths:
      - 'scripts/examples/csharp/**'

jobs:
  csharp-eval-tests:
    name: C# Evaluation Tests
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Python
        uses: actions/setup-python@v5
        with:
          python-version: '3.11'

      - name: Run C# Evaluation Tests
        run: |
          python scripts/csharp-eval.py -d scripts/examples/csharp/ --json > results.json

      - name: Display Results
        if: always()
        run: |
          if [ -f results.json ]; then
            cat results.json
          fi

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: csharp-eval-results
          path: results.json
```

## Local Pre-Commit Hook

Create `.git/hooks/pre-commit`:

```bash
#!/bin/bash
# Pre-commit hook for C# evaluation tests

echo "Running C# evaluation tests..."

python scripts/csharp-eval.py -d scripts/examples/csharp/ --json

if [ $? -ne 0 ]; then
    echo "❌ C# evaluation tests failed!"
    exit 1
fi

echo "✅ C# evaluation tests passed!"
exit 0
```

Make it executable:
```bash
chmod +x .git/hooks/pre-commit
```

## PowerShell Pre-Commit (Windows)

Create `scripts/pre-commit-csharp.ps1`:

```powershell
#!/usr/bin/env pwsh

Write-Host "🧪 Running C# evaluation tests..." -ForegroundColor Cyan

$result = python scripts/csharp-eval.py -d scripts/examples/csharp/ --json
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    Write-Host "❌ C# evaluation tests failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ C# evaluation tests passed!" -ForegroundColor Green
exit 0
```

## Trunk Integration

Add to `.trunk/trunk.yaml`:

```yaml
actions:
  enabled:
    - csharp-eval-tests

actions:
  definitions:
    - id: csharp-eval-tests
      display_name: C# Evaluation Tests
      description: Run C# MCP evaluation tests
      runtime: python
      packages_file: requirements.txt
      run: python scripts/csharp-eval.py -d scripts/examples/csharp/
      triggers:
        - files: [scripts/examples/csharp/**/*.csx]
```

## VS Code Task for CI Simulation

Already added to `.vscode/tasks.json`, but here's standalone:

```json
{
  "label": "csharp-ci-tests",
  "type": "shell",
  "command": "${workspaceFolder}/.venv/Scripts/python.exe",
  "args": [
    "scripts/csharp-eval.py",
    "-d",
    "scripts/examples/csharp/",
    "--json"
  ],
  "group": "test",
  "presentation": {
    "reveal": "always",
    "panel": "dedicated"
  }
}
```

## Docker Integration

Create `docker/Dockerfile.csharp-eval`:

```dockerfile
FROM python:3.11-slim

WORKDIR /workspace

# Install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy evaluation script
COPY scripts/csharp-eval.py scripts/

# Copy test scripts
COPY scripts/examples/csharp/ scripts/examples/csharp/

# Run tests
CMD ["python", "scripts/csharp-eval.py", "-d", "scripts/examples/csharp/", "--json"]
```

Build and run:
```bash
docker build -f docker/Dockerfile.csharp-eval -t csharp-eval:latest .
docker run csharp-eval:latest
```

## Azure DevOps Pipeline

Create `azure-pipelines-csharp-eval.yml`:

```yaml
trigger:
  paths:
    include:
      - scripts/examples/csharp/**

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UsePythonVersion@0
    inputs:
      versionSpec: '3.11'
    displayName: 'Setup Python'

  - script: |
      python scripts/csharp-eval.py -d scripts/examples/csharp/ --json
    displayName: 'Run C# Evaluation Tests'

  - task: PublishTestResults@2
    inputs:
      testResultsFormat: 'JUnit'
      testResultsFiles: 'results.json'
    displayName: 'Publish Test Results'
```

## Integration with dev-start.py

Add to `scripts/dev-start.py`:

```python
def run_csharp_eval_tests():
    """Run C# evaluation tests on startup."""
    import subprocess

    print("🧪 Running C# evaluation tests...")

    result = subprocess.run(
        ["python", "scripts/csharp-eval.py", "-d", "scripts/examples/csharp/"],
        capture_output=True,
        text=True
    )

    if result.returncode == 0:
        print("✅ C# evaluation tests passed!")
    else:
        print("⚠️  C# evaluation tests failed (non-blocking)")
        print(result.stdout)

# Call in main()
if __name__ == "__main__":
    # ... existing code ...
    run_csharp_eval_tests()
```

## Monitoring & Alerts

### Slack Notification (GitHub Actions)

```yaml
- name: Notify Slack on Failure
  if: failure()
  uses: slackapi/slack-github-action@v1
  with:
    payload: |
      {
        "text": "❌ C# Evaluation Tests Failed",
        "blocks": [
          {
            "type": "section",
            "text": {
              "type": "mrkdwn",
              "text": "*C# Evaluation Tests Failed*\nRepo: ${{ github.repository }}\nBranch: ${{ github.ref }}"
            }
          }
        ]
      }
  env:
    SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK }}
```

## Summary

All CI/CD integration options available:
- ✅ GitHub Actions
- ✅ Git Hooks (pre-commit)
- ✅ Trunk CLI
- ✅ VS Code Tasks
- ✅ Docker
- ✅ Azure DevOps
- ✅ Slack Notifications

Choose the method that fits your workflow!
