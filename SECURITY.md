# Security Policy

## 🔒 Secret Management in Wiley Widget

This document outlines how to properly handle secrets, API keys, and sensitive data in the Wiley Widget project.

## ✅ Current Security Status

**Good News**: Our security scan shows **NO HARDCODED SECRETS** in the repository! 🎉

- ✔️ **GitLeaks**: No secrets detected in git history
- ✔️ **TruffleHog**: No secrets found in files
- ✔️ **Configuration**: Using environment variable placeholders correctly

## 🛡️ Security Best Practices

### 1. **Environment Variables (REQUIRED)**

**✅ DO**: Use environment variable placeholders in configuration files:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "${DATABASE_CONNECTION_STRING}"
  },
  "QuickBooks": {
    "ClientSecret": "${QBO_CLIENT_SECRET}"
  },
  "Syncfusion": {
    "LicenseKey": "${SYNCFUSION_LICENSE_KEY}"
  }
}
```

**❌ DON'T**: Hardcode actual secrets:

```json
{
  "QuickBooks": {
    "ClientSecret": "actual_secret_value_here" // ❌ NEVER DO THIS
  }
}
```

## 🚨 What to Do If Secrets Are Exposed

If you accidentally commit secrets:

### 1. **Immediate Response**

```bash
# Stop! Don't push if you haven't already
git status

# If not pushed yet, reset the commit
git reset --soft HEAD~1

# Remove the secrets from files
# Replace with environment variables

# Commit again
git add .
git commit -m "fix: use environment variables for secrets"
```

### 2. **If Already Pushed**

```bash
# Revoke the exposed secrets immediately:
# - Change API keys
# - Rotate passwords
# - Update license keys

# Clean git history (nuclear option)
git filter-repo --path-based-filter 'path_to_secret_file'
git push --force
```

## 🔧 Tools and Commands

### **Security Scanning**

```bash
# Run security scan manually
trunk check --filter=gitleaks,trufflehog --all

# Continuous monitoring
trunk check --monitor
```

## 📞 Reporting Security Vulnerabilities

If you discover a security vulnerability in WileyWidget, please help us by reporting it responsibly.

### How to Report

- **DO NOT** create public GitHub issues for security vulnerabilities
- Email security concerns to: [your-email@example.com]
- Include detailed information about the vulnerability
- Allow reasonable time for us to respond and fix the issue before public disclosure

### What We Need

When reporting a security vulnerability, please include:

- A clear description of the vulnerability
- Steps to reproduce the issue
- Potential impact and severity
- Any suggested fixes or mitigations

### Our Commitment

- We will acknowledge receipt of your report within 48 hours
- We will provide regular updates on our progress
- We will credit you (if desired) once the issue is resolved
- We follow responsible disclosure practices

## Supported Versions

Currently supported versions with security updates:

- Latest release only
- Critical security fixes may be backported to recent major versions

## Security Best Practices

- Keep your dependencies updated
- Use strong, unique passwords
- Enable two-factor authentication
- Regularly review and rotate access tokens
- Follow the principle of least privilege
