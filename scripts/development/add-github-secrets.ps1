<#
Interactive helper for adding repository-level GitHub Actions secrets using `gh` CLI.

Usage (interactive):
  pwsh ./scripts/setup/add-github-secrets.ps1

This script will prompt for secret values and call `gh secret set` for each.
It does NOT persist secrets in repo files and avoids echoing values to logs.
#>

param(
    [string]$Repo = "Bigessfour/Wiley-Widget",
    [switch]$SkipConfirm
)

function Prompt-Secret {
    param([string]$Name, [string]$prompt)
    while ($true) {
        $value = Read-Host -AsSecureString "$prompt (press Enter to skip)"
        if (-not $value) { return $null }
        # Convert SecureString to plaintext briefly for CLI input
        try {
            $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($value))
            return $plain
        } finally {
            # no-op - secure string remains in memory policy-limited
        }
    }
}

$secrets = @(
    @{ Name = 'QBO_CLIENT_ID'; Prompt = 'QuickBooks Client ID' },
    @{ Name = 'QBO_CLIENT_SECRET'; Prompt = 'QuickBooks Client Secret' },
    @{ Name = 'QBO_REALM_ID'; Prompt = 'QuickBooks Realm ID (company ID)' },
    @{ Name = 'SYNCFUSION_LICENSE_KEY'; Prompt = 'Syncfusion License Key' },
    @{ Name = 'XAI_API_KEY'; Prompt = 'xAI / Grok API Key' },
    @{ Name = 'MSSQL_TEST_CONNECTION'; Prompt = 'Integration test MSSQL connection string' },
    @{ Name = 'WW_CONNECTION_STRING'; Prompt = 'WW connection string (if used)' }
)

Write-Host "This helper uses the GitHub CLI (gh). Ensure you are authenticated (gh auth login)." -ForegroundColor Cyan

foreach ($secret in $secrets) {
    $name = $secret.Name
    $prompt = $secret.Prompt

    $value = Prompt-Secret -Name $name -prompt $prompt
    if (-not $value) {
        Write-Host "Skipping $name (no value provided)" -ForegroundColor Yellow
        continue
    }

    if (-not $SkipConfirm) {
        $ok = Read-Host "Set secret '$name' in repo '$Repo'? Type 'yes' to confirm"
        if ($ok -ne 'yes') { Write-Host "Not setting $name (user cancelled)" -ForegroundColor Yellow; continue }
    }

    Write-Host "Setting secret '$name'..." -ForegroundColor Green
    $encoded = [System.Text.Encoding]::UTF8.GetBytes($value)
    # Use gh secret set via STDIN to avoid exposing values to shell
    $process = Start-Process -FilePath gh -ArgumentList "secret set $name --repo $Repo --body -" -NoNewWindow -RedirectStandardInput "pipe" -PassThru -Wait
    $process.StandardInput.WriteLine($value)
    $process.StandardInput.Close()

    if ($process.ExitCode -eq 0) {
        Write-Host "Success: $name" -ForegroundColor Green
    } else {
        Write-Host "Failed to set $name (exit $($process.ExitCode)). See GH CLI output for details." -ForegroundColor Red
    }
}

Write-Host "Done. Verify secrets in: https://github.com/$(($Repo))/settings/secrets/actions" -ForegroundColor Cyan
