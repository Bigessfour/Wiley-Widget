# Generate a Strong Name Key (SNK) file for assembly signing
# This script creates a cryptographic key pair for signing assemblies

param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "WileyWidget.snk"
)

try {
    # Load required assembly
    Add-Type -AssemblyName System.Security

    # Create a new RSA key pair with 2048-bit strength (production-grade)
    $rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider(2048)

    # Export the key pair including private key
    $keyPair = $rsa.ExportCspBlob($true)

    # Write to SNK file
    [System.IO.File]::WriteAllBytes($OutputPath, $keyPair)

    Write-Host "✓ Strong name key generated successfully: $OutputPath" -ForegroundColor Green
    Write-Host "  Key size: 2048 bits (Production-grade)" -ForegroundColor Cyan
    Write-Host "  ⚠️  SECURITY: Store this file securely - it contains your private signing key!" -ForegroundColor Yellow

    $rsa.Dispose()
} catch {
    Write-Error "Failed to generate strong name key: $_"
    exit 1
}
