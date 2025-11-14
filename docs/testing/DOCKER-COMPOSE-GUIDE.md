# Docker Compose Testing Guide

## Quick Start

### 1. Start Database + App
```bash
docker-compose up -d db app
```

### 2. Run Unit + Integration Tests
```bash
docker-compose run --rm test
```

### 3. Run UI Smoke Tests
```bash
docker-compose run --rm ui-test
```

### 4. View Logs
```bash
# Database logs
docker-compose logs -f db

# App logs
docker-compose logs -f app

# Test output
docker-compose logs test
```

### 5. Stop All Services
```bash
docker-compose down
```

### 6. Clean Up (volumes + images)
```bash
docker-compose down -v
docker system prune -f
```

---

## Daily Development Workflow

### Morning Startup
```bash
# Start DB (wait for healthy)
docker-compose up -d db

# Verify DB is ready
docker-compose ps db
# Should show "healthy" in status

# Run tests
docker-compose run --rm test
```

### Code → Test Loop
```bash
# 1. Make code changes in VS Code

# 2. Run specific test category
docker-compose run --rm test --filter Category=Unit

# 3. Check coverage
cat coverage/coverage.cobertura.xml

# 4. If tests pass, commit
git add .
git commit -m "test: add QuickBooksService error handling"
git push
```

### End of Day Cleanup
```bash
# Stop services but keep volumes
docker-compose stop

# Or tear down completely
docker-compose down
```

---

## Test Categories

### Unit Tests (Fast, No DB)
```bash
docker-compose run --rm test --filter Category=Unit
```

### Integration Tests (With DB)
```bash
docker-compose run --rm test --filter Category=Integration
```

### UI Tests (Playwright)
```bash
docker-compose run --rm ui-test
```

### Run Specific Test Class
```bash
docker-compose run --rm test --filter FullyQualifiedName~QuickBooksServiceTests
```

### Run with Verbose Output
```bash
docker-compose run --rm test -- --logger "console;verbosity=detailed"
```

---

## Code Coverage

### Generate Coverage Report
```bash
# Run tests with coverage
docker-compose run --rm test

# View coverage file
cat coverage/coverage.cobertura.xml

# Generate HTML report (requires reportgenerator)
docker run --rm -v "$(pwd):/src" \
  danielpalme/reportgenerator:latest \
  -reports:/src/coverage/coverage.cobertura.xml \
  -targetdir:/src/coverage/html \
  -reporttypes:Html

# Open report
start coverage/html/index.html  # Windows
open coverage/html/index.html   # macOS
```

---

## CI/CD Integration

### GitHub Actions (.github/workflows/ci-optimized.yml)

Add this job:

```yaml
test-docker:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    
    - name: Start Database
      run: docker-compose up -d db
    
    - name: Wait for DB Healthy
      run: |
        timeout 60 sh -c 'until docker-compose ps db | grep healthy; do sleep 2; done'
    
    - name: Run Tests
      run: docker-compose run --rm test
    
    - name: Upload Coverage
      uses: codecov/codecov-action@v4
      with:
        files: ./coverage/coverage.cobertura.xml
    
    - name: Cleanup
      if: always()
      run: docker-compose down -v
```

---

## Troubleshooting

### Database Won't Start
```bash
# Check logs
docker-compose logs db

# Common fix: Remove volume and restart
docker-compose down -v
docker-compose up -d db
```

### Tests Fail with Connection Error
```bash
# Ensure DB is healthy first
docker-compose ps db

# Restart DB
docker-compose restart db

# Wait for healthy status
docker-compose ps db
```

### App Container Exits Immediately
```bash
# Check logs for errors
docker-compose logs app

# Rebuild image
docker-compose build app
docker-compose up -d app
```

### Coverage Files Missing
```bash
# Ensure volume is mounted correctly
docker-compose run --rm test

# Check coverage directory exists
ls -la coverage/

# Verify permissions
chmod -R 777 coverage/
```

---

## Advanced Usage

### Interactive Test Shell
```bash
# Start test container with bash
docker-compose run --rm --entrypoint /bin/bash test

# Inside container:
dotnet test --filter Category=Unit
dotnet test --list-tests
```

### Debug Mode
```bash
# Run app with debug port exposed
docker-compose run --rm -p 5001:5001 app
```

### Custom Connection String
```bash
# Override environment variable
docker-compose run --rm \
  -e ConnectionStrings__DefaultConnection="Server=localhost;Database=Test;..." \
  test
```

---

## Performance Tips

### Speed Up Builds
```bash
# Use BuildKit
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1

# Rebuild with cache
docker-compose build --parallel
```

### Prune Regularly
```bash
# Remove unused images
docker image prune -f

# Remove unused volumes
docker volume prune -f

# Nuclear option (careful!)
docker system prune -af --volumes
```

---

## MCP Compliance

All Docker Compose commands can be wrapped in MCP helpers:

```powershell
# Via Invoke-McpEdit.ps1
.\scripts\tools\Invoke-McpEdit.ps1 `
  -Path "docker-compose.yml" `
  -OldText "1433:1433" `
  -NewText "14333:1433" `
  -UseSequentialThinking

# Via run-script.ps1
.\scripts\tools\run-script.ps1 `
  -ScriptPath "scripts/docker-test-runner.ps1" `
  -Verbose
```

---

## Next Steps

1. **Day 1:** Run `docker-compose run --rm test` → ensure 0 tests pass (scaffold exists)
2. **Day 2:** Add `QuickBooksServiceTests.cs` → 3+ tests pass
3. **Day 3:** Add integration tests → DB tests pass
4. **Day 4:** Configure Playwright → UI tests pass
5. **Day 5:** Add to CI pipeline → GitHub Actions green
6. **Day 6:** Achieve 80%+ coverage → production ready

---

**Pro Tip:** Add this alias to your PowerShell profile:

```powershell
function dt { docker-compose run --rm test $args }
function dtu { docker-compose run --rm test --filter Category=Unit }
function dti { docker-compose run --rm test --filter Category=Integration }
```

Then just run:
```bash
dt                    # All tests
dtu                   # Unit tests only
dti                   # Integration tests only
```
