# WileyWidget AI Ecosystem Architecture

**Version**: 1.0
**Last Updated**: 2026-01-03
**Status**: Active

## 📋 Table of Contents

- [WileyWidget AI Ecosystem Architecture](#wileywidget-ai-ecosystem-architecture)
  - [📋 Table of Contents](#-table-of-contents)
  - [Executive Summary](#executive-summary)
    - [Key Capabilities](#key-capabilities)
    - [Technology Stack](#technology-stack)
  - [Architecture Overview](#architecture-overview)
    - [Service Layer Diagram](#service-layer-diagram)
    - [2. Basic Configuration](#2-basic-configuration)
    - [3. Use AI Services](#3-use-ai-services)
  - [Key Features](#key-features)
    - [✅ Resilience](#-resilience)
    - [✅ Performance](#-performance)
    - [✅ Observability](#-observability)
    - [✅ Security](#-security)
  - [Additional Documentation](#additional-documentation)

---

## Executive Summary

WileyWidget implements a sophisticated AI-powered municipal utility management system built on **xAI's Grok API**. The architecture consists of four primary AI services working in concert to provide conversational AI, rate recommendations, municipal analytics, and compliance reporting.

### Key Capabilities

- **Conversational AI**: Real-time chat interface with conversation persistence
- **Rate Recommendations**: AI-driven utility rate optimization with fallback logic
- **Municipal Analytics**: Budget analysis, compliance reporting, and data insights
- **Cost Control**: Comprehensive caching, circuit breakers, and token usage tracking

### Technology Stack

- **AI Provider**: xAI Grok API (<https://api.x.ai/v1/chat/completions>s>)
- **Models**: grok-beta (fast), grok-4-0709 (latest), grok-2-latest (stable)
- **Framework**: .NET 9.0, C# 13
- **Resilience**: Polly (circuit breaker, retry policies)
- **Observability**: OpenTelemetry, SigNoz, Serilog
- **Caching**: IMemoryCache with configurable TTL

---

## Architecture Overview

### Service Layer Diagram

text
┌─────────────────────────────────────────────────────────────┐
│ PRESENTATION LAYER │
│ ┌──────────────────┐ ┌──────────────────────┐ │
│ │ ChatPanel.cs │◄───────┤ChatPanelViewModel.cs │ │
│ │ (WinForms UI) │ │ (MVVM Layer) │ │
│ └──────────────────┘ └──────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
│
▼
┌─────────────────────────────────────────────────────────────┐
│ SERVICE ABSTRACTION LAYER │
│ ┌──────────────┐ ┌──────────────────┐ ┌──────────────┐ │
│ │ IAIService │ │IGrokSupercomputer│ │IGrokRec... │ │
│ └──────────────┘ └──────────────────┘ └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
│
▼
┌─────────────────────────────────────────────────────────────┐
│ BUSINESS LOGIC LAYER │
│ ┌──────────────────┐ │
│ │ XAIService.cs │──────┐ │
│ │ (Core AI API) │ │ │
│ └──────────────────┘ │ │
│ ▼ │
│ ┌──────────────────────────────────────────────────┐ │
│ │ GrokRecommendationService.cs │ │
│ │ (Rate Recommendations + Resilience) │ │
│ └──────────────────────────────────────────────────┘ │
│ ▼ │
│ ┌──────────────────────────────────────────────────┐ │
│ │ GrokSupercomputer.cs │ │
│ │ (Municipal Analytics + Compliance) │ │
│ └──────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
│
▼
┌─────────────────────────────────────────────────────────────┐
│ EXTERNAL AI PROVIDER │
│ xAI Grok API (<https://api.x.ai/v1/>...) │
│ Models: grok-beta, grok-4-0709, grok-2-latest │
└─────────────────────────────────────────────────────────────┘

````markdown
For complete architecture details, API documentation, configuration guide, and troubleshooting,
see the full documentation in this directory.

---

## Quick Start

### 1. Configure API Key

```bash
# Development (user secrets)
dotnet user-secrets set "XAI:ApiKey" "xai-YOUR_KEY_HERE"

# Production (environment variable)
setx XAI__ApiKey "xai-YOUR_KEY_HERE"
```
````

### 2. Basic Configuration

**appsettings.json**:

```json
{
  "XAI": {
    "Enabled": true,
    "Model": "grok-beta",
    "Temperature": 0.3,
    "MaxTokens": 800,
    "TimeoutSeconds": 15
  }
}
```

### 3. Use AI Services

```csharp
// Inject and use
public class MyViewModel
{
    private readonly IAIService _aiService;

    public MyViewModel(IAIService aiService)
    {
        _aiService = aiService;
    }

    public async Task GetInsightsAsync()
    {
        var result = await _aiService.GetInsightsAsync(
            "Budget Analysis",
            "What are our top spending departments?"
        );
        return result;
    }
}
```

---

## Key Features

### ✅ Resilience

- Circuit breaker pattern (auto-recovery)
- Retry policies with exponential backoff
- Concurrency limiting (prevent rate limits)
- Intelligent fallback strategies

### ✅ Performance

- Multi-layer caching (5min-2hr TTL)
- Batch request processing
- Adaptive timeouts

- Connection pooling

### ✅ Observability

- OpenTelemetry metrics export
- Structured logging (Serilog)

- Cost tracking (token usage)
- Health check endpoints

### ✅ Security

- Input sanitization & validation
- API key rotation support
- Azure Key Vault integration
- Secure credential storage

---

## Additional Documentation

| Document                                        | Description                 |
| ----------------------------------------------- | --------------------------- |
| [Configuration Guide](./CONFIGURATION_GUIDE.md) | Detailed setup and tuning   |
| [API Reference](./API_REFERENCE.md)             | Complete API documentation  |
| [Performance Guide](./PERFORMANCE_GUIDE.md)     | Optimization techniques     |
| [Troubleshooting](./TROUBLESHOOTING.md)         | Common issues and solutions |
| [Metrics Dashboard](./METRICS_DASHBOARD.md)     | Grafana/SigNoz setup        |

---

**Document Version**: 1.0
**Last Review**: 2026-01-03
**Next Review**: 2026-04-03
**Maintainer**: WileyWidget Development Team
