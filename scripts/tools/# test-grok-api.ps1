# test-grok-api.ps1
# Tests xAI Grok API with configured model (grok-4.1)

param(
    [string]$ApiKey,
    [string]$Model = "grok-4.1",
    [string]$Endpoint = "https://api.x.ai/v1",
    [string]$TestMessage = "Say 'Test successful!' and nothing else."
)

# Step 1: Retrieve API key from environment if not provided
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Host "Checking for API key in environment variables..." -ForegroundColor Gray

    # Try hierarchical environment variable (modern convention)
    $ApiKey = $env:XAI__ApiKey
    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        $ApiKey = $env:XAI_API_KEY
    }

    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        Write-Host "❌ ERROR: No API key found." -ForegroundColor Red
        Write-Host "Please set one of:" -ForegroundColor Yellow
        Write-Host "  1. XAI__ApiKey environment variable (recommended)" -ForegroundColor Yellow
        Write-Host "  2. XAI_API_KEY environment variable (legacy)" -ForegroundColor Yellow
        Write-Host "  3. Pass -ApiKey parameter" -ForegroundColor Yellow
        exit 1
    }
}

# Step 2: Prepare request
Write-Host "🔍 Grok API Test" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host "Model:    $Model"
Write-Host "Endpoint: $Endpoint/chat/completions"
Write-Host "API Key:  $(($ApiKey.Substring(0, 4)) + '...' + ($ApiKey.Substring($ApiKey.Length - 4)))" -ForegroundColor Yellow
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $ApiKey"
    "Content-Type"  = "application/json"
}

$body = @{
    model    = $Model
    messages = @(
        @{ role = "user"; content = $TestMessage }
    )
    max_tokens = 50
    stream     = $false
    temperature = 0.3
} | ConvertTo-Json

# Step 3: Send test request
Write-Host "📤 Sending test request..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "$Endpoint/chat/completions" `
        -Method Post `
        -Headers $headers `
        -Body $body `
        -TimeoutSec 10

    Write-Host "✅ SUCCESS! Response received:" -ForegroundColor Green
    Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

    if ($response.choices -and $response.choices.Count -gt 0) {
        $content = $response.choices[0].message.content
        Write-Host "Response: $content" -ForegroundColor Green
        Write-Host ""
        Write-Host "Model: $($response.model)" -ForegroundColor Gray
        Write-Host "Tokens used: $($response.usage.total_tokens)" -ForegroundColor Gray
    }
    else {
        Write-Host "Response object: $($response | ConvertTo-Json)" -ForegroundColor Yellow
    }

    exit 0
}
catch {
    Write-Host "❌ ERROR: Request failed" -ForegroundColor Red
    Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

    $errorResponse = $_
    if ($errorResponse.Exception.Response) {
        $statusCode = $errorResponse.Exception.Response.StatusCode.Value__
        Write-Host "HTTP Status: $statusCode" -ForegroundColor Red

        # Try to parse error response
        try {
            $reader = New-Object System.IO.StreamReader($errorResponse.Exception.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd() | ConvertFrom-Json
            Write-Host "Error Message: $($errorBody.error.message)" -ForegroundColor Red

            if ($statusCode -eq 404) {
                Write-Host "💡 Hint: Model may not exist or endpoint is incorrect" -ForegroundColor Yellow
            }
            elseif ($statusCode -eq 401) {
                Write-Host "💡 Hint: API key is invalid or expired" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "Raw error: $($errorResponse)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }

    exit 1
}
