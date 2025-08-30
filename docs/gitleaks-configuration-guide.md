# Gitleaks Configuration Guide for Wiley Widget

## Overview

This guide covers the comprehensive setup and troubleshooting for Gitleaks secret scanning in the Wiley Widget project using Trunk CI/CD.

## 🎯 Current Configuration

### Trunk Configuration (`.trunk/trunk.yaml`)
```yaml
lint:
  enabled:
    - gitleaks@8.28.0:
        config: .trunk/configs/.gitleaks.toml
    # Alternative: trufflehog@3.90.5 (commented out)
```

### Gitleaks Configuration (`.trunk/configs/.gitleaks.toml`)
- Custom allowlist for false positives
- Extended default rules
- Project-specific exclusions
- Performance optimizations

## 🔧 Troubleshooting Gitleaks Installation

### Quick Diagnosis
```powershell
# Run comprehensive diagnostics
.\scripts\setup-gitleaks.ps1 -Diagnose
```

### Common Issues and Solutions

#### 1. **PATH Not Found Error**
**Symptoms:**
- `trunk check --filter=gitleaks` fails with "PATH not found"
- Gitleaks installation appears successful but doesn't work

**Solutions:**
```powershell
# 1. Check Go installation
go version

# 2. Check GOPATH
go env GOPATH

# 3. Reinstall gitleaks
go install github.com/gitleaks/gitleaks/v8@latest

# 4. Add to PATH manually
$env:PATH += ";$(go env GOPATH)\bin"
```

#### 2. **Go Runtime Issues**
**Symptoms:**
- Go commands fail
- Gitleaks installation fails

**Solutions:**
```powershell
# 1. Install/Update Go
# Download from: https://golang.org/dl/

# 2. Set GOPROXY for faster downloads
go env -w GOPROXY=https://proxy.golang.org,direct

# 3. Clear Go module cache
go clean -modcache
```

#### 3. **Permission Issues**
**Symptoms:**
- Installation fails with permission errors
- Cannot write to Go directories

**Solutions:**
```powershell
# Run PowerShell as Administrator
# Or set GOPATH to user directory
go env -w GOPATH="$env:USERPROFILE\go"
```

## 🚀 Alternative Security Scanning

### Using Trufflehog Instead
If Gitleaks continues to fail, switch to Trufflehog:

```powershell
# Configure alternative
.\scripts\setup-gitleaks.ps1 -Alternative
```

This will:
- Comment out gitleaks in `.trunk/trunk.yaml`
- Uncomment trufflehog configuration
- Maintain the same security scanning capabilities

### Manual Trunk Configuration
Edit `.trunk/trunk.yaml`:

```yaml
lint:
  enabled:
    # Comment out gitleaks
    # - gitleaks@8.28.0:
    #     config: .trunk/configs/.gitleaks.toml

    # Uncomment trufflehog
    - trufflehog@3.90.5
```

## 📋 Gitleaks Configuration Details

### Custom Rules Included

#### 1. **Generic API Key Detection**
```toml
[[rules]]
id = "generic-api-key"
description = "Generic API Key"
regex = "(?i)(api[_-]?key|apikey)([\\s\\S]{0,10})?[=:]([\\s\\S]{0,100})"
entropy = 3.5
```

#### 2. **Azure Connection Strings**
```toml
[[rules]]
id = "azure-connection-string"
description = "Azure Connection String"
regex = "(?i)(DefaultEndpointsProtocol|AccountName|AccountKey|BlobEndpoint|QueueEndpoint|TableEndpoint|FileEndpoint)"
entropy = 4.0
```

#### 3. **Database Connections**
```toml
[[rules]]
id = "database-connection"
description = "Database Connection String"
regex = "(?i)(server|host|database|username|password|connection.*string)"
entropy = 3.0
```

#### 4. **JWT Tokens**
```toml
[[rules]]
id = "jwt-token"
description = "JWT Token"
regex = "eyJ[A-Za-z0-9+/]+=*[A-Za-z0-9+/]*"
```

### Allowlist Configuration

#### Paths Excluded:
- `.trunk/` - Trunk configuration files
- `bin/`, `obj/` - Build artifacts
- `TestResults/` - Test outputs
- `*.binlog`, `*.exe`, `*.dll` - Binary files
- `*.tmp`, `*.log`, `*.bak` - Temporary files

#### Content Allowlist:
- "example", "test", "sample", "dummy"
- "placeholder", "your-*-here"
- Documentation files (`.md`, `.txt`)

## 🧪 Testing Configuration

### Test Gitleaks Functionality
```powershell
# Test with sample secrets
.\scripts\setup-gitleaks.ps1 -Test
```

### Manual Testing
```powershell
# Test on specific file
gitleaks detect --config .trunk/configs/.gitleaks.toml --path .

# Test with verbose output
gitleaks detect --verbose --config .trunk/configs/.gitleaks.toml --path .
```

### Integration Testing
```powershell
# Test Trunk integration
trunk check --filter=gitleaks --verbose

# Test specific file types
trunk check --filter=gitleaks --files="*.ps1,*.json,*.yaml"
```

## 📊 Performance Optimization

### Current Settings
```toml
[performance]
maxTargetMegaBytes = 100
regexTimeout = 30
```

### Tuning for Large Projects
```toml
# For very large repositories
[performance]
maxTargetMegaBytes = 500
regexTimeout = 60
concurrency = 4
```

## 🔍 Monitoring and Alerts

### GitHub Actions Integration
```yaml
- name: Security Scan
  uses: gitleaks/gitleaks-action@v2
  with:
    config-path: .trunk/configs/.gitleaks.toml
```

### Custom Reporting
```powershell
# Generate detailed report
gitleaks detect --config .trunk/configs/.gitleaks.toml --report-format json --report-path security-report.json
```

## 🛠️ Advanced Configuration

### Custom Rules for Project-Specific Patterns
```toml
[[rules]]
id = "wiley-widget-api-key"
description = "Wiley Widget API Key"
regex = "(?i)wiley[_-]?widget[_-]?key[=:]([A-Za-z0-9+/]{32})"
entropy = 4.5
tags = ["wiley", "api", "key"]
```

### Environment-Specific Configurations
```powershell
# Development config (more permissive)
Copy-Item .trunk/configs/.gitleaks.toml .trunk/configs/.gitleaks.dev.toml

# Production config (stricter)
Copy-Item .trunk/configs/.gitleaks.toml .trunk/configs/.gitleaks.prod.toml
```

## 📚 Reference Documentation

### Official Resources
- [Gitleaks GitHub](https://github.com/gitleaks/gitleaks)
- [Gitleaks Documentation](https://gitleaks.io/)
- [Trunk CI/CD Docs](https://docs.trunk.io/)
- [Go Installation Guide](https://golang.org/doc/install)

### PowerShell Resources
- [PSScriptAnalyzer](https://github.com/PowerShell/PSScriptAnalyzer)
- [PowerShell Security Best Practices](https://docs.microsoft.com/en-us/powershell/scripting/security/security-glossary)

## 🎯 Best Practices

### 1. **Regular Updates**
```powershell
# Update gitleaks regularly
go install github.com/gitleaks/gitleaks/v8@latest
```

### 2. **Configuration Backup**
```powershell
# Backup configurations before changes
Copy-Item .trunk/configs/.gitleaks.toml .trunk/configs/.gitleaks.toml.backup
```

### 3. **Testing Changes**
```powershell
# Test configuration changes
gitleaks detect --config .trunk/configs/.gitleaks.toml --dry-run
```

### 4. **Performance Monitoring**
```powershell
# Monitor scan performance
Measure-Command { trunk check --filter=gitleaks }
```

## 🚨 Emergency Procedures

### If Security Issues Are Found
1. **Immediate Action**: Rotate any detected secrets
2. **Investigation**: Review git history for exposure
3. **Prevention**: Update allowlist if false positive
4. **Documentation**: Document incident and resolution

### If Gitleaks Completely Fails
1. **Switch to Alternative**: Use Trufflehog
2. **Manual Scanning**: Implement manual security reviews
3. **Third-party Tools**: Consider GitGuardian or Snyk
4. **Process Updates**: Update development workflow

## 📞 Support and Resources

### Getting Help
1. **Gitleaks Issues**: [GitHub Issues](https://github.com/gitleaks/gitleaks/issues)
2. **Trunk Support**: [Trunk Documentation](https://docs.trunk.io/)
3. **PowerShell Community**: [PowerShell.org](https://powershell.org/)

### Community Resources
- [OWASP Secret Management](https://owasp.org/www-project-top-ten/)
- [GitHub Security Best Practices](https://docs.github.com/en/code-security)
- [Microsoft Security Guidelines](https://docs.microsoft.com/en-us/security/)

---

**Remember**: Security scanning is crucial for protecting sensitive data. If Gitleaks continues to have issues, the alternative Trufflehog provides similar functionality with potentially better Windows compatibility.
