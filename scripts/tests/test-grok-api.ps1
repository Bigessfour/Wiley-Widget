#Requires -Version 7.5
<#
.SYNOPSIS
    Validates xAI Grok API configuration and connectivity with comprehensive testing.

.DESCRIPTION
    This script performs comprehensive testing of Grok API integration:
    - Validates API key from environment or parameter
    - Tests HTTP connectivity to Grok endpoints
    - Verifies response parsing and format
    - Checks model availability (grok-4.1)
    - Generates structured test report as JSON
    - Returns proper exit codes for CI/CD integration

    Default model is grok-4.1 (from appsettings.json configuration).

.PARAMETER ApiKey
    The xAI API key for authentication. If not provided, checks environment variables:
    - XAI__ApiKey (modern convention, hierarchical format)
    - XAI_API_KEY (legacy format)

.PARAMETER Model
    Model to test (default: grok-4.1). Supports grok-4.1, grok-2, or other available models.

.PARAMETER Endpoint
    Grok API base endpoint (default: https://api.x.ai/v1). Script appends /chat/completions.

.PARAMETER TestMessage
    Custom test message to send (default: validates model availability).

.PARAMETER OutputDirectory
    Directory for JSON test results (default: ./diagnostics).

.PARAMETER Timeout
    Request timeout in seconds (default: 30). Range: 1-300.

.EXAMPLE
    # Test with environment variables
    $env:XAI__ApiKey = 'your-api-key-here'
    .\scripts\tests\test-grok-api.ps1

.EXAMPLE
    # Test with explicit API key
    .\scripts\tests\test-grok-api.ps1 -ApiKey 'sk-...' -Model 'grok-4.1'

.EXAMPLE
    # Test with custom output location
    .\scripts\tests\test-grok-api.ps1 -OutputDirectory './test-results'

.NOTES
    Exit Codes:
    - 0: All tests passed
    - 1: One or more tests failed
    - 2: Configuration error

.LINK
    https://console.x.ai/
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, HelpMessage = 'xAI API key for authentication')]
    [ValidateNotNullOrEmpty()]
    [string]
    $ApiKey,

    [Parameter(Mandatory = $false, HelpMessage = 'Model identifier to test')]
    [ValidateNotNullOrEmpty()]
    [string]
    $Model = 'grok-4-1-fast-reasoning',

    [Parameter(Mandatory = $false, HelpMessage = 'API endpoint URL')]
    [ValidatePattern('^https?://')]
    [string]
    $Endpoint = 'https://api.x.ai/v1/responses',

    [Parameter(Mandatory = $false, HelpMessage = 'Test message to send')]
    [ValidateNotNullOrEmpty()]
    [string]
    $TestMessage = 'Say "Test successful!" and nothing else.',

    [Parameter(Mandatory = $false, HelpMessage = 'Output directory for results')]
    [ValidateNotNullOrEmpty()]
    [string]
    $OutputDirectory = './diagnostics',

    [Parameter(Mandatory = $false, HelpMessage = 'Request timeout in seconds')]
    [ValidateRange(1, 300)]
    [int]
    $Timeout = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$VerbosePreference = $PSBoundParameters['Verbose'] ? 'Continue' : 'SilentlyContinue'

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

<#
.SYNOPSIS
    Writes formatted log message to host.
#>
function Write-TestLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]
        $Message,

        [Parameter(Mandatory = $false)]
        [ValidateSet('Info', 'Success', 'Error', 'Warning')]
        [string]
        $Level = 'Info'
    )

    $colors = @{
        'Info'    = 'Cyan'
        'Success' = 'Green'
        'Error'   = 'Red'
        'Warning' = 'Yellow'
    }

    $prefix = @{
        'Info'    = '▶'
        'Success' = '✅'
        'Error'   = '❌'
        'Warning' = '⚠️'
    }

    Write-Host "$($prefix[$Level]) $Message" -ForegroundColor $colors[$Level]
}

<#
.SYNOPSIS
    Checks API key availability from environment variables.
#>
function Get-ApiKeyFromEnvironment {
    [CmdletBinding()]
    param()

    Write-Verbose 'Checking for API key in environment variables'

    # Check hierarchical environment variable (modern convention)
    $key = $env:XAI__ApiKey
    if ([string]::IsNullOrWhiteSpace($key)) {
        # Check legacy format
        $key = $env:XAI_API_KEY
    }

    if ([string]::IsNullOrWhiteSpace($key)) {
        return $null
    }

    return $key
}

<#
.SYNOPSIS
    Validates API key format and length.
#>
function Test-ApiKeyFormat {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]
        $Key
    )

    if ([string]::IsNullOrWhiteSpace($Key)) {
        return $false
    }

    if ($Key.Length -lt 10) {
        return $false
    }

    return $true
}

# ============================================================================
# INITIALIZATION
# ============================================================================

Write-Host ''
Write-Host '=' * 70 -ForegroundColor Cyan
Write-Host '  🔍 Grok API Validation Test' -ForegroundColor Cyan
Write-Host '=' * 70 -ForegroundColor Cyan
Write-Host ''

# Resolve API key
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Verbose 'API key not provided as parameter, checking environment'
    $ApiKey = Get-ApiKeyFromEnvironment

    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        Write-TestLog 'API key not configured' -Level Error
        Write-Host ''
        Write-Host 'Set API key using one of these methods:' -ForegroundColor Yellow
        Write-Host '  1. Environment variable: XAI__ApiKey (recommended)' -ForegroundColor Yellow
        Write-Host '  2. Environment variable: XAI_API_KEY (legacy)' -ForegroundColor Yellow
        Write-Host '  3. Parameter: -ApiKey "your-key"' -ForegroundColor Yellow
        Write-Host ''
        exit 2
    }
}

# Create output directory
if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    $null = New-Item -ItemType Directory -Path $OutputDirectory -Force -ErrorAction Stop
    Write-Verbose "Created output directory: $OutputDirectory"
}

# ============================================================================
# TEST CONFIGURATION
# ============================================================================

$testResults = [PSCustomObject]@{
    Timestamp              = Get-Date -Format 'o'
    Configuration          = [PSCustomObject]@{
        Model              = $Model
        Endpoint           = $Endpoint
        Timeout            = "$Timeout seconds"
        OutputDirectory    = $OutputDirectory
    }
    ApiKeyValidation       = $null
    EndpointConnectivity   = $null
    RequestResponse        = $null
    ModelAvailability      = $null
    RateLimitStatus        = $null
    OverallStatus          = 'Pending'
    TestDetails            = @()
}

Write-TestLog "Model:     $Model"
Write-TestLog "Endpoint:  $Endpoint"
Write-TestLog "Timeout:   $Timeout seconds"
Write-Host ''

# ============================================================================
# TEST 1: VALIDATE API KEY
# ============================================================================

Write-Verbose 'Testing API key format'

try {
    if (-not (Test-ApiKeyFormat -Key $ApiKey)) {
        throw 'API key format invalid (too short or empty)'
    }

    $maskedKey = $ApiKey.Substring(0, 4) + '...' + $ApiKey.Substring($ApiKey.Length - 4)
    Write-TestLog "API key validated: $maskedKey" -Level Success

    $testResults.ApiKeyValidation = [PSCustomObject]@{
        Status  = 'Pass'
        Message = "API key present and valid format (length: $($ApiKey.Length))"
    }

    $testResults.TestDetails += [PSCustomObject]@{
        Name   = 'API Key Validation'
        Status = 'Pass'
        Time   = Get-Date -Format 'o'
    }
}
catch {
    Write-TestLog $_.Exception.Message -Level Error
    $testResults.ApiKeyValidation = [PSCustomObject]@{
        Status  = 'Fail'
        Message = $_.Exception.Message
    }

    $testResults.TestDetails += [PSCustomObject]@{
        Name    = 'API Key Validation'
        Status  = 'Fail'
        Error   = $_.Exception.Message
        Time    = Get-Date -Format 'o'
    }

    exit 1
}

# ============================================================================
# TEST 2: TEST ENDPOINT CONNECTIVITY & REQUEST
# ============================================================================

Write-Verbose 'Testing endpoint connectivity'
Write-Host ''
Write-TestLog 'Sending test request...'

try {
    $headers = @{
        'Authorization' = "Bearer $ApiKey"
        'Content-Type'  = 'application/json'
    }

    $payload = @{
        model   = $Model
        input   = @(
            @{ role = 'system'; content = 'You are Grok, a highly intelligent, helpful AI assistant.' }
            @{ role = 'user'; content = $TestMessage }
        )
    }

    $bodyJson = $payload | ConvertTo-Json -Depth 10 -ErrorAction Stop
    $uri = $Endpoint

    Write-Verbose "Request URI: $uri"
    Write-Verbose "Payload: $bodyJson"

    $requestParams = @{
        Uri             = $uri
        Method          = 'Post'
        Headers         = $headers
        Body            = $bodyJson
        TimeoutSec      = $Timeout
        ErrorAction     = 'Stop'
        UseBasicParsing = $true
    }

    $response = Invoke-WebRequest @requestParams

    if ($response.StatusCode -eq 200) {
        Write-TestLog 'Request successful (HTTP 200)' -Level Success

        $testResults.EndpointConnectivity = [PSCustomObject]@{
            Status     = 'Pass'
            StatusCode = $response.StatusCode
            Message    = 'Successfully connected to Grok API endpoint'
        }

        $testResults.TestDetails += [PSCustomObject]@{
            Name   = 'Endpoint Connectivity'
            Status = 'Pass'
            Time   = Get-Date -Format 'o'
        }
    }
}
catch [System.Net.Http.HttpRequestException] {
    $statusCode = $_.Exception.Response.StatusCode.Value__

    Write-TestLog "HTTP $statusCode - Request failed" -Level Error

    if ($statusCode -eq 401) {
        Write-Host 'Cause: Invalid or expired API key' -ForegroundColor Yellow
    }
    elseif ($statusCode -eq 404) {
        Write-Host 'Cause: Model not found or endpoint incorrect' -ForegroundColor Yellow
    }

    $testResults.EndpointConnectivity = [PSCustomObject]@{
        Status     = 'Fail'
        StatusCode = $statusCode
        Message    = "HTTP $statusCode error"
    }

    $testResults.TestDetails += [PSCustomObject]@{
        Name    = 'Endpoint Connectivity'
        Status  = 'Fail'
        Error   = "HTTP $statusCode"
        Time    = Get-Date -Format 'o'
    }

    exit 1
}
catch [System.TimeoutException] {
    Write-TestLog "Request timeout after $Timeout seconds" -Level Error

    $testResults.EndpointConnectivity = [PSCustomObject]@{
        Status  = 'Fail'
        Message = "Request timed out after $Timeout seconds"
    }

    $testResults.TestDetails += [PSCustomObject]@{
        Name    = 'Endpoint Connectivity'
        Status  = 'Fail'
        Error   = "Timeout after $Timeout seconds"
        Time    = Get-Date -Format 'o'
    }

    exit 1
}
catch {
    Write-TestLog $_.Exception.Message -Level Error

    $testResults.EndpointConnectivity = [PSCustomObject]@{
        Status  = 'Fail'
        Message = $_.Exception.Message
    }

    $testResults.TestDetails += [PSCustomObject]@{
        Name    = 'Endpoint Connectivity'
        Status  = 'Fail'
        Error   = $_.Exception.Message
        Time    = Get-Date -Format 'o'
    }

    exit 1
}

# ============================================================================
# TEST 3: PARSE RESPONSE
# ============================================================================

Write-Verbose 'Parsing response'

try {
    $responseContent = $response.Content | ConvertFrom-Json -ErrorAction Stop

    Write-TestLog 'Response parsed successfully' -Level Success

    $testResults.RequestResponse = [PSCustomObject]@{
        Status  = 'Pass'
        Message = 'Response successfully parsed as JSON'
    }

    if ($responseContent.output -and $responseContent.output.Count -gt 0) {
        $firstOutput = $responseContent.output[0]

        if ($firstOutput.content -and $firstOutput.content.Count -gt 0) {
            $content = $firstOutput.content[0].text
        } else {
            $content = $firstOutput | ConvertTo-Json
        }

        $model = $responseContent.model
        $tokensUsed = $responseContent.usage.total_tokens
        $inputTokens = $responseContent.usage.input_tokens
        $outputTokens = $responseContent.usage.output_tokens

        Write-TestLog "Model: $model" -Level Info
        Write-TestLog "Response: $content" -Level Success
        Write-TestLog "Tokens used: $tokensUsed (input: $inputTokens, output: $outputTokens)" -Level Info

        $testResults.ModelAvailability = [PSCustomObject]@{
            Status           = 'Pass'
            Model            = $model
            ResponseReceived = $true
            TotalTokens      = $tokensUsed
            InputTokens      = $inputTokens
            OutputTokens     = $outputTokens
        }

        $testResults.TestDetails += [PSCustomObject]@{
            Name   = 'Model Availability'
            Status = 'Pass'
            Model  = $model
            Time   = Get-Date -Format 'o'
        }
    }
    else {
        throw 'No output in response'
    }

    $testResults.TestDetails += [PSCustomObject]@{
        Name   = 'Response Parsing'
        Status = 'Pass'
        Time   = Get-Date -Format 'o'
    }
}
catch {
    Write-TestLog "Failed to parse response: $($_.Exception.Message)" -Level Error

    $testResults.RequestResponse = [PSCustomObject]@{
        Status  = 'Fail'
        Message = "Failed to parse JSON: $($_.Exception.Message)"
    }

    $testResults.TestDetails += [PSCustomObject]@{
        Name    = 'Response Parsing'
        Status  = 'Fail'
        Error   = $_.Exception.Message
        Time    = Get-Date -Format 'o'
    }

    exit 1
}

# ============================================================================
# TEST 4: CHECK RATE LIMIT HEADERS (x.ai /v1/responses doesn't provide these)
# ============================================================================

Write-Verbose 'Checking rate limit headers (x.ai /v1/responses may not provide standard rate limit headers)'
Write-Host ''

try {
    $remaining = $response.Headers['X-RateLimit-Remaining']
    $limit = $response.Headers['X-RateLimit-Limit']

    if ($remaining -and $limit) {
        Write-TestLog "Rate limit: $remaining/$limit requests remaining" -Level Info

        $testResults.RateLimitStatus = [PSCustomObject]@{
            Status    = 'Pass'
            Remaining = $remaining
            Limit     = $limit
        }

        $testResults.TestDetails += [PSCustomObject]@{
            Name      = 'Rate Limit Check'
            Status    = 'Pass'
            Remaining = $remaining
            Limit     = $limit
            Time      = Get-Date -Format 'o'
        }
    }
    else {
        Write-TestLog 'Rate limit headers not present (normal for x.ai /v1/responses endpoint)' -Level Info

        $testResults.RateLimitStatus = [PSCustomObject]@{
            Status  = 'Pass'
            Message = 'Rate limit headers not present (expected for /v1/responses endpoint)'
        }

        $testResults.TestDetails += [PSCustomObject]@{
            Name   = 'Rate Limit Check'
            Status = 'Pass'
            Time   = Get-Date -Format 'o'
        }
    }
}
catch {
    Write-Verbose "Unable to retrieve rate limit headers: $($_.Exception.Message)"
}

# ============================================================================
# FINAL REPORT
# ============================================================================

Write-Host ''
Write-Host '=' * 70 -ForegroundColor Cyan
Write-Host '  📊 TEST RESULTS SUMMARY' -ForegroundColor Cyan
Write-Host '=' * 70 -ForegroundColor Cyan
Write-Host ''

$passCount = @($testResults.TestDetails | Where-Object { $_.Status -eq 'Pass' }).Count
$failCount = @($testResults.TestDetails | Where-Object { $_.Status -eq 'Fail' }).Count
$warnCount = @($testResults.TestDetails | Where-Object { $_.Status -eq 'Warn' }).Count

Write-TestLog "Passed:  $passCount" -Level Info
Write-TestLog "Failed:  $failCount" -Level $(if ($failCount -gt 0) { 'Error' } else { 'Info' })
Write-TestLog "Warned:  $warnCount" -Level $(if ($warnCount -gt 0) { 'Warning' } else { 'Info' })

if ($failCount -eq 0) {
    $testResults.OverallStatus = 'Pass'
    Write-Host ''
    Write-TestLog 'ALL TESTS PASSED ✨' -Level Success
}
else {
    $testResults.OverallStatus = 'Fail'
}

# Save results to JSON
$jsonPath = Join-Path -Path $OutputDirectory -ChildPath "grok-api-test-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$testResults | ConvertTo-Json -Depth 10 | Set-Content -Path $jsonPath -Encoding UTF8 -ErrorAction Stop
Write-Host ''
Write-Host "Results saved: $jsonPath" -ForegroundColor Gray

# Exit with proper code
if ($failCount -eq 0) {
    exit 0
}
else {
    exit 1
}
