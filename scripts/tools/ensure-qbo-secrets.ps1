<#
.SYNOPSIS
Populate QuickBooks secrets into the local secret vault and optionally set user-level environment vars.

.DESCRIPTION
This helper writes QBO secrets to %APPDATA%\WileyWidget\Secrets\secrets.json (merging with any existing secrets)
and can also persist them as user environment variables so the running process can pick them up after restart.

.PARAMETER ClientId
QuickBooks client id. Default: test-client-id

.PARAMETER ClientSecret
QuickBooks client secret. Default: test-client-secret

.PARAMETER RealmId
Optional QuickBooks realm/company id.

.PARAMETER SetEnv
If provided, also set user-level environment variables QBO_CLIENT_ID and QBO_CLIENT_SECRET.

.EXAMPLE
    pwsh -NoProfile -ExecutionPolicy Bypass -File .\ensure-qbo-secrets.ps1 -ClientId 'abc' -ClientSecret 'xyz' -SetEnv
#>

param(
    [string]$ClientId = "test-client-id",
    [string]$ClientSecret = "test-client-secret",
    [string]$RealmId = "",
    [switch]$SetEnv
)

function Write-Log {
    param($msg)
    Write-Host "[ensure-qbo-secrets] $msg"
}

try {
    $appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
    $baseDir = Join-Path $appData "WileyWidget\Secrets"
    if (-not (Test-Path $baseDir)) { New-Item -ItemType Directory -Path $baseDir -Force | Out-Null }

    $secretsPath = Join-Path $baseDir "secrets.json"

    # Helper to convert PS object to hashtable
    function ConvertTo-Hashtable($obj) {
        if ($null -eq $obj) { return @{} }
        if ($obj -is [hashtable]) { return $obj }
        $h = @{}
        $obj.PSObject.Properties | ForEach-Object { $h[$_.Name] = $_.Value }
        return $h
    }

    # Load existing secrets if present
    $secrets = @{}
    if (Test-Path $secretsPath) {
        try {
            $json = Get-Content -Path $secretsPath -Raw -ErrorAction Stop
            if (-not [string]::IsNullOrWhiteSpace($json)) {
                $parsed = $json | ConvertFrom-Json -ErrorAction Stop
                $secrets = ConvertTo-Hashtable $parsed
            }
        } catch {
            Write-Log "Warning: failed to parse existing secrets.json. Proceeding with a fresh file. Error: $_"
            $secrets = @{}
        }
    }

    # Merge keys used by the app (both QBO-CLIENT-ID and QuickBooks-ClientId are accepted)
    $secrets["QBO-CLIENT-ID"] = $ClientId
    $secrets["QuickBooks-ClientId"] = $ClientId
    $secrets["QBO-CLIENT-SECRET"] = $ClientSecret
    $secrets["QuickBooks-ClientSecret"] = $ClientSecret
    if (-not [string]::IsNullOrWhiteSpace($RealmId)) {
        $secrets["QBO-REALM-ID"] = $RealmId
        $secrets["QuickBooks-RealmId"] = $RealmId
    }

    # Persist to file (pretty-printed)
    $secrets | ConvertTo-Json -Depth 10 | Out-File -FilePath $secretsPath -Encoding utf8 -Force

    Write-Log "Wrote/updated secrets at: $secretsPath"

    if ($SetEnv.IsPresent) {
        [Environment]::SetEnvironmentVariable("QBO_CLIENT_ID", $ClientId, "User")
        [Environment]::SetEnvironmentVariable("QBO_CLIENT_SECRET", $ClientSecret, "User")
        if (-not [string]::IsNullOrWhiteSpace($RealmId)) {
            [Environment]::SetEnvironmentVariable("QBO_REALM_ID", $RealmId, "User")
        }
        Write-Log "Set user environment variables QBO_CLIENT_ID and QBO_CLIENT_SECRET (effective for new processes)."
    }

    Write-Log "Done. Restart the application (or your terminal/VS Code) to pick up user-level env vars if you set them."
    exit 0
} catch {
    Write-Error "Failed to ensure QBO secrets: $_"
    exit 2
}
