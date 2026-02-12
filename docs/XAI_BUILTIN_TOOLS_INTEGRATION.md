# xAI Built-in Tools Integration Guide

**Last Updated:** 2026-02-10
**Status:** ✅ Implemented
**Reference:** https://docs.x.ai/developers/tools/overview

---

## Overview

This document describes the implementation of xAI's built-in server-side tools for the Wiley Widget JARVIS assistant. These tools extend Grok's capabilities beyond text generation to include real-time web search, code execution, social sentiment analysis, and document querying.

## What Was Implemented

### 1. Built-in Tools Infrastructure

**File:** [`src/WileyWidget.WinForms/Services/AI/XAI/XAIBuiltInTools.cs`](../src/WileyWidget.WinForms/Services/AI/XAI/XAIBuiltInTools.cs)

A comprehensive infrastructure for managing xAI built-in tools:

- **Tool configuration classes** for each tool type (WebSearch, XSearch, CodeExecution, CollectionsSearch)
- **Tool definition generation** in OpenAI-compatible format (xAI uses OpenAI function calling format)
- **Configuration validation** to ensure compliance with xAI API limits
- **Default configuration factory** optimized for municipal finance use cases

### 2. Configuration Integration

**File:** [`src/WileyWidget.WinForms/appsettings.json`](../src/WileyWidget.WinForms/appsettings.json)

Added comprehensive tools configuration under `XAI:Tools` section:

```json
"XAI": {
  "Tools": {
    "Enabled": true,
    "WebSearch": {
      "Enabled": true,
      "EnableImageUnderstanding": false,
      "AllowedDomains": [],
      "ExcludedDomains": []
    },
    "CodeExecution": {
      "Enabled": true,
      "TimeoutSeconds": 60
    },
    "XSearch": {
      "Enabled": false,
      "EnableImageUnderstanding": false
    },
    "CollectionsSearch": {
      "Enabled": false,
      "CollectionIds": []
    }
  }
}
```

### 3. GrokAgentService Integration

**File:** [`src/WileyWidget.WinForms/Services/AI/GrokAgentService.cs`](../src/WileyWidget.WinForms/Services/AI/GrokAgentService.cs)

Updated the Grok service to:

- Load tool configuration from appsettings on startup
- Validate tool configuration and log enabled tools
- Inject tool definitions into all API requests (via `CreateResponsesPayload`)
- Support tool calls in both streaming and non-streaming modes

**Key changes:**

1. Added `_toolConfiguration` field to store loaded tools
2. Added `LoadToolConfiguration` static method to parse configuration
3. Updated `CreateResponsesPayload` to include tools in API requests
4. Added logging for tool invocations

---

## Available Tools

### 1. Web Search (`web_search`)

**Purpose:** Real-time web search and page browsing for up-to-date information

**Municipal Finance Use Cases:**

- Latest GASB/FASB regulatory changes
- Current municipal bond yields
- State/federal legislation updates
- Inflation data and economic indicators

**Configuration:**

```json
"WebSearch": {
  "Enabled": true,
  "AllowedDomains": ["gasb.org", "msrb.org"],  // Optional: restrict to specific domains
  "ExcludedDomains": [],                        // Optional: exclude domains
  "EnableImageUnderstanding": false              // Optional: analyze images in results
}
```

**Example Prompt:**

> "JARVIS, what are the current AAA-rated municipal bond yields for 10-year maturities?"

**xAI Response Flow:**

1. Grok invokes `web_search` tool to find current bond yields
2. Browses financial sites (e.g., Municipal Market Data, Bloomberg)
3. Returns structured answer with citations

---

### 2. Code Execution (`code_interpreter`)

**Purpose:** Execute Python code in a secure sandbox with pandas, numpy, scipy, matplotlib, sympy

**Municipal Finance Use Cases:**

- Financial ratio calculations (debt service coverage, liquidity ratios)
- Statistical forecasting (ARIMA models, Monte Carlo simulations)
- Cash flow projections and scenario modeling
- Data analysis and visualization

**Configuration:**

```json
"CodeExecution": {
  "Enabled": true,
  "TimeoutSeconds": 60  // Adjust for complex calculations
}
```

**Example Prompts:**

**Ratio Calculation:**

> "JARVIS, calculate the debt service coverage ratio for Electric Enterprise: Operating Income $2.5M, Debt Service $800K"

**Forecasting:**

> "JARVIS, forecast Water Enterprise revenue for Q1-Q4 2027 using ARIMA on the last 3 years of data: [historical_data]"

**xAI Response Flow:**

1. Grok generates Python code (e.g., pandas DataFrame operations)
2. Code executes in xAI's secure sandbox
3. Returns results with reasoning

---

### 3. X Search (`x_search`)

**Purpose:** Search X (Twitter) for posts, users, threads, and sentiment

**Municipal Finance Use Cases:**

- Public sentiment on utility rate increases
- Council meeting discourse analysis
- Taxpayer feedback and concerns

**Configuration:**

```json
"XSearch": {
  "Enabled": false,  // Enable when needed for sentiment analysis
  "EnableImageUnderstanding": false
}
```

**Example Prompt:**

> "JARVIS, what's the public sentiment on the proposed 15% water rate increase in Town of Wiley?"

**Note:** Disabled by default; enable when active social listening is required.

---

### 4. Collections Search (`collections_search`)

**Purpose:** Query uploaded knowledge base documents (PDFs, audit reports, compliance docs)

**Municipal Finance Use Cases:**

- Query historical audit reports
- Search compliance guidelines
- Extract key figures from budget PDFs

**Configuration:**

```json
"CollectionsSearch": {
  "Enabled": false,  // Requires document upload to xAI console first
  "CollectionIds": ["audit-reports-2024", "compliance-docs"]
}
```

**Setup Required:**

1. Upload documents to xAI Console: https://console.x.ai/
2. Copy collection IDs
3. Add to configuration and enable

**Example Prompt:**

> "JARVIS, summarize findings from the 2023 comprehensive annual financial report"

---

## How It Works

### Request Flow

1. **User sends message** to JARVIS via UI (e.g., JARVISChatUserControl)
2. **GrokAgentService constructs request:**
   - Loads tool configuration from `_toolConfiguration`
   - Calls `CreateResponsesPayload` which injects tools via `XAIBuiltInTools.CreateToolDefinitions`
   - Payload includes `tools` array with function definitions
3. **xAI API receives request:**
   - Grok analyzes the query
   - Decides whether to call tools (web_search, code_interpreter, etc.)
   - **Built-in tools execute server-side** (no callback needed)
4. **xAI API returns response:**
   - Includes `tool_calls` metadata showing what tools were used
   - Includes `citations` for web/X search results
   - Returns final answer synthesized from tool results
5. **GrokAgentService streams response** to UI with citations

### Tool Call Logging

All tool invocations are logged:

```csharp
_logger?.LogInformation("[XAI] Added {Count} built-in tools to request: {Tools}",
    tools.Count,
    string.Join(", ", tools.Select(t => t["function"]["name"])));
```

Example log output:

```
[XAI] Added 2 built-in tools to request: web_search, code_interpreter
```

---

## Configuration Options

### Global Enable/Disable

```json
"XAI": {
  "Tools": {
    "Enabled": false  // Disables ALL tools
  }
}
```

### Per-Tool Configuration

Each tool can be individually enabled/disabled:

```json
"WebSearch": { "Enabled": true },
"CodeExecution": { "Enabled": true },
"XSearch": { "Enabled": false },
"CollectionsSearch": { "Enabled": false }
```

### Advanced Options

**Web Search:**

- `AllowedDomains`: Restrict search to specific domains (max 5)
- `ExcludedDomains`: Block specific domains from results (max 5)
- `EnableImageUnderstanding`: Allow Grok to analyze images in search results

**Code Execution:**

- `TimeoutSeconds`: Max execution time for Python code (1-300 seconds)

**Collections Search:**

- `CollectionIds`: Specific document collections to search

---

## Validation

Configuration is validated on startup:

- **Mutual exclusivity:** Cannot set both `AllowedDomains` and `ExcludedDomains`
- **Domain limits:** Max 5 domains per list
- **Timeout limits:** Code execution timeout must be 1-300 seconds

Validation errors are logged as warnings and disable the tool config.

---

## Testing

### Manual Test (via JARVIS Chat)

1. Enable tools in `appsettings.json` (Web Search + Code Execution recommended)
2. Launch application
3. Open JARVIS chat panel
4. Test prompts:

**Web Search Test:**

> "JARVIS, what are the current 10-year municipal bond yields?"

**Code Execution Test:**

> "JARVIS, calculate the IRR for a bond with 5% coupon, 10-year maturity, and $950 price"

**Combined Test:**

> "JARVIS, find the latest GASB standards on pension reporting and calculate our compliance ratio"

### Integration Test

**File:** [`tests/WileyWidget.WinForms.Tests/Integration/RealAPI/GrokAgentServiceRealApiTests.cs`](../tests/WileyWidget.WinForms.Tests/Integration/RealAPI/GrokAgentServiceRealApiTests.cs)

Add test methods:

```csharp
[Fact(Skip = "Requires real xAI API key and network access")]
public async Task RunAgentAsync_WebSearchTool_ReturnsCurrentData()
{
    // Arrange
    var prompt = "What are the current 10-year municipal bond yields?";

    // Act
    var response = await _grokAgent.RunAgentAsync(prompt);

    // Assert
    Assert.NotNull(response);
    Assert.Contains("bond", response, StringComparison.OrdinalIgnoreCase);
}

[Fact(Skip = "Requires real xAI API key and network access")]
public async Task RunAgentAsync_CodeExecutionTool_CalculatesFinancialRatio()
{
    // Arrange
    var prompt = "Calculate debt service coverage ratio: operating income $2.5M, debt service $800K";

    // Act
    var response = await _grokAgent.RunAgentAsync(prompt);

    // Assert
    Assert.NotNull(response);
    Assert.Contains("3.12", response); // Expected DSCR
}
```

---

## Performance Considerations

### Token Usage

Tools consume additional tokens:

- **Tool definitions:** ~100-500 tokens per request (included in prompt)
- **Tool results:** Variable (web search: ~500-2000 tokens, code execution: ~100-1000 tokens)

**Estimate:** $0.01-0.05 per request with tools (vs. $0.001-0.01 without)

### Latency

- **Web Search:** +2-5 seconds
- **Code Execution:** +1-10 seconds (depends on complexity)
- **X Search:** +2-4 seconds
- **Collections Search:** +1-3 seconds

**Recommendation:** Use tools only when necessary; default prompts work without them.

---

## Security & Privacy

### Built-in Tools Execute on xAI Servers

- **No local execution:** All tools run in xAI's infrastructure
- **Sandboxed:** Code execution is isolated (no network access, limited file I/O)
- **Data retention:** Responses stored for 30 days (per xAI Responses API)

### Best Practices

1. **Do NOT include sensitive data** in prompts (API keys, passwords, PII)
2. **Review code execution requests** in logs before deploying to production
3. **Use Collections Search** for organization-specific documents (avoids web search leakage)

---

## Troubleshooting

### Tools Not Being Invoked

**Symptom:** Prompts that should trigger tools return generic answers

**Solutions:**

1. Check `XAI:Tools:Enabled` is `true` in appsettings
2. Verify specific tool is enabled (e.g., `WebSearch:Enabled: true`)
3. Check logs for tool injection confirmation:
   ```
   [XAI] Added 2 built-in tools to request: web_search, code_interpreter
   ```
4. Ensure API key has access to tools (some tiers may restrict features)

### Configuration Validation Errors

**Symptom:** Logs show "Tool configuration validation failed"

**Solutions:**

- Check `AllowedDomains` and `ExcludedDomains` are not both set
- Verify domain count ≤ 5 for each list
- Confirm `TimeoutSeconds` is 1-300

### Code Execution Timeout

**Symptom:** "Code execution timed out" error

**Solutions:**

- Increase `CodeExecution:TimeoutSeconds` (max 300)
- Simplify prompt (avoid asking for complex multi-step calculations in one request)

---

## Future Enhancements

### Phase 2: Finance-Specific Plugins

Once built-in tools are stable, add custom Semantic Kernel plugins:

1. **FinancialRatioCalculator** - Pre-built ratio formulas (leverage code_execution for complex ones)
2. **CashFlowForecaster** - ARIMA/Monte Carlo wrapper (calls code_execution with templates)
3. **RegulatoryComplianceChecker** - Combines web_search + collections_search
4. **ScenarioModeler** - Multi-scenario what-if analysis

### Phase 3: Tool Call Observability

- Add telemetry for tool invocation frequency
- Track token usage per tool
- Dashboard for tool usage patterns

---

## References

- **xAI Docs:** https://docs.x.ai/developers/tools/overview
- **Function Calling:** https://docs.x.ai/developers/tools/function-calling
- **Web Search:** https://docs.x.ai/developers/tools/web-search
- **Code Execution:** https://docs.x.ai/developers/tools/code-execution
- **X Search:** https://docs.x.ai/developers/tools/x-search
- **Collections Search:** https://docs.x.ai/developers/tools/collections-search

---

## Quick Start Checklist

- [x] Infrastructure created (`XAIBuiltInTools.cs`)
- [x] Configuration added to `appsettings.json`
- [x] `GrokAgentService` updated to load and inject tools
- [x] Default tools enabled: Web Search + Code Execution
- [ ] **Test with real API:** Run manual tests in JARVIS chat
- [ ] **Upload documents:** For Collections Search (optional)
- [ ] **Add integration tests:** Expand `GrokAgentServiceRealApiTests`
- [ ] **Monitor usage:** Check logs for tool invocation patterns

---

**Implementation Complete:** ✅ All Phase 1 tasks finished
**Next Step:** Test with real prompts and monitor tool usage
