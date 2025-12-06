using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using FluentValidation;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Validation;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Export;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls;
using System.Diagnostics;
using ServiceProviderExtensions = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;

namespace WileyWidget.WinForms.Configuration
{
    public static class DependencyInjection
    {
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // Make IConfiguration available to services that depend on it (tests and DI consumers)
            services.AddSingleton<IConfiguration>(configuration);

            // === CRITICAL INFRASTRUCTURE (MUST BE FIRST) ===

            // HTTP Client Factory
            services.AddHttpClient();
            Debug.WriteLine("DI: Registered AddHttpClient()");

            // Memory Cache
            services.AddMemoryCache();
            Debug.WriteLine("DI: Registered AddMemoryCache()");
            services.AddSingleton<ICacheService, MemoryCacheService>();
            Debug.WriteLine("DI: Registered ICacheService -> MemoryCacheService (Singleton)");

            // DbContext (SCOPED - NOT SINGLETON! DbContext is NOT thread-safe)
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json");

            // Validate and expand connection string
            // If the connection string contains environment variable placeholders (${VAR_NAME}),
            // attempt to resolve them from the environment
            connectionString = ExpandConnectionStringVariables(connectionString, configuration);

            // Validate connection string format before passing to Entity Framework
            ValidateConnectionString(connectionString);

            // Output connection string info to debug output
            System.Diagnostics.Debug.WriteLine("✓ Connection string validated successfully");
            System.Diagnostics.Debug.WriteLine($"  Server: {ExtractConnectionStringPart(connectionString, "Server") ?? ExtractConnectionStringPart(connectionString, "Data Source") ?? "(unknown)"}");
            System.Diagnostics.Debug.WriteLine($"  Database: {ExtractConnectionStringPart(connectionString, "Database") ?? ExtractConnectionStringPart(connectionString, "Initial Catalog") ?? "(unknown)"}");

            // Use a DbContextFactory to create short-lived DbContext instances when needed.
            // Register AppDbContext via the factory so existing code that depends on AppDbContext
            // can still resolve it as Scoped while allowing concurrent operations via the factory.

            // Also register a DbContextFactory so callers can create short-lived DbContext instances
            // This is important for UI/WinForms scenarios where multiple concurrent async operations
            // may occur on background threads. The factory prevents "A second operation was started"
            // EF Core concurrency exceptions by giving each operation its own context instance.
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                options.EnableSensitiveDataLogging(configuration.GetValue<bool>("DB:EnableSensitiveDataLogging", false));
                options.EnableDetailedErrors(configuration.GetValue<bool>("DB:EnableDetailedErrors", true));
            });
            Debug.WriteLine("DI: Registered AddDbContextFactory<AppDbContext>");

            // Provide AppDbContext as Scoped by creating a new context from the factory per scope.
            // Use GetRequiredService via the alias to avoid ambiguity with other GetRequiredService extension methods
            services.AddScoped(sp => ServiceProviderExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>>(sp).CreateDbContext());
            Debug.WriteLine("DI: Registered Scoped AppDbContext backed by IDbContextFactory");

            // === CORE SERVICES ===
            services.AddSingleton<ISettingsService, SettingsService>();
            Debug.WriteLine("DI: Registered ISettingsService -> SettingsService (Singleton)");
            services.AddSingleton<SettingsService>(sp => (SettingsService)sp.GetService(typeof(ISettingsService))!);
            Debug.WriteLine("DI: Registered SettingsService (singleton facade)");
            services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            Debug.WriteLine("DI: Registered ISecretVaultService -> EncryptedLocalSecretVaultService (Singleton)");
            services.AddScoped<IWileyWidgetContextService, WileyWidgetContextService>();
            Debug.WriteLine("DI: Registered IWileyWidgetContextService -> WileyWidgetContextService (Scoped)");

            // Configure HealthCheckConfiguration from appsettings.json using Options pattern
            services.AddOptions<HealthCheckConfiguration>()
                .Bind(configuration.GetSection("HealthChecks"))
                .ValidateOnStart();
            Debug.WriteLine("DI: Registered HealthCheckConfiguration options");

            services.AddSingleton<HealthCheckService>();
            Debug.WriteLine("DI: Registered HealthCheckService (Singleton)");

            // === DATA SERVICES ===
            services.AddSingleton<IQuickBooksApiClient, QuickBooksApiClient>();
            Debug.WriteLine("DI: Registered IQuickBooksApiClient -> QuickBooksApiClient (Singleton)");
            services.AddSingleton<IQuickBooksService, QuickBooksService>();
            Debug.WriteLine("DI: Registered IQuickBooksService -> QuickBooksService (Singleton)");

            // === REPOSITORIES (SCOPED - aligned with DbContext pattern) ===
            services.AddTransient<IEnterpriseRepository, EnterpriseRepository>();
            Debug.WriteLine("DI: Registered IEnterpriseRepository -> EnterpriseRepository (Transient)");
            // Repositories are stateless and should be registered as Transient so they are
            // resolved fresh per-operation when created inside a scope.
            services.AddTransient<IBudgetRepository, BudgetRepository>();
            Debug.WriteLine("DI: Registered IBudgetRepository -> BudgetRepository (Transient)");
            services.AddTransient<IAuditRepository, AuditRepository>();
            Debug.WriteLine("DI: Registered IAuditRepository -> AuditRepository (Transient)");
            services.AddTransient<IMunicipalAccountRepository, MunicipalAccountRepository>();
            Debug.WriteLine("DI: Registered IMunicipalAccountRepository -> MunicipalAccountRepository (Transient)");

            // Chart data aggregation service (Data layer implementation)
            services.AddTransient<Business.Interfaces.IChartService, WileyWidget.Data.Services.ChartService>();
            Debug.WriteLine("DI: Registered IChartService -> ChartService (Transient)");

            // Additional repository registrations to align with scoped AppDbContext
            services.AddTransient<IDepartmentRepository, DepartmentRepository>();
            Debug.WriteLine("DI: Registered IDepartmentRepository -> DepartmentRepository (Transient)");
            services.AddTransient<IUtilityBillRepository, UtilityBillRepository>();
            Debug.WriteLine("DI: Registered IUtilityBillRepository -> UtilityBillRepository (Transient)");
            services.AddTransient<IUtilityCustomerRepository, UtilityCustomerRepository>();
            Debug.WriteLine("DI: Registered IUtilityCustomerRepository -> UtilityCustomerRepository (Transient)");

            // === FEATURE SERVICES ===
            // XAIService needs DbContextFactory, so it cannot be Singleton.
            // Changed to Scoped to allow proper DI chain resolution.
            services.AddScoped<IAIService, XAIService>();
            Debug.WriteLine("DI: Registered IAIService -> XAIService (Scoped)");
            services.AddSingleton<IAILoggingService, AILoggingService>();
            Debug.WriteLine("DI: Registered IAILoggingService -> AILoggingService (Singleton)");

            // AI Assistant Service for tool execution (Scoped for proper lifecycle)
            services.AddScoped<IAIAssistantService, AIAssistantService>();
            Debug.WriteLine("DI: Registered IAIAssistantService -> AIAssistantService (Scoped)");

            // AI Tool Service for Grok function calling (Scoped for proper lifecycle)
            services.AddScoped<IAIToolService, AIToolService>();
            Debug.WriteLine("DI: Registered IAIToolService -> AIToolService (Scoped)");

            // === AI CONVERSATION PERSISTENCE ===
            // Conversation repository for saving/loading chat history
            services.AddTransient<IConversationRepository, ConversationRepository>();
            Debug.WriteLine("DI: Registered IConversationRepository -> ConversationRepository (Transient)");

            // === AI SERVICE VALIDATORS ===
            // Validators for AI inputs (injection prevention, constraint validation)
            services.AddTransient<IValidator<ChatMessage>, ChatMessageValidator>();
            Debug.WriteLine("DI: Registered IValidator<ChatMessage> -> ChatMessageValidator (Transient)");
            services.AddTransient<IValidator<ToolCall>, ToolCallValidator>();
            Debug.WriteLine("DI: Registered IValidator<ToolCall> -> ToolCallValidator (Transient)");
            services.AddTransient<IValidator<ConversationHistory>, ConversationHistoryValidator>();
            Debug.WriteLine("DI: Registered IValidator<ConversationHistory> -> ConversationHistoryValidator (Transient)");

            // === CHAT WINDOW AND CONTROLS ===
            // Chat window form for dedicated AI interaction
            services.AddScoped<ChatWindow>();
            Debug.WriteLine("DI: Registered ChatWindow (Scoped)");

            services.AddSingleton<IAuditService, AuditService>();
            Debug.WriteLine("DI: Registered IAuditService -> AuditService (Singleton)");
            services.AddSingleton<IReportExportService, ReportExportService>();
            Debug.WriteLine("DI: Registered IReportExportService -> ReportExportService (Singleton)");
            services.AddSingleton<IBoldReportService, BoldReportService>();
            Debug.WriteLine("DI: Registered IBoldReportService -> BoldReportService (Singleton)");
            services.AddTransient<IExcelReaderService, ExcelReaderService>();
            Debug.WriteLine("DI: Registered IExcelReaderService -> ExcelReaderService (Transient)");
            services.AddTransient<IExcelExportService, ExcelExportService>();
            Debug.WriteLine("DI: Registered IExcelExportService -> ExcelExportService (Transient)");
            services.AddTransient<SyncfusionPdfExportService>();
            Debug.WriteLine("DI: Registered SyncfusionPdfExportService (Transient)");
            services.AddSingleton<IPrintingService, PrintingService>();
            Debug.WriteLine("DI: Registered IPrintingService -> PrintingService (Singleton)");
            services.AddTransient<IDataAnonymizerService, DataAnonymizerService>();
            Debug.WriteLine("DI: Registered IDataAnonymizerService -> DataAnonymizerService (Transient)");

            // Account mapper used by ViewModels to transform domain models to display DTOs
            services.AddTransient<IAccountMapper, WileyWidget.Business.Services.AccountMapper>();
            Debug.WriteLine("DI: Registered IAccountMapper -> AccountMapper (Transient)");

            // === MVVM BUSINESS LOGIC SERVICES (Phase 2 Refactoring) ===
            // These services extract business logic from ViewModels for MVVM purity
            services.AddTransient<IAccountService, AccountService>();
            Debug.WriteLine("DI: Registered IAccountService -> AccountService (Transient)");
            services.AddTransient<IMainDashboardService, MainDashboardService>();
            Debug.WriteLine("DI: Registered IMainDashboardService -> MainDashboardService (Transient)");
            services.AddTransient<ISettingsManagementService, SettingsManagementService>();
            Debug.WriteLine("DI: Registered ISettingsManagementService -> SettingsManagementService (Transient)");
            // Register validator for SettingsDto. This ensures SettingsManagementService can resolve
            // its validation dependency during startup validation and at runtime.
            services.AddTransient<IValidator<SettingsDto>, SettingsDtoValidator>();
            Debug.WriteLine("DI: Registered IValidator<SettingsDto> -> SettingsDtoValidator (Transient)");

            // Extra safety: ensure the service collection actually contains a registration for the validator
            // when building the diagnostic provider. If not present for any reason (assembly binding issues,
            // conditional compilation, etc.), register a fallback implementation so resolution succeeds.
            if (!services.Any(sd => sd.ServiceType == typeof(IValidator<SettingsDto>)))
            {
                services.AddTransient<IValidator<SettingsDto>, SettingsDtoValidator>();
                Debug.WriteLine("DI: Fallback registered IValidator<SettingsDto> -> SettingsDtoValidator (Transient)");
            }

            // Register both Excel export implementations for flexibility
            // ClosedXmlExportService and ExcelExportService both implement IExcelExportService
            // Default is ExcelExportService above; applications can override as needed
            services.AddTransient<IChargeCalculatorService, ServiceChargeCalculatorService>();
            Debug.WriteLine("DI: Registered IChargeCalculatorService -> ServiceChargeCalculatorService (Transient)");
            services.AddSingleton<IDiValidationService, DiValidationService>();
            Debug.WriteLine("DI: Registered IDiValidationService -> DiValidationService (Singleton)");

            // === VIEWMODELS (SCOPED to match DbContext lifetime) ===
            // Using Scoped instead of Transient ensures ViewModels share the same DbContext instance
            // within a dialog/form lifetime, preventing "tracked by another instance" EF Core errors
            services.AddScoped<MainViewModel>();
            Debug.WriteLine("DI: Registered MainViewModel (Scoped)");
            services.AddScoped<ChartViewModel>();
            Debug.WriteLine("DI: Registered ChartViewModel (Scoped)");
            services.AddScoped<SettingsViewModel>();
            Debug.WriteLine("DI: Registered SettingsViewModel (Scoped)");
            services.AddScoped<AccountsViewModel>();
            Debug.WriteLine("DI: Registered AccountsViewModel (Scoped)");
            services.AddScoped<BudgetOverviewViewModel>();
            Debug.WriteLine("DI: Registered BudgetOverviewViewModel (Scoped)");
            services.AddScoped<ReportsViewModel>();
            Debug.WriteLine("DI: Registered ReportsViewModel (Scoped)");

            // === FORMS (SCOPED to get fresh instances per dialog) ===
            services.AddScoped<MainForm>();
            Debug.WriteLine("DI: Registered MainForm (Scoped)");
            services.AddScoped<ChartForm>();
            Debug.WriteLine("DI: Registered ChartForm (Scoped)");
            services.AddScoped<SettingsForm>();
            Debug.WriteLine("DI: Registered SettingsForm (Scoped)");
            services.AddScoped<AccountsForm>();
            Debug.WriteLine("DI: Registered AccountsForm (Scoped)");
            services.AddScoped<BudgetOverviewForm>();
            Debug.WriteLine("DI: Registered BudgetOverviewForm (Scoped)");
            services.AddScoped<ReportsForm>();
            Debug.WriteLine("DI: Registered ReportsForm (Scoped)");

            // === CONTROLS (TRANSIENT to avoid scope mismatch with Singleton forms) ===
            services.AddTransient<AIChatControl>();
            Debug.WriteLine("DI: Registered AIChatControl (Transient)");

            // === DI VALIDATION: Build a temporary provider with ValidateScopes = true and
            // attempt to resolve important scoped services (ViewModels) so errors are surfaced early
            // NOTE: DO NOT resolve ILoggerFactory here - it causes Serilog ReloadableLogger to freeze early,
            // which leads to "The logger is already frozen" exception when the host builds.
            try
            {
                var diagProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                Debug.WriteLine("DI: Temporary validation provider created with ValidateScopes = true");

                var viewModelTypes = new Type[]
                {
                    typeof(MainViewModel),
                    typeof(ChartViewModel),
                    typeof(SettingsViewModel),
                    typeof(AccountsViewModel),
                    typeof(BudgetOverviewViewModel)
                };

                foreach (var vmType in viewModelTypes)
                {
                    try
                    {
                        using var scope = diagProvider.CreateScope();
                        var vm = scope.ServiceProvider.GetRequiredService(vmType);
                        Debug.WriteLine($"DI: Successfully resolved {vmType.FullName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DI: Failed to resolve {vmType.FullName}: {ex}");
                    }
                }

                // Also test critical singleton/scoped services to ensure DI graph is healthy
                var serviceTypes = new Type[]
                {
                    typeof(ISecretVaultService),
                    typeof(IQuickBooksService),
                    typeof(IAIService),
                    typeof(IAILoggingService),
                    typeof(IQuickBooksApiClient)
                };

                foreach (var sType in serviceTypes)
                {
                    try
                    {
                        using var scope = diagProvider.CreateScope();
                        var svc = scope.ServiceProvider.GetService(sType);
                        if (svc != null)
                        {
                            Debug.WriteLine($"DI: Successfully resolved {sType.FullName}");
                        }
                        else
                        {
                            Debug.WriteLine($"DI: Service {sType.FullName} resolved to null (not registered)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DI: Failed to resolve {sType.FullName}: {ex}");
                    }
                }

                if (diagProvider is IDisposable d)
                {
                    d.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DI: Failed to build diagnostic provider: {ex}");
            }
        }

        /// <summary>
        /// Expands environment variable placeholders in connection strings.
        /// Supports ${VAR_NAME} syntax. If expansion fails, returns the original string.
        /// </summary>
        private static string ExpandConnectionStringVariables(string connectionString, IConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            // Check for environment variable placeholder pattern: ${VAR_NAME}
            if (!connectionString.Contains("${", StringComparison.Ordinal))
                return connectionString;

            var expanded = System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"\$\{(\w+)\}",
                match =>
                {
                    var varName = match.Groups[1].Value;
                    var value = Environment.GetEnvironmentVariable(varName);
                    return string.IsNullOrEmpty(value) ? match.Value : value;
                });

            return expanded;
        }

        /// <summary>
        /// Validates that a connection string is properly formatted and not just a placeholder.
        /// Throws InvalidOperationException if validation fails.
        /// </summary>
        private static void ValidateConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Connection string is empty or whitespace.");

            // Reject unexpanded placeholder variables
            if (connectionString.Contains("${", StringComparison.Ordinal) || connectionString.Contains("}", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Connection string contains unexpanded environment variable placeholders: {connectionString}. " +
                    "Set the corresponding environment variables (e.g., DB_CONNECTION_STRING) before running the application.");

            // Basic validation: connection strings should contain key parts
            // For SQL Server: should have 'Server' or 'Data Source'
            var lowerCs = connectionString.ToLowerInvariant();
            if (!lowerCs.Contains("server", StringComparison.Ordinal) && !lowerCs.Contains("data source", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Connection string does not appear to be a valid SQL Server connection string. " +
                    "It should contain 'Server' or 'Data Source' parameter.");
        }

        /// <summary>
        /// Extracts a specific key's value from a connection string
        /// </summary>
        private static string? ExtractConnectionStringPart(string connectionString, string key)
        {
            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(key))
                return null;

            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                var kvp = part.Split('=');
                if (kvp.Length == 2 && kvp[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp[1].Trim();
                }
            }

            return null;
        }
    }
}
