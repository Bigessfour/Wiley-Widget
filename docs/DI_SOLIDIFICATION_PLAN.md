# Wiley Widget DI Solidification Plan

**Deep Evaluation Against Microsoft DI Best Practices**
**Date:** January 17, 2026

---

## Executive Summary

Your DI setup has **solid foundational practices** (clear lifetime management, good documentation) but exhibits **4 critical fragility patterns** that violate Microsoft DI best practices:

1. **Scope violation trap**: Singletons holding scoped dependencies (indirect scope injection)
2. **Constructor ambiguity risk**: Multiple constructors without clear resolution guidance
3. **Manual provider orchestration**: Bypassing framework-provided hosting patterns (WinForms-specific)
4. **Validation system mismatch**: DI validator checking concrete types instead of interface registrations

This plan provides **11 actionable recommendations** organized by risk level.

---

## PART 1: CRITICAL ISSUES (Must Fix Immediately)

### Issue #1: Singleton Services Holding Scoped Dependencies

**Severity:** CRITICAL | **Microsoft Guidance:** Violated

#### The Problem

Your code registers singletons that depend on scoped services, creating a **scope leakage anti-pattern**:

```csharp
// ❌ VIOLATION - Singletons with scoped dependencies
services.AddSingleton<IWileyWidgetContextService, WileyWidgetContextService>();  // Line 251
services.AddSingleton<IAILoggingService, AILoggingService>();                     // Line 259
services.AddSingleton<IReportExportService, ReportExportService>();               // Line 280
services.AddSingleton<IDataAnonymizerService, DataAnonymizerService>();           // Line 291
services.AddSingleton<IGrokSupercomputer, GrokSupercomputer>();                   // Line 297
```

**Why it breaks:** If these implementations depend on `DbContext`, repositories, or other scoped services (via constructor injection), they'll:

- Capture a scoped instance at startup (first resolution)
- Reuse that same instance for the entire app lifetime
- Lead to `ObjectDisposedException` when the app tries to dispose the scope
- Cause stale data issues (repository never sees fresh data)

**Evidence from Code:**

- Line 258-259: `AILoggingService` is Singleton
- Line 280: `ReportExportService` is Singleton
- Line 291: `DataAnonymizerService` is Singleton
- If any of these have constructor params like `IRepository`, `AppDbContext`, or `IDashboardService` (Transient) → **scope violation**

**Microsoft Doctrine (from docs):**

> "Do **not** resolve a scoped service directly from a singleton using constructor injection. Doing so causes the scoped service to behave like a singleton, which can lead to incorrect state when processing subsequent requests."

---

### Issue #2: Ambiguous Constructor Resolution Path

**Severity:** HIGH | **Microsoft Guidance:** Violated

#### Understanding Ambiguous Constructor Resolution

Services with multiple constructors can cause **ambiguous resolution** if the DI container can't determine which constructor to use:

```csharp
public class Example
{
    public Example() { }  // Parameterless
    public Example(ILogger<Example> logger) { }  // Resolvable
    public Example(IOptions<AppOptions> options) { }  // Also resolvable
    // DI throws exception: "ambiguous constructors"
}
```

**Your Risk:**

- Line 329: `GrokAgentService` has a manual factory: `ActivatorUtilities.CreateInstance<GrokAgentService>(sp)`
  - This **bypasses** constructor selection, but only for this one service
  - Other services may silently choose wrong constructors
- Panels (DashboardPanel, AccountsPanel, etc., lines 406-420) are registered as Scoped
  - If they have multiple constructors, resolver picks "most parameters resolvable"
  - May not match your intended constructor

**Microsoft Doctrine:**

> "When a type defines more than one constructor, the service provider selects the constructor with the most parameters where the types are DI-resolvable. If there's ambiguity when discovering constructors, an exception is thrown."

**Recommendation:**

- Ensure each service class has **exactly one public constructor** or **one primary + parameterless fallback**
- Avoid multiple parameterized constructors with overlapping resolvable types

---

### Issue #3: Manual Host Builder Duplication (Fragility Vector)

**Severity:** HIGH | **Microsoft Guidance:** Non-optimal

#### Duplicate IStartupOrchestrator Registration

You're explicitly registering `IStartupOrchestrator` twice:

```csharp
// DependencyInjection.cs, line 215-216 (ConfigureServicesInternal):
services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();
services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();  // DUPLICATE

// Program.cs, line 130 (CreateHostBuilder):
services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();  // TRIPLE
```

**Why this breaks:**

- The last registration **wins** (overwrites previous)
- If registration order changes, behavior changes silently
- Creates maintenance burden: changes in one place don't sync to others
- Violates DRY (Don't Repeat Yourself)

**Evidence:**
The comment on line 128 admits the fragility:

> "CRITICAL: Register the startup orchestrator explicitly here... (DependencyInjection.AddWinFormsServices registers it, but we ensure explicit availability...)"

This is a **code smell** indicating architectural uncertainty.

---

### Issue #4: ValidateOnBuild with Unregistered UI Components

**Severity:** MEDIUM | **Microsoft Guidance:** Violated

#### Build Validation Conflicts with UI Components

Line 52-55 uses `ValidateOnBuild = true`:

```csharp
return services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true  // Strict validation
});
```

But comments (lines 339-347) explicitly note that some services **cannot be validated** at build time:

```csharp
// DO NOT register them here to avoid build-time DI validation.
```

**Fragility:** If someone registers `FloatingPanelManager` or similar UI-dependent service, the whole build fails.

---

## PART 2: MEDIUM-SEVERITY ANTI-PATTERNS

### Issue #5: Conditional Registration with .Any() Checks (Fragility)

**Severity:** MEDIUM | **Pattern:** Defensive Programming (Problematic)

#### Implicit Registration with Conditional Checks

Lines 73-75, 165-166, etc. use:

```csharp
if (!services.Any(sd => sd.ServiceType == typeof(IConfiguration)))
{
    // Register only if not already registered
}
```

**Why this weakens DI:**

- Registration becomes **implicit** and **order-dependent**
- Difficult to debug which registration "wins"
- Creates **silent failures**: If someone registers IConfiguration earlier, your default is silently ignored
- Violates **explicit is better than implicit** principle

**Microsoft Guidance:**
Use `TryAdd*` extension methods instead of `.Any()` checks:

```csharp
// ✅ Cleaner, explicit intent
services.TryAddSingleton<IConfiguration>(configuration);
services.TryAddScoped<AppDbContext>(sp => ...);
```

**Your problematic patterns:**

- Line 73-75: `IConfiguration`
- Line 197-201: `IStartupTimelineService`
- Line 206-209: `IStartupOrchestrator` (registered unconditionally THEN checked)
- Line 313-317: `GrokAgentService`

---

### Issue #6: Generic Host Pattern Incompleteness (WinForms-Specific)

**Severity:** MEDIUM | **Architecture:** Non-standard

#### Synchronous Blocking on Async Initialization

You're using `Host.CreateDefaultBuilder()` (correct) but then **manually synchronously blocking** on async code:

```csharp
// Program.cs, lines 96-97
orchestrator.InitializeAsync().GetAwaiter().GetResult();  // Blocks UI thread
orchestrator.RunApplicationAsync(host.Services).GetAwaiter().GetResult();
```

**Issues:**

1. **Blocks UI thread** during startup → unresponsive UI during init
2. **Hides cancellation context** → can't gracefully cancel startup
3. **No startup timeout** → hangs indefinitely if service initialization deadlocks
4. **Violates async/await pattern** → breaks with async-only services

**Microsoft Pattern:**
Host.CreateDefaultBuilder supports `IHostedService` pattern (BackgroundService):

```csharp
// ✅ Better for async work
public class StartupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Runs asynchronously without blocking UI
    }
}
services.AddHostedService<StartupService>();
```

---

### Issue #7: Memory Cache Singleton with Manual Factory (Potential Leak)

**Severity:** MEDIUM | **Memory Management:** Risk

#### The Problem

Lines 125-135 create a custom MemoryCache singleton:

```csharp
services.AddSingleton<IMemoryCache>(sp =>
{
    var options = new MemoryCacheOptions { SizeLimit = 1024 };
    return new MemoryCache(options);
});
```

**Issues:**

1. **Factory lambda creates new instance every resolution** → defeats singleton purpose if called multiple times
2. **No disposal guarantee** → custom factory doesn't hook into host shutdown
3. **Size limit of 1024 is arbitrary** → no data-driven adjustment

**Better approach:**

```csharp
// ✅ Let Microsoft Extensions manage lifecycle
services.AddMemoryCache(options => options.SizeLimit = 1024);
```

---

### Issue #8: DbContextFactory Lifetime Mismatch

**Severity:** MEDIUM | **Entity Framework:** Non-standard

#### The Problem

Lines 177-179:

```csharp
services.AddDbContextFactory<AppDbContext>(
    (sp, options) => options.UseInMemoryDatabase("TestDb"),
    ServiceLifetime.Scoped  // ← DbContextFactory as Scoped
);
```

**Issue:** `DbContextFactory` should be **Singleton**:

- Factory itself is stateless → can be singleton
- Each call to `.CreateDbContext()` returns a new DbContext (scoped lifetime)
- Making factory Scoped is redundant and confusing

**Microsoft Documentation:**

> "DbContextFactory should be registered as Singleton. Each call to CreateDbContext() creates a new DbContext."

---

### Issue #9: HttpClient Registration Fragility

**Severity:** LOW-MEDIUM | **Network:** Best Practice Gap

#### The Problem

Lines 89-90:

```csharp
services.AddHttpClient();  // Generic factory
```

Then line 94-117 adds a named client:

```csharp
services.AddHttpClient("GrokClient") ...
```

**Gaps:**

1. No default client configuration (retry, timeout, etc.)
2. Code must know exact name "GrokClient" as string (no type safety)
3. No fallback if service forgets to use named client

**Better approach:**

```csharp
// ✅ Typed clients with compile-time safety
services.AddHttpClient<IGrokClient, GrokClient>()
    .SetHandlerLifetime(...)
    .AddResilienceHandler(...);
```

---

## PART 3: CODE QUALITY / MAINTAINABILITY

### Issue #10: DI Validator Concrete Type Anti-Pattern (Already Fixed ✅)

**Status:** RESOLVED (Fixed earlier today)

**Was checking:** `typeof(WileyWidget.Services.QuickBooksApiClient)` (concrete)
**Now checks:** `typeof(IQuickBooksService)` (interface)

---

### Issue #11: Comment Burden & Complexity

**Severity:** LOW | **Maintenance:** Debt

#### The Problem

- 73 lines of comments in 454-line file (16% commentary ratio)
- Comments document **why things are broken** rather than **why they're designed this way**
- Examples:
  - Line 339-347: "Do NOT register FloatingPanelManager to avoid DI validation failure"
  - Line 128: "CRITICAL: explicitly register because implicit registration might not work"
  - Line 100-105: Explanation of why custom MemoryCache was needed

**Better approach:**
Fix the underlying issues so comments aren't needed.

---

## PART 4: RECOMMENDED REMEDIATION ROADMAP

### Phase 1: IMMEDIATE (This Week)

**Goal:** Eliminate scope violations and ambiguity

#### Task 1A: Audit Singleton-Scoped Dependencies

**Action:**

1. For each Singleton service (lines 232-297), verify its constructor params:

   ```csharp
   public class AILoggingService  // Registered Singleton (line 259)
   {
       public AILoggingService(
           IRepository<T> repo,  // ← SCOPE VIOLATION!
           ILogger logger         // ← OK (framework provides)
       ) { }
   }
   ```

2. If constructor contains scoped service, **change lifetime to Scoped**:

   ```csharp
   // Change from AddSingleton to AddScoped
   services.AddScoped<IAILoggingService, AILoggingService>();
   ```

**Affected Services to Check:**

- `IWileyWidgetContextService` (line 251) → **likely Scoped**
- `IAILoggingService` (line 259) → **likely Scoped**
- `IReportExportService` (line 280) → **likely Scoped**
- `IDataAnonymizerService` (line 291) → **likely Transient**
- `IGrokSupercomputer` (line 297) → check for scoped deps → **likely Scoped**

**Rationale:** If a Singleton depends on scoped services, it must create explicit scopes via `IServiceScopeFactory`.

#### Task 1B: Remove Duplicate Registrations

**Action:**

1. Delete line 216 (duplicate `AddSingleton<IStartupOrchestrator>`)
2. Delete line 128 registration in Program.cs (let DependencyInjection.cs own it)
3. Use `TryAddSingleton` to prevent accidental duplicates

#### Task 1C: Add Startup Timeout Protection

**Action:**

```csharp
// Program.cs, replace lines 96-97
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30-second timeout
try
{
    await orchestrator.InitializeAsync().ConfigureAwait(false);  // Actually await!
    await orchestrator.RunApplicationAsync(host.Services, cts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    Log.Fatal("Startup initialization timed out after 30 seconds");
    throw;
}
```

---

### Phase 2: SHORT-TERM (Next 2 Weeks)

**Goal:** Improve resilience and reduce fragility

#### Task 2A: Replace .Any() Checks with TryAdd

**Action:**
Find and replace pattern:

```csharp
// ❌ Before
if (!services.Any(sd => sd.ServiceType == typeof(IConfiguration)))
{
    services.AddSingleton<IConfiguration>(defaultConfig);
}

// ✅ After
services.TryAddSingleton<IConfiguration>(defaultConfig);
```

**Files to update:**

- DependencyInjection.cs (3 locations)

**Benefit:** Removes implicit behavior, clarifies intent.

#### Task 2B: Add Single Fluent Registration Entry Point

**Action:**
Create a new extension method that encapsulates the entire registration:

```csharp
namespace WileyWidget.WinForms.Configuration
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all Wiley Widget services with proper lifetime management.
        /// This is the SINGLE ENTRY POINT for DI configuration.
        /// </summary>
        public static IServiceCollection AddWileyWidgetServices(
            this IServiceCollection services,
            IConfiguration configuration,
            DIOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            options ??= new();

            // Clear all previous registrations to prevent duplicates
            if (options.ClearExisting)
            {
                var typesToRemove = services
                    .Where(sd => sd.ServiceType.Namespace?.StartsWith("WileyWidget") == true)
                    .Select(sd => sd.ServiceType)
                    .ToList();

                foreach (var type in typesToRemove)
                {
                    services.RemoveAll(type);
                }
            }

            // All registrations go here
            RegisterInfrastructure(services, configuration);
            RegisterDatabase(services);
            RegisterBusinessServices(services);
            // ... etc

            return services;
        }
    }

    public class DIOptions
    {
        public bool ClearExisting { get; set; } = false;
        public bool ValidateOnBuild { get; set; } = true;
    }
}
```

**Usage in Program.cs:**

```csharp
services.AddWileyWidgetServices(hostContext.Configuration);
```

**Benefit:** Single source of truth; prevents accidental duplication.

#### Task 2C: Implement Keyed Services for Variants

**Action:**
Use .NET 8+ keyed services for multiple implementations (e.g., multiple `IExportService`):

```csharp
// Register with keys
services.AddTransient<IExcelExportService, ExcelExportService>();
services.AddKeyedTransient<IExcelExportService, CsvExportService>("csv");
services.AddKeyedTransient<IExcelExportService, JsonExportService>("json");

// Consume with [FromKeyedServices]
public class ReportExporter
{
    public ReportExporter(
        [FromKeyedServices("csv")] IExcelExportService csvExporter)
    {
        // Use csvExporter
    }
}
```

**Current misses:**

- Multiple `IMessageWriter` implementations (lines 181-182 in DependencyInjection.cs show awareness)
- Should apply to any service with multiple variants

---

### Phase 3: MEDIUM-TERM (Month 1)

**Goal:** Refactor async patterns and host setup

#### Task 3A: Move Startup to IHostedService Pattern

**Action:**

```csharp
public class StartupHostedService : BackgroundService
{
    private readonly IStartupOrchestrator _orchestrator;
    private readonly ILogger<StartupHostedService> _logger;

    public StartupHostedService(IStartupOrchestrator orchestrator, ILogger<StartupHostedService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting application initialization");
            await _orchestrator.InitializeAsync(stoppingToken).ConfigureAwait(false);
            await _orchestrator.RunApplicationAsync(Services, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Startup was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Startup failed");
            throw;
        }
    }
}

// In Program.cs
services.AddHostedService<StartupHostedService>();
```

**Benefit:**

- Non-blocking UI startup
- Proper cancellation support
- Framework-standard pattern

---

#### Task 3B: Implement Service Registration Validation Tests

**Action:**
Create unit tests in `WileyWidget.WinForms.Tests`:

```csharp
[TestClass]
public class DIRegistrationValidationTests
{
    [TestMethod]
    public void AllSingletonsWithScopedDependenciesMustCreateScopes()
    {
        var services = DependencyInjection.CreateServiceCollection();
        var provider = services.BuildServiceProvider();

        // Get all singleton registrations
        var singletons = services.Where(sd => sd.Lifetime == ServiceLifetime.Singleton);

        foreach (var singleton in singletons)
        {
            // Verify: if singleton constructor has scoped params, it must use IServiceScopeFactory
            var ctors = singleton.ImplementationType?.GetConstructors() ?? Array.Empty<ConstructorInfo>();
            foreach (var ctor in ctors)
            {
                var scopedParams = ctor.GetParameters()
                    .Where(p => IsServiceScoped(services, p.ParameterType))
                    .ToList();

                Assert.IsTrue(scopedParams.Count == 0 ||
                    ctor.GetParameters().Any(p => p.ParameterType == typeof(IServiceScopeFactory)),
                    $"{singleton.ServiceType.Name} is singleton with scoped dependencies but doesn't use IServiceScopeFactory");
            }
        }
    }

    [TestMethod]
    public void NoDuplicateServiceRegistrations()
    {
        var services = DependencyInjection.CreateServiceCollection();

        var serviceTypes = services
            .GroupBy(sd => sd.ServiceType)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.AreEqual(0, serviceTypes.Count,
            $"Duplicate registrations found: {string.Join(", ", serviceTypes.Select(g => g.Key.Name))}");
    }

    [TestMethod]
    public void AllConstructorsAreUnambiguous()
    {
        var services = DependencyInjection.CreateServiceCollection();

        foreach (var descriptor in services)
        {
            var impType = descriptor.ImplementationType ?? descriptor.ServiceType;
            var ctors = impType.GetConstructors();

            // Should have 0 (resolve via factory) or 1 public constructor
            Assert.IsTrue(ctors.Length <= 1,
                $"{impType.Name} has {ctors.Length} public constructors (should be ≤ 1)");
        }
    }
}
```

**Benefit:** Catches fragility issues at test time, not runtime.

---

#### Task 3C: Implement Service Lifetime Governance

**Action:**
Create an attribute and analyzer to enforce lifetime rules:

```csharp
/// <summary>
/// Specifies the required lifetime for a service.
/// The DI container validates this at build time.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class ServiceLifetimeAttribute : Attribute
{
    public ServiceLifetime Lifetime { get; }
    public string Reason { get; }

    public ServiceLifetimeAttribute(ServiceLifetime lifetime, string reason)
    {
        Lifetime = lifetime;
        Reason = reason;
    }
}

// Usage
[ServiceLifetime(ServiceLifetime.Scoped, "Holds repository state; must be per-request")]
public class WileyWidgetContextService : IWileyWidgetContextService { }

[ServiceLifetime(ServiceLifetime.Singleton, "Stateless; thread-safe")]
public class ThemeService : IThemeService { }

[ServiceLifetime(ServiceLifetime.Transient, "I/O operations; should not be cached")]
public class ExcelExportService : IExcelExportService { }
```

Then create Roslyn analyzer to verify registrations match attributes.

---

### Phase 4: LONG-TERM (Month 2+)

**Goal:** Architectural stability and best practices

#### Task 4A: Implement DI Validation as Build-Time Analyzer

Instead of runtime `WinFormsDiValidator`, create Roslyn analyzer that:

1. Detects scope violations at compile time
2. Warns about ambiguous constructors
3. Validates lifetime consistency
4. Reports unused registrations

#### Task 4B: Create DI Registration Documentation

**Action:**
Generate auto-documentation of service graph:

```csharp
public class DIDocumentationGenerator
{
    public static string GenerateMarkdown(IServiceCollection services)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Dependency Injection Service Graph");
        sb.AppendLine();

        var byLifetime = services.GroupBy(sd => sd.Lifetime);
        foreach (var group in byLifetime)
        {
            sb.AppendLine($"## {group.Key} Services ({group.Count()})");
            sb.AppendLine();

            foreach (var descriptor in group.OrderBy(d => d.ServiceType.Name))
            {
                sb.AppendLine($"- **{descriptor.ServiceType.Name}** → {descriptor.ImplementationType?.Name ?? "factory"}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
```

Run this during CI/CD to generate [docs/DI_SERVICE_GRAPH.md](docs/DI_SERVICE_GRAPH.md).

---

## SUMMARY TABLE: Risk Remediation

| Issue                          | Severity | Status       | Phase | Owner   | Est. Time |
| ------------------------------ | -------- | ------------ | ----- | ------- | --------- |
| Singleton-scoped dependencies  | CRITICAL | Not Fixed    | 1     | Backend | 2 hrs     |
| Duplicate IStartupOrchestrator | CRITICAL | Not Fixed    | 1     | DevOps  | 30 min    |
| Ambiguous constructors         | HIGH     | Not Fixed    | 1     | Backend | 1 hr      |
| .Any() checks → TryAdd         | MEDIUM   | Not Fixed    | 2     | DevOps  | 1 hr      |
| DI validator concrete types    | MEDIUM   | **✅ FIXED** | -     | -       | -         |
| HttpClient registration        | LOW      | Not Fixed    | 2     | Backend | 1 hr      |
| MemoryCache custom factory     | MEDIUM   | Not Fixed    | 2     | Backend | 1 hr      |
| DbContextFactory lifetime      | MEDIUM   | Not Fixed    | 2     | Backend | 30 min    |
| Async blocking pattern         | HIGH     | Not Fixed    | 3     | Backend | 2 hrs     |
| Startup timeout protection     | HIGH     | Not Fixed    | 1     | DevOps  | 30 min    |

---

## Testing Strategy

### Unit Tests (Phase 2)

```
WileyWidget.WinForms.Tests/
├── DIRegistrationValidationTests.cs      (scope violations, duplicates)
├── ConstructorAmbiguityTests.cs          (multiple constructors)
└── ServiceLifetimeConsistencyTests.cs    (lifetime graph validation)
```

### Integration Tests (Phase 3)

```
WileyWidget.WinForms.Tests/
├── DIContainerBootstrapTests.cs          (host builder, service provider creation)
└── ServiceResolutionTests.cs             (validate all services resolve without error)
```

### Performance Tests (Phase 4)

```
WileyWidget.WinForms.Tests/
├── ServiceResolutionPerformanceTests.cs  (measure resolution time for 100+ iterations)
└── MemoryLeakTests.cs                    (GC.Collect, verify scope disposal)
```

---

## Configuration Management

Add to `appsettings.json`:

```json
{
  "DependencyInjection": {
    "ValidateOnBuild": true,
    "ValidateScopes": true,
    "StartupTimeoutSeconds": 30,
    "MemoryCacheSizeLimit": 1024,
    "ServiceResolutionLogging": false
  }
}
```

---

## Governance Going Forward

**Rule: "Every new service registration must include a comment explaining its lifetime choice and why."**

```csharp
// ✅ Good
services.AddScoped<IBudgetService, BudgetService>();  // Scoped: depends on DbContext (line 245)

// ❌ Bad
services.AddSingleton<IFooService, FooService>();  // No explanation
```

**Rule: "Before committing DI changes, run DI validation tests."**

```powershell
dotnet test --filter "Category=DIValidation"
```

---

## References

- [Microsoft: .NET Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Microsoft: DI Guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
- [Microsoft: Scope Validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#scope-validation)
- [OWASP: Service Locator Anti-Pattern](https://martinfowler.com/articles/injection.html)

---

**Status:** Ready for Implementation
**Last Updated:** 2026-01-17
