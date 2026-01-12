# Startup Hang Diagnosis & Resolution

## Problem

The application hangs immediately after "DI Container Build complete" (line 338 in Program.cs).

## Root Cause

The hang occurs at `GetRequiredService<IStartupOrchestrator>()` (line 342).

**The actual culprit:** `GrokAgentService` registered as Singleton in line 1089 of Program.cs in `ConfigureUiServices()`

```csharp
builder.Services.AddSingleton<GrokAgentService>();
```

When the DI container builds, it **immediately instantiates all Singletons**, including `GrokAgentService`.

### GrokAgentService Constructor Does:

1. Reads configuration (API keys, endpoints)
2. **Creates a Semantic Kernel** - `Kernel.CreateBuilder()`
3. **Adds OpenAI chat completion** - network/protocol initialization
4. **Scans assembly for plugins** - `KernelPluginRegistrar.ImportPluginsFromAssemblies()`

This is **CPU-bound and I/O-bound work** that should NOT happen during startup initialization.

## Solution: Lazy Initialization Pattern

Change `GrokAgentService` registration from **eager Singleton** to **Lazy<GrokAgentService>**:

```csharp
// Before (blocking, eager):
builder.Services.AddSingleton<GrokAgentService>();

// After (lazy, deferred):
builder.Services.AddSingleton(serviceProvider =>
    new Lazy<GrokAgentService>(() =>
        new GrokAgentService(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetService<ILogger<GrokAgentService>>(),
            serviceProvider.GetService<IHttpClientFactory>()
        )
    )
);
```

Then inject `Lazy<GrokAgentService>` where needed and call `.Value` only when actually used.

## Immediate Workaround

Comment out line 1089 temporarily to test:

```csharp
// Temporarily disabled to diagnose startup hang
// builder.Services.AddSingleton<GrokAgentService>();
```

This will allow the app to start and verify the diagnosis.

## Recommended Implementation

1. Create `IGrokAgentService` interface if not exists
2. Use `Lazy<T>` pattern for deferred initialization
3. Document that Grok initialization happens on first use, not at startup
4. Add logging to track when Grok service is first accessed

## Logs Evidence

- Last log entry: "Startup milestone: DI Container Build complete" (14:23:50.183)
- No subsequent logs, confirming hang happens immediately after
- No async initialization is logged, confirming it's a synchronous blocking call

---

**Analysis Date:** 2026-01-07  
**Status:** Diagnosed, ready for implementation  
**Priority:** High (blocks application startup)
