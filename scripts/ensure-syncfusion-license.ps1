<#!
.SYNOPSIS
  Ensures SYNCFUSION_LICENSE_KEY is present in local .env (process development convenience).
.DESCRIPTION
  Priority order (unless -Force or -PreferMachine):
    1. Existing .env non-placeholder value
    2. Current process / user / machine environment variable
    3. Azure Key Vault (if -KeyVaultName provided or KEYVAULT_NAME env var set) secret name (default SYNCFUSION-LICENSE-KEY)
       (Legacy compatible fallback names tried automatically: SYNCFUSION-LICENSE, SYNCFUSION_LICENSE_KEY)
    4. license.key file in project root
    5. Interactive prompt (masked) if in TTY

  Writes / updates .env and creates sentinel .syncfusion.license.synced for audit.
.PARAMETER KeyVaultName
  Azure Key Vault name (or rely on KEYVAULT_NAME env var).
.PARAMETER SecretName
  Secret name in Key Vault (default SYNCFUSION-LICENSE-KEY) storing the raw license key.
  Legacy names still probed automatically: SYNCFUSION-LICENSE, SYNCFUSION_LICENSE_KEY
.PARAMETER Force
  Force re-acquisition even if existing key found.
.PARAMETER PreferMachine
  Prioritize machine-level environment variable (authoritative) over .env/user when present.
.PARAMETER SyncMachineToEnv
  If machine variable is authoritative and .env missing/placeholder, sync it into .env without needing -PersistToEnvFile.
.PARAMETER Quiet
  Suppress non-error output.
.EXAMPLE
  pwsh ./scripts/ensure-syncfusion-license.ps1 -KeyVaultName myVault
#>
[CmdletBinding()]
param(
  [string]$KeyVaultName,
  # Default secret name aligned with README documentation table
  [string]$SecretName = 'SYNCFUSION-LICENSE-KEY',
  [switch]$Force,
  [switch]$Quiet,
  [switch]$PreferMachine,
  [switch]$SyncMachineToEnv,
  # Opt-in: persist secret into .env file (disabled by default per Microsoft guidance to reduce at-rest exposure)
  [switch]$PersistToEnvFile
)

$ErrorActionPreference = 'Stop'
function Write-Info($m){ if(-not $Quiet){ Write-Host $m -ForegroundColor Cyan }}
function Write-Warn($m){ if(-not $Quiet){ Write-Warning $m }}
function Write-Ok($m){ if(-not $Quiet){ Write-Host $m -ForegroundColor Green }}
function Write-Err($m){ Write-Host $m -ForegroundColor Red }

# Locate project root (script directory parent if scripts\)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$envFile = Join-Path $projectRoot '.env'
$sentinel = Join-Path $projectRoot '.syncfusion.license.synced'
$licenseFile = Join-Path $projectRoot 'license.key'

$ENV_KEY = 'SYNCFUSION_LICENSE_KEY'
$PLACEHOLDER = 'YOUR_SYNCFUSION_LICENSE_KEY_HERE'

function Get-ExistingEnvValue {
    $val = [Environment]::GetEnvironmentVariable($ENV_KEY, 'Process')
    if([string]::IsNullOrWhiteSpace($val)){ $val = [Environment]::GetEnvironmentVariable($ENV_KEY, 'User') }
    if([string]::IsNullOrWhiteSpace($val)){ $val = [Environment]::GetEnvironmentVariable($ENV_KEY, 'Machine') }
    return $val
}

function Parse-DotEnvValue {
    if(-not (Test-Path $envFile)){ return $null }
    foreach($line in Get-Content $envFile){
        if($line -match '^\s*'+[regex]::Escape($ENV_KEY)+'\s*=\s*(.+)$'){
            return $matches[1].Trim()
        }
    }
    return $null
}

function Set-DotEnvValue([string]$value){
  # Sanitize value to a single line (strip embedded newlines / leading comment markers)
  if($value -match "`r`n|`n"){ $value = ($value -split "`r?`n" | Where-Object { $_ -and ($_ -notmatch '^#') } | Select-Object -First 1).Trim() }
  if($value.StartsWith('#')){ $value = $value.TrimStart('#').Trim() }
  $lines = @()
  if(Test-Path $envFile){ $lines = Get-Content $envFile }
  $found = $false
  for($i=0; $i -lt $lines.Count; $i++){
    if($lines[$i] -match '^\s*'+[regex]::Escape($ENV_KEY)+'\s*='){ $lines[$i] = "$ENV_KEY=$value"; $found = $true }
  }
  if(-not $found){ $lines += "$ENV_KEY=$value" }
  $lines | Set-Content -NoNewline:$false -Encoding UTF8 $envFile
}

function Fetch-KeyVault([string]$vault, [string]$secret){
    try {
        if([string]::IsNullOrWhiteSpace($vault)){ return $null }
        $value = az keyvault secret show --vault-name $vault --name $secret --query value -o tsv 2>$null
        if([string]::IsNullOrWhiteSpace($value)){ return $null }
        return $value.Trim()
    } catch { return $null }
}

function Read-LicenseFile {
    if(Test-Path $licenseFile){
        try { return (Get-Content $licenseFile -Raw).Trim() } catch { return $null }
    }
    return $null
}

$kvName = if($KeyVaultName){ $KeyVaultName } elseif($env:KEYVAULT_NAME){ $env:KEYVAULT_NAME } else { $null }

$currentDotEnv = Parse-DotEnvValue
$existingEnv = Get-ExistingEnvValue
$useVal = $null
$source = $null

# Prefer machine variable explicitly if requested
if($PreferMachine -and -not $Force){
  $machineVal = [Environment]::GetEnvironmentVariable($ENV_KEY,'Machine')
  if($machineVal -and $machineVal -ne $PLACEHOLDER){
    $useVal = $machineVal
    $source = 'environment:machine'
  }
}

if(-not $Force -and -not $useVal){
  if($currentDotEnv -and $currentDotEnv -ne $PLACEHOLDER){ $useVal = $currentDotEnv; $source='dot-env' }
  elseif($existingEnv -and $existingEnv -ne $PLACEHOLDER){ $useVal = $existingEnv; $source='environment' }
}

if(-not $useVal){
  $kvVal = Fetch-KeyVault $kvName $SecretName
  if(-not $kvVal){
    # Backward-compatible fallback secret names
    foreach($alt in @('SYNCFUSION-LICENSE','SYNCFUSION_LICENSE_KEY')){
      if($alt -ieq $SecretName){ continue }
      $kvVal = Fetch-KeyVault $kvName $alt
      if($kvVal){ $SecretName = $alt; break }
    }
  }
  if($kvVal -and $kvVal -ne $PLACEHOLDER){ $useVal = $kvVal; $source = "keyvault:$kvName/$SecretName" }
}
if(-not $useVal){
    $fileVal = Read-LicenseFile
    if($fileVal -and $fileVal -ne $PLACEHOLDER){ $useVal = $fileVal; $source = 'license.key' }
}
if(-not $useVal -and -not $Quiet){
    if(-not $Host.UI.RawUI.KeyAvailable){ }
    Write-Info "Prompting for Syncfusion license (input hidden).";
    $secure = Read-Host "Enter Syncfusion License Key" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    if($plain){ $useVal = $plain.Trim(); $source='prompt' }
}

if(-not $useVal){ Write-Err "No license key provided; trial mode will be used."; exit 2 }

# Persistence strategy (secure-by-default):
# - Do NOT write the secret to disk unless -PersistToEnvFile explicitly provided.
# - Prefer process scope only; optional user/machine scope if operator requests permanence via extra switches in future.

$persistedToEnv = $false
$shouldSync = $PersistToEnvFile -or ($SyncMachineToEnv -and $source -eq 'environment:machine')
if($shouldSync){
  $existingDot = $currentDotEnv
  $canWrite = $true
  if(-not $Force -and $existingDot -and $existingDot -ne $PLACEHOLDER){
    # Only overwrite if using explicit persist flag or Force
    if(-not $PersistToEnvFile -and -not $Force){ $canWrite = $false }
  }
  if($canWrite){
    $roundTrip = $null
    try {
      Set-DotEnvValue $useVal
      $roundTrip = Parse-DotEnvValue
      if($roundTrip -eq $useVal){
        $persistedToEnv = $true
        Write-Info ".env synchronized (source=$source, len=$($useVal.Length))." 
      } else {
        $rtLen = if($roundTrip){ $roundTrip.Length } else { 0 }
        Write-Warn ".env round-trip mismatch (len=$rtLen); file not trusted." 
      }
    } catch {
      Write-Warn ".env sync failed: $($_.Exception.Message)" 
    }
  } else {
    Write-Info ".env already has non-placeholder value; skipping (use -Force or -PersistToEnvFile to override)." 
  }
} else {
  Write-Info "Skipping .env persistence (default). Use -PersistToEnvFile or -SyncMachineToEnv with -PreferMachine." 
}

# NO automatic machine/user persistence anymore (reduced surface). Can be added back with future -PersistUser / -PersistMachine flags.

# Export to current process (non-invasive; even if already set at user scope keeps existing precedence semantics)
[Environment]::SetEnvironmentVariable($ENV_KEY, $useVal, 'Process')

$len = $useVal.Length
function Test-IsLikelySyncfusionKey($k){
  if([string]::IsNullOrWhiteSpace($k)){ return $false }
  if($k -match 'YOUR_SYNCFUSION_LICENSE_KEY_HERE'){ return $false }
  if($k -match 'REPLACE_WITH_REAL_SYNCFUSION_KEY'){ return $false }
  # Syncfusion license keys are Base64-like strings typically 70-120 chars ending with '=' or '=='
  if($k -match '^[A-Za-z0-9+/=]{40,140}$'){
    return $true
  }
  return ($k.Length -ge 40)
}

if(-not (Test-IsLikelySyncfusionKey $useVal)){
  Write-Err "Provided Syncfusion license key failed heuristic validation (len=$len). Use -Force to bypass or verify secret name/value in Key Vault.";
  if(-not $Force){ exit 3 }
}
Write-Ok "SYNCFUSION_LICENSE_KEY secured (len=$len, source=$source)."

# Sentinel audit file (only if file persistence was chosen to avoid revealing secret handling when purely ephemeral)
if($PersistToEnvFile){
  "Timestamp=$(Get-Date -Format o)`nSource=$source`nLength=$len`nPersistedToEnvFile=$persistedToEnv" | Set-Content $sentinel -Encoding UTF8
}

# Basic validation heuristic (Syncfusion keys are typically > 50 chars)
if($len -lt 40){ Write-Warn "Key length unusually short. Verify correctness." }

exit 0
