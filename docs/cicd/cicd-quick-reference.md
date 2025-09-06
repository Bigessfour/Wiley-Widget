# WileyWidget CI/CD Quick Reference

## 🚀 Daily Development Commands

### Code Quality (Trunk)

```bash
# Check all files for issues
trunk check

# Fix auto-fixable issues
trunk fix

# Format code
trunk fmt

# Check specific files
trunk check --files="WileyWidget/**/*.cs"
```

### Building & Testing

```bash
# Standard build with tests
.\scripts\build.ps1

# Debug build
.\scripts\build.ps1 -Config Debug

# Build without tests
dotnet build WileyWidget.sln -c Release

# Run tests only
dotnet test WileyWidget.sln
```

### Azure Management

```bash
# Setup Azure environment
.\scripts\setup-azure.ps1

# Update firewall with current IP
.\scripts\update-firewall-ip.ps1 -ResourceGroup "your-rg" -SqlServer "your-server" -AutoDetectIP

# Test database connection
.\scripts\test-database-connection.ps1
```

## 📋 Workflow Checklist

### Before Committing

- [ ] Run `trunk check` - No linting errors
- [ ] Run `trunk fix` - Auto-fix issues
- [ ] Build locally: `.\scripts\build.ps1`
- [ ] Tests pass: All green
- [ ] No secrets committed (trunk will catch this)

### Before Pushing

- [ ] Branch is up to date with main
- [ ] Commit message follows convention
- [ ] Pre-push hooks pass (automatic)

### Pull Request Process

- [ ] Create feature branch from main
- [ ] Implement changes with tests
- [ ] Run full CI pipeline locally
- [ ] Create PR with description
- [ ] Address review feedback
- [ ] Merge when approved

## 🔧 Troubleshooting

### Build Issues

```bash
# Check build logs
Get-Content TestResults/msbuild.binlog

# Clean and rebuild
dotnet clean
dotnet restore
.\scripts\build.ps1
```

### Test Failures

```bash
# Run specific test
dotnet test --filter "TestName"

# Debug test output
dotnet test --logger "console;verbosity=detailed"
```

### Azure Connection Issues

```bash
# Check Azure login
az account show

# Verify firewall rules
az sql server firewall-rule list --resource-group "rg" --server "server"

# Test connection
.\scripts\test-database-connection.ps1
```

### Trunk Issues

```bash
# Update trunk
npm update -g @trunkio/launcher

# Reinitialize
trunk init --force

# Check tool versions
trunk tools
```

## 📊 Quality Metrics

### Required Standards

- **Test Coverage**: ≥70%
- **Build Success**: 100%
- **Security Scan**: Pass
- **Code Style**: Pass

### Checking Coverage

```bash
# After build, check coverage
Get-ChildItem -Recurse -Filter "coverage.cobertura.xml"

# View HTML report
start CoverageReport/index.html
```

## 🚀 Release Process

### Automated Release

1. Go to GitHub Actions → Release workflow
2. Click "Run workflow"
3. Enter version (e.g., "1.2.3")
4. Monitor the release creation

### Manual Release

```bash
# Update version
# Edit Directory.Build.targets with new version

# Build and package
.\scripts\build.ps1 -Publish -SelfContained

# Create release archive
Compress-Archive -Path publish/* -DestinationPath "WileyWidget-v1.2.3.zip"
```

## 📞 Support

### Getting Help

1. **Local Issues**: Check logs in `TestResults/`
2. **CI/CD Issues**: Check GitHub Actions logs
3. **Azure Issues**: Check Azure portal and CLI output
4. **Code Quality**: Run `trunk check --verbose`

### Common Solutions

- **Clean build**: `dotnet clean && dotnet restore`
- **Reset environment**: Delete `bin/`, `obj/`, `TestResults/`
- **Update tools**: `npm update -g @trunkio/launcher`
- **Azure login**: `az login`

## 🔗 Useful Links

- **Trunk Documentation**: https://docs.trunk.io
- **GitHub Actions**: https://docs.github.com/actions
- **Azure CLI**: https://docs.microsoft.com/cli/azure
- **.NET Documentation**: https://docs.microsoft.com/dotnet

---

_Keep this guide handy for daily development tasks!_
