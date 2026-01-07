# WileyWidget AI API Reference

**Version**: 1.0
**Last Updated**: 2026-01-03

## 📋 Table of Contents

1. [IAIService Interface](#iaiservice-interface)
2. [IGrokRecommendationService Interface](#igrokrecommendationservice-interface)
3. [IGrokSupercomputer Interface](#igroksupercomputer-interface)
4. [Data Models](#data-models)
5. [Error Handling](#error-handling)
6. [Code Examples](#code-examples)

---

## IAIService Interface

Core AI service for conversational AI, data analysis, and insights generation.

**Namespace**: `WileyWidget.Services.Abstractions`
**Implementation**: `WileyWidget.Services.XAIService`

### IAIService Methods

#### GetInsightsAsync

Primary AI query method for general insights and Q&A.

```csharp
Task<string> GetInsightsAsync(
    string context,
    string question,
    CancellationToken cancellationToken = default)
```

**Parameters**:

- `context` (string): Context information for the AI (max 10,000 chars)
- `question` (string): User's question (max 5,000 chars)
- `cancellationToken` (CancellationToken): Optional cancellation token

**Returns**: AI-generated response as string

**Throws**:

- `ArgumentException`: Invalid input (null, empty, or exceeds length limits)
- `InvalidOperationException`: API key invalid or service disabled
- `BrokenCircuitException`: Circuit breaker is open

**Example**:

```csharp
var result = await _aiService.GetInsightsAsync(
    "Municipal Budget Analysis",
    "What are our top 3 spending departments this quarter?"
);
Console.WriteLine(result);
```

---

#### GetInsightsWithStatusAsync

Returns AI response with HTTP status codes for UI error handling.

```csharp
Task<AIResponseResult> GetInsightsWithStatusAsync(
    string context,
    string question,
    CancellationToken cancellationToken = default)
```

**Parameters**: Same as `GetInsightsAsync`

**Returns**: `AIResponseResult` record with:

- `Content` (string): AI response or error message
- `HttpStatusCode` (int): HTTP status (200=success, 4xx/5xx=error)
- `ErrorCode` (string?): Machine-readable error code
- `RawErrorBody` (string?): Raw API error response

**Example**:

```csharp
var result = await _aiService.GetInsightsWithStatusAsync(
    "Budget", "Analyze spending"
);

if (result.HttpStatusCode == 200)
{
    ShowSuccess(result.Content);
}
else if (result.ErrorCode == "RateLimited")
{
    ShowWarning("AI service is busy. Please wait.");
}
else
{
    ShowError($"Error: {result.Content}");
}
```

---

#### SendPromptAsync

Direct prompt execution without context building.

```csharp
Task<AIResponseResult> SendPromptAsync(
    string prompt,
    CancellationToken cancellationToken = default)
```

**Parameters**:

- `prompt` (string): Raw prompt to send to AI

**Returns**: `AIResponseResult`

**Example**:

```csharp
var prompt = @"
Analyze this budget data and provide 3 key insights:
- Total Budget: $1,500,000
- Actual Spending: $1,650,000
- Variance: +10%
";

var result = await _aiService.SendPromptAsync(prompt);
```

---

#### SendMessageAsync

Send message with conversation history for chat interfaces.

```csharp
Task<string> SendMessageAsync(
    string message,
    object conversationHistory)
```

**Parameters**:

- `message` (string): User's message
- `conversationHistory` (object): Conversation context (typically `List<ChatMessage>`)

**Returns**: AI response string

**Example**:

```csharp
var messages = new List<ChatMessage>
{
    ChatMessage.CreateUserMessage("What is our water budget?"),
    ChatMessage.CreateAIMessage("Your water budget is $500,000."),
    ChatMessage.CreateUserMessage("What about sewer?")
};

var response = await _aiService.SendMessageAsync(
    messages.Last().Message,
    messages
);
```

---

#### ValidateApiKeyAsync

Validates an API key without mutating service configuration.

```csharp
Task<AIResponseResult> ValidateApiKeyAsync(
    string apiKey,
    CancellationToken cancellationToken = default)
```

**Parameters**:

- `apiKey` (string): API key to validate

**Returns**: `AIResponseResult` (200=valid, 4xx=invalid)

**Example**:

```csharp
var validation = await _aiService.ValidateApiKeyAsync(userProvidedKey);

if (validation.HttpStatusCode == 200)
{
    // Key is valid, can save it
    await SaveApiKeyAsync(userProvidedKey);
}
else
{
    ShowError($"Invalid API key: {validation.ErrorCode}");
}
```

---

#### UpdateApiKeyAsync

Hot-swaps API key for the running service (key rotation).

```csharp
Task UpdateApiKeyAsync(string newApiKey)
```

**Parameters**:

- `newApiKey` (string): New API key to use

**Returns**: Task (void)

**Example**:

```csharp
// Validate before updating
var validation = await _aiService.ValidateApiKeyAsync(newKey);
if (validation.HttpStatusCode == 200)
{
    await _aiService.UpdateApiKeyAsync(newKey);
    await UpdateKeyVaultAsync(newKey); // Persist to Key Vault
    _logger.LogInformation("API key rotated successfully");
}
```

---

#### AnalyzeDataAsync

Analyze structured data and provide insights.

```csharp
Task<string> AnalyzeDataAsync(
    string data,
    string analysisType,
    CancellationToken cancellationToken = default)
```

**Parameters**:

- `data` (string): Data to analyze (JSON, CSV, or text)
- `analysisType` (string): Type of analysis (e.g., "Budget", "Revenue", "Compliance")

**Returns**: Analysis results as string

**Example**:

```csharp
var budgetData = JsonSerializer.Serialize(new {
    TotalBudget = 1500000,
    ActualSpending = 1650000,
    Departments = new[] { "Water", "Sewer", "Trash" }
});

var analysis = await _aiService.AnalyzeDataAsync(
    budgetData,
    "Budget Analysis"
);
```

---

#### BatchGetInsightsAsync

Process multiple queries in batches (3 per batch) for efficiency.

```csharp
Task<Dictionary<string, string>> BatchGetInsightsAsync(
    IEnumerable<(string context, string question)> requests,
    CancellationToken cancellationToken = default)
```

**Parameters**:

- `requests`: Collection of (context, question) tuples

**Returns**: Dictionary mapping cache keys to responses

```csharp
var requests = new[]
{
    ("Budget", "Analyze Q1 spending"),
    ("Revenue", "Forecast Q2 revenue"),

var results = await _aiService.BatchGetInsightsAsync(requests);

foreach (var (key, response) in results)
{
    Console.WriteLine($"Result: {response}");
}
```

---

## IGrokRecommendationService Interface

AI-driven rate recommendations with fallback to rule-based calculations.

**Namespace**: `WileyWidget.Business.Interfaces`
**Implementation**: `WileyWidget.Business.Services.GrokRecommendationService`

### IGrokRecommendationService Methods

#### GetRecommendedAdjustmentFactorsAsync

Calculate rate adjustment factors for departments.

```csharp
Task<RecommendationResult> GetRecommendedAdjustmentFactorsAsync(
    Dictionary<string, decimal> departmentExpenses,
    decimal targetProfitMargin = 15.0m,
    CancellationToken cancellationToken = default)
```

**Parameters**:

- `departmentExpenses`: Dictionary of department names → monthly expenses
- `targetProfitMargin`: Desired profit margin percentage (0-50%)
- `cancellationToken`: Optional cancellation token

**Returns**: `RecommendationResult` with adjustment factors

**Throws**:

- `ArgumentException`: Invalid expenses (null, empty, negative values)
- `ArgumentOutOfRangeException`: Margin not 0-50%

**Example**:

```csharp
var expenses = new Dictionary<string, decimal>
    ["Water"] = 5000m,
    ["Sewer"] = 3000m,
    ["Trash"] = 1500m
};

var result = await _recommendationService
    .GetRecommendedAdjustmentFactorsAsync(expenses, 15.0m);

// result.AdjustmentFactors = { Water: 1.15, Sewer: 1.17, Trash: 1.10 }
// result.Explanation = "Multi-paragraph explanation..."
// result.FromGrokApi = true
// result.ApiModelUsed = "grok-beta"
// result.Warnings = []
```

---

#### GetRecommendationExplanationAsync

Get detailed explanation for stakeholders (council, public).

```csharp
Task<string> GetRecommendationExplanationAsync(
    decimal targetProfitMargin = 15.0m,
    CancellationToken cancellationToken = default)
```

**Parameters**: Same as `GetRecommendedAdjustmentFactorsAsync`

**Returns**: Multi-paragraph professional explanation

**Example**:

```csharp
var explanation = await _recommendationService
    .GetRecommendationExplanationAsync(expenses, 15.0m);

// Use in city council presentation or public notice
await GeneratePDFReportAsync(explanation);

#### ClearCache

Manually clear cached recommendations (e.g., after rate changes).

```

**Example**:

```csharp
// After user updates expense data
await _expenseRepository.UpdateExpensesAsync(newExpenses);

// Clear cached recommendations
_recommendationService.ClearCache();

// Next call will compute fresh recommendations
var newRecommendations = await _recommendationService
    .GetRecommendedAdjustmentFactorsAsync(newExpenses, 15.0m);
```

---

## IGrokSupercomputer Interface

Comprehensive municipal data analysis and compliance reporting.
**Namespace**: `WileyWidget.Services.Abstractions`
**Implementation**: `WileyWidget.Services.GrokSupercomputer`

### IGrokSupercomputer Methods

#### FetchEnterpriseDataAsync

Aggregate all enterprise data with filtering.

````csharp
Task<ReportData> FetchEnterpriseDataAsync(
    int? enterpriseId = null,
    DateTime? startDate = null,
    DateTime? endDate = null,
    string filter = "")
 -`enterpriseId`: Specific enterprise (null = all)
- `startDate`: Filter start date (null = 12 months ago)
- `endDate`: Filter end date (null = today)
- `filter`: Text filter for departments/funds/audit entries

**Returns**: `ReportData` with aggregated information

**Example**:

```csharp
var data = await _grokSupercomputer.FetchEnterpriseDataAsync(
    enterpriseId: 123,
    startDate: new DateTime(2025, 1, 1),
    endDate: new DateTime(2025, 12, 31),
    filter: "Water"
);

// data.BudgetSummary - Budget totals
// data.VarianceAnalysis - Budget vs actual
// data.Departments - Department breakdown
// data.Funds - Fund allocations
// data.AuditEntries - Audit trail
// data.YearEndSummary - Year-end projections
// data.Enterprises - Enterprise list
````

---

#### AnalyzeBudgetDataAsync

AI-powered budget analysis with recommendations.

```csharp
Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget)
```

**Parameters**:

- `budget`: Budget data to analyze

**Returns**: `BudgetInsights` with:

- `Variances` - Budget vs actual by category
- `Projections` - End-of-year projections with confidence
- `Recommendations` - AI-powered improvement suggestions
- `HealthScore` - Overall budget health (0-100)

**Example**:

```csharp
var budgetData = await _budgetRepository.GetBudgetForYearAsync(2025);
var insights = await _grokSupercomputer.AnalyzeBudgetDataAsync(budgetData);

Console.WriteLine($"Budget Health: {insights.HealthScore}/100");

foreach (var variance in insights.Variances)
{
    Console.WriteLine($"{variance.Category}: {variance.Variance:C}");
}

foreach (var recommendation in insights.Recommendations)
{
    Console.WriteLine($"- {recommendation}");
}
```

---

#### GenerateComplianceReportAsync

Generate regulatory compliance report.

```csharp
Task<ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise)
```

**Parameters**:

- `enterprise`: Enterprise to audit

**Returns**: `ComplianceReport` with:

- `OverallStatus` - Compliant | Warning | NonCompliant | Critical
- `Violations` - List of compliance violations
- `Recommendations` - Corrective actions
- `ComplianceScore` - Score (0-100)
- `NextAuditDate` - Scheduled next audit

**Example**:

```csharp
var enterprise = await _enterpriseRepository.GetByIdAsync(123);
var report = await _grokSupercomputer.GenerateComplianceReportAsync(enterprise);

if (report.OverallStatus == ComplianceStatus.NonCompliant)
{
    foreach (var violation in report.Violations)
    {
        _logger.LogWarning(
            "Violation: {Regulation} - {Description} (Severity: {Severity})",
            violation.Regulation,
            violation.Description,
            violation.Severity
        );
    }
}
```

---

#### QueryAsync

Direct AI query for custom prompts.

```csharp
Task<string> QueryAsync(string prompt)
```

**Parameters**:

- `prompt`: Custom AI prompt

**Returns**: AI response string

**Example**:

```csharp
var prompt = @"
Analyze this municipal account data and identify potential issues:
- Account 101-Water: $5,000/month
- Account 102-Sewer: $3,000/month
- Account 103-Trash: -$500/month (negative!)

Focus on the negative value and recommend corrective actions.
";

var analysis = await _grokSupercomputer.QueryAsync(prompt);
```

---

## Data Models

### AIResponseResult

```csharp
public record AIResponseResult(
    string Content,
    int HttpStatusCode = 200,
    string? ErrorCode = null,
    string? RawErrorBody = null
);
```

**Properties**:

- `Content`: AI response or error message
- `HttpStatusCode`: HTTP status (200, 403, 429, 500, etc.)
- `ErrorCode`: Machine-readable code ("RateLimited", "AuthFailure", etc.)
- `RawErrorBody`: Full API error response

---

### RecommendationResult

```csharp
public record RecommendationResult(
    Dictionary<string, decimal> AdjustmentFactors,
    string Explanation,
    bool FromGrokApi,
    string ApiModelUsed,
    IEnumerable<string> Warnings
);
```

**Properties**:

- `AdjustmentFactors`: Department → adjustment factor (e.g., 1.15)
- `Explanation`: Multi-paragraph professional explanation
- `FromGrokApi`: `true` if AI was used, `false` if rule-based fallback
- `ApiModelUsed`: Model name (e.g., "grok-beta", "rule-based")
- `Warnings`: Any validation warnings (e.g., missing departments)

---

### BudgetInsights

```csharp
public class BudgetInsights
{
    public List<BudgetVariance> Variances { get; set; }
    public List<BudgetProjection> Projections { get; set; }
    public List<string> Recommendations { get; set; }
    public int HealthScore { get; set; } // 0-100
}
```

---

### ComplianceReport

```csharp
public class ComplianceReport
{
    public int EnterpriseId { get; set; }
    public DateTime GeneratedDate { get; set; }
    public ComplianceStatus OverallStatus { get; set; }
    public List<ComplianceViolation> Violations { get; set; }
    public List<string> Recommendations { get; set; }
    public int ComplianceScore { get; set; } // 0-100
    public DateTime NextAuditDate { get; set; }
}

public enum ComplianceStatus
{
    Compliant,
    Warning,
    NonCompliant,
    Critical
}
```

---

## Error Handling

### Common Error Codes

| Error Code           | HTTP Status | Meaning              | Action                             |
| -------------------- | ----------- | -------------------- | ---------------------------------- |
| `AuthFailure`        | 403         | Invalid API key      | Check configuration                |
| `RateLimited`        | 429         | Too many requests    | Wait and retry                     |
| `NetworkError`       | 0           | Network issue        | Check connectivity                 |
| `Timeout`            | 0           | Request timeout      | Increase timeout or simplify query |
| `ServerError`        | 500         | xAI API error        | Check xAI status page              |
| `CircuitBreakerOpen` | 503         | Circuit breaker open | Wait for reset (60s)               |
| `Disabled`           | 503         | AI service disabled  | Enable in configuration            |

### Error Handling Best Practices

```csharp
public async Task<string> GetInsightsWithRetryAsync(string context, string question)
{
    int maxAttempts = 3;
    int attempt = 0;

    while (attempt < maxAttempts)
    {
        try
        {
            var result = await _aiService.GetInsightsWithStatusAsync(context, question);

            if (result.HttpStatusCode == 200)
            {
                return result.Content;
            }
            else if (result.ErrorCode == "RateLimited")
            {
                // Wait and retry for rate limiting
                await Task.Delay(TimeSpan.FromSeconds(5));
                attempt++;
                continue;
            }
            else if (result.ErrorCode == "CircuitBreakerOpen")
            {
                // Circuit breaker - wait longer
                await Task.Delay(TimeSpan.FromSeconds(60));
                attempt++;
                continue;
            }
            else
            {
                // Other errors - don't retry
                throw new InvalidOperationException(
                    $"AI service error: {result.ErrorCode} - {result.Content}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service call failed (attempt {Attempt})", attempt + 1);

            if (attempt == maxAttempts - 1)
            {
                throw; // Last attempt failed
            }

            attempt++;
            await Task.Delay(TimeSpan.FromSeconds(2 * attempt)); // Exponential backoff
        }
    }

    throw new InvalidOperationException("AI service unavailable after retries");
}
```

---

## Code Examples

See [CONFIGURATION_GUIDE.md](./CONFIGURATION_GUIDE.md) and individual method documentation above for complete examples.

---

**Document Version**: 1.0
**Last Review**: 2026-01-03
**Maintainer**: WileyWidget Development Team
