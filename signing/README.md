# Signing Directory

This directory contains assembly signing keys for the Wiley Widget application.

## Security Notice

⚠️ **CRITICAL SECURITY INFORMATION**

- The `WileyWidget.snk` file contains the private key used for strong-name signing
- **NEVER** commit `.snk`, `.pfx`, or `.p12` files to version control
- Store production signing keys in secure key management systems (Azure Key Vault, AWS KMS, etc.)
- Rotate signing keys according to your organization's security policy

## Generating Keys

Use the `Generate-StrongNameKey.ps1` script to create a new strong name key:

```powershell
.\Generate-StrongNameKey.ps1 -OutputPath WileyWidget.snk
```

## Build Configuration

- **Debug builds**: Signing is disabled for faster iteration
- **Release builds**: Signing is enabled
- **Production builds**: Signing is REQUIRED

## Key Management Best Practices

1. Generate separate keys for development, staging, and production
2. Store production keys in a hardware security module (HSM) or secure vault
3. Use delay signing for development with public key only
4. Implement key rotation policies
5. Audit key access and usage
6. Never share private keys via email, chat, or insecure channels

## References

- [Strong Name Signing](https://docs.microsoft.com/en-us/dotnet/standard/assembly/strong-named)
- [Assembly Signing Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/library-guidance/strong-naming)
