# DI Solidification - Implementation Checklist

## Phase 1: IMMEDIATE (Critical Fixes)

### 1A: Audit & Fix Singleton-Scoped Dependencies

- [ ] **Task:** Identify which Singletons have scoped constructor parameters
  ```csharp
  // Check these services:
  services.AddSingleton<IAILoggingService, AILoggingService>();           // Line 259
  services.AddSingleton<IReportExportService, ReportExportService>();     // Line 280
  services.AddSingleton<IWileyWidgetContextService, WileyWidgetContextService>(); // Line 251
  services.AddSingleton<IGrokSupercomputer, GrokSupercomputer>();         // Line 297
  ```
- [ ] **Task:** For each affected service, check constructor:
  - Open implementation class
  - Look for constructor params that are repositories, DbContext, or other scoped services
  - If found: **Change registration to AddScoped or AddTransient**
- [ ] **Verify:** Build succeeds
- [ ] **Verify:** DI validation passes

**Expected Changes:**

- `IWileyWidgetContextService` → Scoped (already is, line 251 ✓)
- `IAILoggingService` → Check constructor parameters
- `IReportExportService` → Check constructor parameters
- `IGrokSupercomputer` → Check constructor parameters (line 297, currently Scoped ✓)

---

### 1B: Remove Duplicate Registrations

- [ ] Delete line 216 in DependencyInjection.cs (duplicate `AddSingleton<IStartupOrchestrator>`)
  ```csharp
  // REMOVE THIS LINE:
  services.AddSingleton<IStartupOrchestrator, StartupOrchestrator>();
  ```
- [ ] Verify only ONE registration of IStartupOrchestrator remains (line 215)
- [ ] Check if Program.cs line 130 can be removed (it duplicates DependencyInjection registration)
  - Option 1: Remove from Program.cs, let DependencyInjection.cs own it
  - Option 2: Remove from DependencyInjection.cs, keep only in Program.cs
  - **Recommend:** Option 2 - keep in Program.cs, mark DependencyInjection comment
- [ ] Build and verify

---

### 1C: Add Startup Timeout Protection

- [ ] Open [Program.cs](src/WileyWidget.WinForms/Program.cs), lines 96-97
- [ ] Replace:
  ```csharp
  // OLD (lines 96-97):
  orchestrator.InitializeAsync().GetAwaiter().GetResult();
  orchestrator.RunApplicationAsync(host.Services).GetAwaiter().GetResult();
  ```
  With:
  ```csharp
  // NEW:
  using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
  try
  {
      orchestrator.InitializeAsync().GetAwaiter().GetResult();
      orchestrator.RunApplicationAsync(host.Services).GetAwaiter().GetResult();
  }
  catch (OperationCanceledException ex)
  {
      Log.Fatal("Startup initialization timed out after 30 seconds");
      throw;
  }
  ```
- [ ] Verify `StartupOrchestrator.InitializeAsync()` and `RunApplicationAsync()` support cancellation tokens
  - If they don't: update signatures to accept `CancellationToken stoppingToken`
- [ ] Test by simulating slow initialization (add `await Task.Delay(35000)` temporarily)
- [ ] Verify timeout fires after 30s

---

## Phase 2: SHORT-TERM (Next 2 Weeks)

### 2A: Replace .Any() Checks with TryAdd

- [ ] Find all `.Any(sd => sd.ServiceType == typeof(...))` patterns

  ```powershell
  # PowerShell command to find them:
  Get-Content src/WileyWidget.WinForms/Configuration/DependencyInjection.cs | Select-String -Pattern "\.Any\(sd =>"
  ```

  **Expected locations:**
  - Line 73-75: `IConfiguration`
  - Line 197-201: `IStartupTimelineService`
  - Line 313-317: `GrokAgentService`

- [ ] Replace each with `TryAdd*` method:

  ```csharp
  // OLD:
  if (!services.Any(sd => sd.ServiceType == typeof(IConfiguration)))
  {
      services.AddSingleton<IConfiguration>(configuration);
  }

  // NEW:
  services.TryAddSingleton<IConfiguration>(configuration);
  ```

- [ ] For conditional logic that's NOT about duplicate prevention, add comment explaining why:

  ```csharp
  // Example: if initialization mode is development-only
  if (isDevelopment)
  {
      services.AddScoped<IDebugService, DebugService>();
  }
  ```

- [ ] Build and verify all DI validations pass

---

### 2B: Create Single Fluent Registration Entry Point

- [ ] Create new file: [src/WileyWidget.WinForms/Configuration/ServiceCollectionExtensions.cs](src/WileyWidget.WinForms/Configuration/ServiceCollectionExtensions.cs)

  Include:

  ```csharp
  public static IServiceCollection AddWileyWidgetServices(
      this IServiceCollection services,
      IConfiguration configuration,
      DIOptions? options = null)
  {
      // Delegate to DependencyInjection.ConfigureServicesInternal()
      // or refactor ConfigureServicesInternal to be public + static
  }

  public class DIOptions
  {
      public bool ValidateOnBuild { get; set; } = true;
      public bool ValidateScopes { get; set; } = true;
  }
  ```

- [ ] Update [Program.cs](src/WileyWidget.WinForms/Program.cs) line 130:

  ```csharp
  // OLD:
  services.AddWinFormsServices(hostContext.Configuration);

  // NEW:
  services.AddWileyWidgetServices(hostContext.Configuration);
  ```

- [ ] Remove duplicate registrations from Program.cs (no more AddSingleton<IStartupOrchestrator> there)

- [ ] Build and verify

---

### 2C: Implement Keyed Services for Multiple Implementations

- [ ] Identify services with multiple implementations
  - `IMessageWriter` (currently line 181-182 has multiple registrations)
  - `IExcelExportService` / variants
  - `IAIService` (XAIService, potentially others)

- [ ] For each, convert to keyed services:

  ```csharp
  // OLD:
  services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
  services.AddSingleton<IMessageWriter, LoggingMessageWriter>();

  // NEW:
  services.AddKeyedSingleton<IMessageWriter, ConsoleMessageWriter>("console");
  services.AddKeyedSingleton<IMessageWriter, LoggingMessageWriter>("logging");
  ```

- [ ] Update consumers to use [FromKeyedServices]:

  ```csharp
  public class ReportService
  {
      public ReportService([FromKeyedServices("logging")] IMessageWriter writer)
      {
          // Use keyed service
      }
  }
  ```

- [ ] Build and verify

---

## Phase 3: MEDIUM-TERM (Month 1)

### 3A: Move Startup to IHostedService Pattern

- [ ] Create [src/WileyWidget.WinForms/Services/StartupHostedService.cs](src/WileyWidget.WinForms/Services/StartupHostedService.cs)

  ```csharp
  public class StartupHostedService : BackgroundService
  {
      private readonly IStartupOrchestrator _orchestrator;
      private readonly ILogger<StartupHostedService> _logger;

      public StartupHostedService(
          IStartupOrchestrator orchestrator,
          ILogger<StartupHostedService> logger)
      {
          _orchestrator = orchestrator;
          _logger = logger;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          try
          {
              _logger.LogInformation("Starting application initialization");
              await _orchestrator.InitializeAsync(stoppingToken);
              await _orchestrator.RunApplicationAsync(Services, stoppingToken);
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
  ```

- [ ] Update [DependencyInjection.cs](src/WileyWidget.WinForms/Configuration/DependencyInjection.cs):

  ```csharp
  services.AddHostedService<StartupHostedService>();
  ```

- [ ] Update [Program.cs](src/WileyWidget.WinForms/Program.cs) to remove the blocking calls:

  ```csharp
  // REMOVE these lines:
  orchestrator.InitializeAsync().GetAwaiter().GetResult();
  orchestrator.RunApplicationAsync(host.Services).GetAwaiter().GetResult();

  // INSTEAD: Let BackgroundService run them
  // hosted.RunAsync() will execute when host starts
  ```

- [ ] Ensure `IStartupOrchestrator` methods accept `CancellationToken` parameters

- [ ] Build and test startup behavior

---

### 3B: Implement Service Registration Validation Tests

- [ ] Create test file: [tests/WileyWidget.WinForms.Tests/DIRegistrationValidationTests.cs](tests/WileyWidget.WinForms.Tests/DIRegistrationValidationTests.cs)

  **Tests to include:**

  ```csharp
  [TestClass]
  public class DIRegistrationValidationTests
  {
      [TestMethod]
      public void AllSingletonsWithScopedDependenciesMustCreateScopes()
      {
          // Check that singletons don't directly inject scoped services
      }

      [TestMethod]
      public void NoDuplicateServiceRegistrations()
      {
          // Verify no service type registered twice
      }

      [TestMethod]
      public void AllConstructorsAreUnambiguous()
      {
          // Each service should have 0 or 1 public constructor
      }

      [TestMethod]
      public void ScopedServicesNotInjectedIntoSingletons()
      {
          // Deep check of constructor parameter types
      }
  }
  ```

- [ ] Run tests: `dotnet test --filter "Category=DIValidation"`

- [ ] Fix any violations found

---

### 3C: Implement Service Lifetime Governance Attribute

- [ ] Create [src/WileyWidget.Services.Abstractions/ServiceLifetimeAttribute.cs](src/WileyWidget.Services.Abstractions/ServiceLifetimeAttribute.cs)

  ```csharp
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
  ```

- [ ] Annotate all service implementations:

  ```csharp
  [ServiceLifetime(ServiceLifetime.Scoped, "Depends on DbContext; must be per-request")]
  public class BudgetService : IBudgetService { }

  [ServiceLifetime(ServiceLifetime.Singleton, "Stateless; thread-safe")]
  public class ThemeService : IThemeService { }
  ```

- [ ] (Optional) Create Roslyn analyzer to validate attributes match registrations

- [ ] Build and verify

---

## Phase 4: LONG-TERM (Month 2+)

### 4A: Implement DI Validation as Build-Time Analyzer

- [ ] Create [src/WileyWidget.Analyzers/DIValidationAnalyzer.cs](src/WileyWidget.Analyzers/DIValidationAnalyzer.cs)

  **Rules to implement:**
  - WIDI001: Singleton service with scoped constructor parameter
  - WIDI002: Ambiguous constructors (multiple with same resolvable parameters)
  - WIDI003: Lifetime attribute mismatch (attribute says Singleton, registered as Scoped)
  - WIDI004: ServiceLifetime attribute missing on all services

- [ ] Register analyzer in .editorconfig or NuGet package

- [ ] Test analyzer with intentional violations

---

### 4B: Create DI Registration Documentation Generator

- [ ] Create [src/WileyWidget.WinForms/Tools/DIDocumentationGenerator.cs](src/WileyWidget.WinForms/Tools/DIDocumentationGenerator.cs)

  ```csharp
  public class DIDocumentationGenerator
  {
      public static string GenerateMarkdown(IServiceCollection services)
      {
          // Generates docs/DI_SERVICE_GRAPH.md
          // Lists all services by lifetime
          // Shows dependency relationships
      }
  }
  ```

- [ ] Add CI/CD task to generate docs on each build

- [ ] Document in [docs/DI_SERVICE_GRAPH.md](docs/DI_SERVICE_GRAPH.md)

---

## Verification Checklist

After each phase, verify:

- [ ] `dotnet build` succeeds
- [ ] `dotnet test --filter "Category=DIValidation"` passes
- [ ] `dotnet test` (all tests) passes
- [ ] No compiler warnings about DI
- [ ] Application starts without DI errors
- [ ] DI validation report shows 0 errors
- [ ] Code review approved by 2+ team members

---

## Rollback Plan

If any phase breaks the application:

1. **Revert changes:** `git revert <commit>`
2. **Document issue:** Create GitHub issue with tag `di-fragility`
3. **Review with team:** Discuss what went wrong
4. **Plan alternative:** Adjust approach before retrying

---

## Success Criteria

✅ **Phase 1 Complete When:**

- No duplicate service registrations
- No singleton-scoped scope violations
- Startup has 30-second timeout
- Build succeeds

✅ **Phase 2 Complete When:**

- All .Any() checks replaced with TryAdd\*
- Single entry point for service registration
- Keyed services working for multi-implementation scenarios
- 0 compiler warnings

✅ **Phase 3 Complete When:**

- All DI validation tests passing
- ServiceLifetime attributes on all services
- Startup via IHostedService pattern working
- No blocking calls in Program.cs

✅ **Phase 4 Complete When:**

- DI validation analyzer integrated in CI/CD
- Service graph documentation auto-generated
- Zero fragility issues in DI container
- DI setup documented and maintainable

---

## Team Communication

**For Phase 1 (This Week):**

> "We're fixing critical DI fragility issues that could cause runtime scope violations. Changes are minimal and focused."

**For Phase 2 (Next 2 Weeks):**

> "Improving DI resilience and reducing code fragility through refactoring. All public APIs unchanged."

**For Phase 3+ (Ongoing):**

> "Modernizing DI setup to match Microsoft best practices. Non-breaking changes; improves startup and reliability."

---

**Status:** Ready for Implementation
**Owner:** Backend / DevOps Team
**Stakeholder Reviews Required:** Architect, Tech Lead
