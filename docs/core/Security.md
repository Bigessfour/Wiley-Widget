# Security Practices

- Secrets must be injected via environment variables or secure secret stores—never commit real keys. The sample configuration in `config/development/appsettings.json` uses `${PLACEHOLDER}` syntax.
- Run `trunk check --filter=gitleaks,trufflehog` before pushing changes that touch configuration.
- Follow the repository level [Security Policy](../../SECURITY.md) for reporting procedures and mandatory safeguards.

## Additional Resources

- [Security Compliance Assessment](../reference/SECURITY_COMPLIANCE_ASSESSMENT.md)
- [Secrets Guidance](../reference/Secrets.md)
- [KeyVault Fix Guide](../reference/KEYVAULT_FIX_GUIDE.md)
