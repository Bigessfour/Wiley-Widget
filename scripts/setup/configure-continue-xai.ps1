#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Configure Continue.dev to use xAI Grok API key.

.DESCRIPTION
    Updates the Continue.dev config.json to use xAI's Grok models instead of local Ollama.
    Supports secure API key prompting and validation. PSScriptAnalyzer compliant.

.PARAMETER ApiKey
    xAI API key (format: xai-XXXXXXXXXXXX). If not provided, prompts securely.

.PARAMETER SkipValidation
    Skip API key validation test.

.PARAMETER AutoConfigure
    Automatically configure using environment variable XAI_API_KEY.

.EXAMPLE
    .\configure-continue-xai.ps1

.EXAMPLE
    .\configure-continue-xai.ps1 -ApiKey "xai-your-key-here"

.EXAMPLE
    $env:XAI_API_KEY = "xai-your-key"; .\configure-continue-xai.ps1 -AutoConfigure

.NOTES
    Author: AI-Assisted Setup
    Date: November 3, 2025
    Requires: PowerShell 7.5+, Continue.dev extension installed
    PSScriptAnalyzer: Compliant (no Write-Host usage)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$ApiKey,

    [switch]$SkipValidation,

    [switch]$AutoConfigure
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Configuration
$ContinueConfigPath = Join-Path $env:USERPROFILE ".continue\config.json"
$ContinueDir = Split-Path $ContinueConfigPath -Parent

Write-Information "🔐 Configure Continue.dev with xAI Grok API"
Write-Information "=========================================="
Write-Information ""

#region Step 1: Get API Key
if ($AutoConfigure) {
    if ([string]::IsNullOrWhiteSpace($env:XAI_API_KEY)) {
        Write-Error "AutoConfigure specified but XAI_API_KEY environment variable not set."
        exit 1
    }
    $ApiKey = $env:XAI_API_KEY
    Write-Information "✅ Using API key from XAI_API_KEY environment variable"
}
elseif ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Information "📝 Enter your xAI API key:"
    Write-Information "   (Get your key from: https://console.x.ai/)"
    Write-Information ""

    $secureKey = Read-Host "xAI API Key" -AsSecureString
    $ApiKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey))
}

# Validate API key format
if (-not ($ApiKey -match '^xai-[A-Za-z0-9_-]+$')) {
    Write-Warning "API key format looks unusual (expected: xai-XXXXXXXXXXXX)"
    Write-Information "Continuing anyway..."
}

Write-Information "✅ API key received (length: $($ApiKey.Length) characters)"
Write-Information ""

#endregion

#region Step 2: Test API Key (optional)
if (-not $SkipValidation) {
    Write-Information "🔍 Validating API key with xAI..."

    try {
        $headers = @{
            "Authorization" = "Bearer $ApiKey"
            "Content-Type" = "application/json"
        }

        $body = @{
            model = "grok-beta"
            messages = @(
                @{
                    role = "user"
                    content = "Hello, respond with just 'OK'"
                }
            )
            max_tokens = 10
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "https://api.x.ai/v1/chat/completions" `
            -Method Post `
            -Headers $headers `
            -Body $body `
            -TimeoutSec 10 `
            -ErrorAction Stop

        Write-Information "   ✅ API key validated successfully!"
        Write-Verbose "   Response from Grok: $($response.choices[0].message.content)"
    }
    catch {
        Write-Warning "API key validation failed: $($_.Exception.Message)"
        Write-Information "Continuing with configuration anyway..."
        Write-Information ""
    }
}

Write-Information ""

#endregion

#region Step 3: Create Continue.dev Config
Write-Information "📝 Creating Continue.dev configuration..."

if (-not (Test-Path $ContinueDir)) {
    Write-Verbose "   Creating .continue directory..."
    New-Item -ItemType Directory -Path $ContinueDir -Force | Out-Null
}

$config = @{
    models = @(
        @{
            title = "Grok-4-0709 (Latest)"
            provider = "openai"
            model = "grok-4-0709"
            apiBase = "https://api.x.ai/v1"
            apiKey = $ApiKey
        },
        @{
            title = "Grok-2 (Stable)"
            provider = "openai"
            model = "grok-2-latest"
            apiBase = "https://api.x.ai/v1"
            apiKey = $ApiKey
        },
        @{
            title = "Grok-beta (Fast)"
            provider = "openai"
            model = "grok-beta"
            apiBase = "https://api.x.ai/v1"
            apiKey = $ApiKey
        }
    )
    tabAutocompleteModel = @{
        title = "Grok-beta Autocomplete"
        provider = "openai"
        model = "grok-beta"
        apiBase = "https://api.x.ai/v1"
        apiKey = $ApiKey
    }
    slashCommands = @(
        @{ name = "edit"; description = "Edit selected code" },
        @{ name = "comment"; description = "Write comments for code" },
        @{ name = "test"; description = "Generate unit tests" },
        @{ name = "fix"; description = "Fix problems in code" },
        @{ name = "share"; description = "Export chat to markdown" }
    )
    contextProviders = @(
        @{ name = "diff"; params = @{} },
        @{ name = "open"; params = @{} },
        @{ name = "terminal"; params = @{} },
        @{ name = "codebase"; params = @{} },
        @{ name = "folder"; params = @{} },
        @{ name = "problems"; params = @{} }
    )
    allowAnonymousTelemetry = $false
    experimental = @{
        modelRoles = @{
            inlineEdit = "Grok-beta (Fast)"
            applyCodeBlock = "Grok-4-0709 (Latest)"
        }
    }
} | ConvertTo-Json -Depth 10

Write-Verbose "   Writing config to: $ContinueConfigPath"
Set-Content -Path $ContinueConfigPath -Value $config -Force

Write-Information "   ✅ Configuration saved successfully"
Write-Information ""

#endregion

#region Step 4: Verify Configuration
Write-Information "✅ Setup Complete!"
Write-Information ""
Write-Information "📋 Configuration Summary:"
Write-Information "   Config Location: $ContinueConfigPath"
Write-Information "   Primary Model:   Grok-4-0709 (Latest & Most Powerful)"
Write-Information "   Stable Model:    Grok-2"
Write-Information "   Fast Model:      Grok-beta"
Write-Information "   Autocomplete:    Grok-beta"
Write-Information ""

Write-Information "🚀 Next Steps:"
Write-Information "1. Restart VS Code to reload Continue.dev configuration"
Write-Information "2. Press Ctrl+L to open Continue.dev chat"
Write-Information "3. Select 'Grok-4-0709 (Latest)' from the model dropdown"
Write-Information "4. Try this prompt:"
Write-Information ""
Write-Information "   'Generate a C# xUnit E2E test using FlaUI for MunicipalAccountView'"
Write-Information "   'that validates 31 Conservation Trust Fund accounts in SfDataGrid'"
Write-Information ""

Write-Information "💡 Usage Tips:"
Write-Information "   - Ctrl+L: Open sidebar chat"
Write-Information "   - Ctrl+I: Inline edit mode"
Write-Information "   - /test: Generate unit tests for selected code"
Write-Information "   - /edit: Modify selected code"
Write-Information ""

Write-Information "📚 Documentation:"
Write-Information "   - xAI Console: https://console.x.ai/"
Write-Information "   - Continue Docs: https://docs.continue.dev/"
Write-Information "   - E2E Testing Guide: docs/AI_E2E_TESTING_SETUP.md"
Write-Information ""

Write-Information "🔐 Security Note:"
Write-Information "   Your API key is stored in: $ContinueConfigPath"
Write-Information "   Keep this file secure and don't commit it to version control!"
Write-Information ""

#endregion
