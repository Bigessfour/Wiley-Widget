<#!
.SYNOPSIS
  Stores (or rotates) the Syncfusion license key in Azure Key Vault securely.
.DESCRIPTION
  Reads the license key from stdin (preferred), a file, or a parameter and sets
  the Key Vault secret (default name: SYNCFUSION-LICENSE-KEY). Does NOT echo
  the key. Supports dry-run verification and base64 input decoding.
.PARAMETER Vault
  Azure Key Vault name. If omitted, uses $env:KEYVAULT_NAME.
.PARAMETER SecretName
  Secret name (default SYNCFUSION-LICENSE-KEY).
.PARAMETER Key
  License key literal (NOT recommended; use -File or pipeline instead).
.PARAMETER File
  Path to file containing the license key (first line used if multiple).
.PARAMETER FromBase64
  Treat supplied key (or file contents) as Base64 and decode before storing.
.PARAMETER DryRun
  Show what would happen without writing secret.
.EXAMPLE
  'abc123...' | pwsh ./scripts/set-syncfusion-license-az.ps1 -Vault myvault
.EXAMPLE
  pwsh ./scripts/set-syncfusion-license-az.ps1 -Vault myvault -File ./license.key
#>
[CmdletBinding()] param(
  [string]$Vault,
  [string]$SecretName = 'SYNCFUSION-LICENSE-KEY',
  [string]$Key,
  [string]$File,
  [switch]$FromBase64,
  [switch]$DryRun,
  [switch]$Quiet
)

$ErrorActionPreference='Stop'
function Out-Info($m){ if(-not $Quiet){ Write-Host $m -ForegroundColor Cyan }}
function Out-Ok($m){ if(-not $Quiet){ Write-Host $m -ForegroundColor Green }}
function Out-Warn($m){ if(-not $Quiet){ Write-Warning $m }}
function Out-Err($m){ Write-Host $m -ForegroundColor Red }

if(-not $Vault){ $Vault = $env:KEYVAULT_NAME }
if(-not $Vault){ Out-Err 'Key Vault name not provided (use -Vault or set KEYVAULT_NAME).'; exit 2 }

if($Key){ if($File){ Out-Err 'Specify either -Key or -File, not both.'; exit 2 } }

# Read from pipeline if present
if(-not $Key -and -not $File){
  if($MyInvocation.ExpectingInput){
    $buf = @(); while($input.MoveNext()){ $buf += $input.Current }
    $Key = ($buf -join "`n").Trim()
  }
}

if(-not $Key -and $File){
  if(-not (Test-Path $File)){ Out-Err "File not found: $File"; exit 3 }
  $Key = (Get-Content $File -Raw).Split("`n")[0].Trim()
}

if(-not $Key){
  # Interactive secure prompt
  $sec = Read-Host 'Enter Syncfusion License Key' -AsSecureString
  $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
  try { $Key = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
  $Key = $Key.Trim()
}

if([string]::IsNullOrWhiteSpace($Key)){ Out-Err 'No key provided.'; exit 4 }

if($FromBase64){
  try {
    $decoded = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Key))
    if(-not [string]::IsNullOrWhiteSpace($decoded)){ $Key = $decoded.Trim() } else { Out-Warn 'Decoded Base64 is empty.' }
  } catch { Out-Err 'Failed to decode Base64 input.'; exit 5 }
}

$len = $Key.Length
if($Key -match 'YOUR_SYNCFUSION_LICENSE_KEY_HERE' -or $len -lt 50){
  Out-Warn "Key length ($len) suggests placeholder or invalid key.";
}

Out-Info "Target Vault: $Vault"
Out-Info "Secret Name : $SecretName"
Out-Info "Key Length  : $len"
if($DryRun){ Out-Ok 'DryRun specified - not writing secret.'; exit 0 }

try {
  az keyvault secret set --vault-name $Vault --name $SecretName --value $Key --only-show-errors | Out-Null
  Out-Ok 'Syncfusion license key stored/rotated successfully.'
} catch {
  Out-Err "Failed to set secret: $($_.Exception.Message)"; exit 6
}

exit 0