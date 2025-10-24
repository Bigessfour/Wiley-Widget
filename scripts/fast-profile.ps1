# Wiley Widget PowerShell Profile - OPTIMIZED VERSION
# ========================================================
# Performance improvements:
# - Lazy loading for Azure/MCP modules
# - Background loading for Key Vault secrets
# - Conditional loading based on context
# - Cached expensive operations
# ========================================================

# Fast startup - only essential setup
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:POWERSHELL_TELEMETRY_OPTOUT = "1"

# Measure load time
$profileStartTime = Get-Date

# Essential aliases (immediate)
Set-Alias -Name ll -Value Get-ChildItem
Set-Alias -Name grep -Value Select-String

# Lazy loading functions for heavy components
function Initialize-AzureEnvironment {
    if (-not $global:AzureInitialized) {
        Write-Host "🔧 Loading Azure environment..." -ForegroundColor Yellow
        # Load Azure modules only when needed
        if (Get-Command az -ErrorAction SilentlyContinue) {
            # Azure CLI is available, load minimal Azure modules
            Import-Module Az.Accounts -ErrorAction SilentlyContinue
        }
        $global:AzureInitialized = $true
    }
}

function Initialize-MCPEnvironment {
    if (-not $global:MCPInitialized) {
        Write-Host "🔧 Loading MCP environment..." -ForegroundColor Yellow

        # Background loading of Key Vault secrets
        $keyVaultJob = Start-Job -ScriptBlock {
            param($using:using:using:using:using:using:using:using:using:using:using:using:using:using:using:using:using:vaultName)
            try {
                # Load secrets asynchronously
                $secrets = @{}
                $secretNames = @('XAI_API_KEY', 'GITHUB_TOKEN', 'SYNCFUSION-LICENSE-KEY')

                foreach ($secretName in $secretNames) {
                    try {
                        $secret = az keyvault secret show --vault-name $vaultName --name $secretName 2>$null
                        if ($secret) {
                            $secrets[$secretName] = ($secret | ConvertFrom-Json).value
                        }
                    }
                    catch {
                        # Silently continue if secret not found
                    }
                }
                return $secrets
            }
            catch {
                return @{}
            }
        } -ArgumentList "wiley-widget-secrets"

        # Store job for later retrieval
        $global:KeyVaultJob = $keyVaultJob

        $global:MCPInitialized = $true
    }
}

# Function to get Key Vault secrets (lazy loading)
function Get-KeyVaultSecret {
    param([string]$SecretName)

    # Initialize MCP if not done
    if (-not $global:MCPInitialized) {
        Initialize-MCPEnvironment
    }

    # Wait for background job if still running
    if ($global:KeyVaultJob -and $global:KeyVaultJob.State -eq 'Running') {
        Write-Host "⏳ Waiting for Key Vault secrets..." -ForegroundColor Yellow
        $global:KeyVaultJob | Wait-Job | Out-Null
    }

    # Get results from background job
    if ($global:KeyVaultJob) {
        $secrets = $global:KeyVaultJob | Receive-Job
        $global:KeyVaultJob = $null  # Clean up

        # Cache results
        if (-not $global:CachedSecrets) {
            $global:CachedSecrets = @{}
        }
        foreach ($key in $secrets.Keys) {
            $global:CachedSecrets[$key] = $secrets[$key]
        }
    }

    # Return cached secret
    if ($global:CachedSecrets -and $global:CachedSecrets.ContainsKey($SecretName)) {
        return $global:CachedSecrets[$SecretName]
    }

    return $null
}

# Override commands to trigger lazy loading
$lazyLoadCommands = @(
    @{Command = 'az'; Loader = ${function:Initialize-AzureEnvironment} },
    @{Command = 'azd'; Loader = ${function:Initialize-AzureEnvironment} },
    @{Command = 'kubectl'; Loader = ${function:Initialize-MCPEnvironment} }
)

foreach ($item in $lazyLoadCommands) {
    if (Get-Command $item.Command -ErrorAction SilentlyContinue) {
        $originalCommand = Get-Command $item.Command
        $loader = $item.Loader

        # Create wrapper function
        $wrapperScript = @"
function global:$($item.Command) {
    `$loader.Invoke()
    & `$originalCommand @args
}
"@
        Invoke-Expression $wrapperScript
    }
}

# Fast prompt
function prompt {
    "$pwd$('>' * ($nestedPromptLevel + 1)) "
}

# Utility functions
function Get-ProfileLoadTime {
    $loadTime = (Get-Date) - $global:profileStartTime
    Write-Host "Profile loaded in: $($loadTime.TotalMilliseconds)ms" -ForegroundColor Green
}

function Reload-Profile {
    $global:AzureInitialized = $false
    $global:MCPInitialized = $false
    $global:CachedSecrets = @{}
    . $PROFILE
    Write-Host "Profile reloaded with optimizations!" -ForegroundColor Green
}

# Background initialization for non-critical components
Start-Job -ScriptBlock {
    # Pre-load commonly used assemblies
    [System.AppDomain]::CurrentDomain.GetAssemblies() | Out-Null

    # Cache environment info
    $global:CachedEnv = @{
        IsAdmin           = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        PowerShellVersion = $PSVersionTable.PSVersion
        OSVersion         = (Get-CimInstance Win32_OperatingSystem).Caption
    }
} | Out-Null

# Profile load time reporting
$profileLoadTime = (Get-Date) - $profileStartTime
$loadTimeMs = [math]::Round($profileLoadTime.TotalMilliseconds, 2)

if ($loadTimeMs -gt 3000) {
    Write-Host "🐌 Profile loaded in: ${loadTimeMs}ms (consider optimization)" -ForegroundColor Red
}
elseif ($loadTimeMs -gt 1000) {
    Write-Host "⚠️  Profile loaded in: ${loadTimeMs}ms" -ForegroundColor Yellow
}
else {
    Write-Host "✅ Profile loaded in: ${loadTimeMs}ms" -ForegroundColor Green
}

# Display optimization status
Write-Host "🔧 Lazy loading enabled for: Azure, MCP, Key Vault" -ForegroundColor Cyan
