using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Services;
using WileyWidget.ViewModels;


namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// WinForms-specific DI validation orchestrator.
    /// Uses the core IDiValidationService for the heavy lifting but provides
    /// WinForms-specific categorization and validation workflows.
    /// </summary>
    public interface IWinFormsDiValidator
    {
        /// <summary>
        /// Validates all critical services are registered in the DI container.
        /// </summary>
        WileyWidget.Services.Abstractions.DiValidationResult ValidateCriticalServices(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates all repositories are registered correctly.
        /// </summary>
        WileyWidget.Services.Abstractions.DiValidationResult ValidateRepositories(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates all business services are registered correctly.
        /// </summary>
        WileyWidget.Services.Abstractions.DiValidationResult ValidateServices(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates all ViewModels are registered correctly.
        /// </summary>
        WileyWidget.Services.Abstractions.DiValidationResult ValidateViewModels(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates MainForm is registered correctly.
        /// Note: Most UI components are now UserControl panels resolved via IPanelNavigationService.
        /// </summary>
        WileyWidget.Services.Abstractions.DiValidationResult ValidateForms(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates all panels defined in the PanelRegistry are correctly registered in DI.
        /// </summary>
        WileyWidget.Services.Abstractions.DiValidationResult ValidatePanelsFromRegistry(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates that all ScopedPanelBase<T> generic arguments are registered as scoped.
        /// </summary>
        WileyWidget.Services.Abstractions.DiValidationResult ValidateScopedPanels(IServiceProvider serviceProvider);

        /// <summary>
        /// Performs comprehensive validation of all DI registrations.
        /// </summary>
        WileyWidget.Services.Abstractions.DiValidationResult ValidateAll(IServiceProvider serviceProvider);
    }

    /// <summary>
    /// WinForms-specific DI validator that delegates to the core validation service
    /// but provides UI-layer specific categorization and reporting.
    /// </summary>
    public class WinFormsDiValidator : IWinFormsDiValidator
    {
        private readonly global::WileyWidget.Services.DiValidationService _coreValidator;
        private readonly ILogger<WinFormsDiValidator> _logger;
        private readonly IConfiguration _configuration;

        public WinFormsDiValidator(
            global::WileyWidget.Services.DiValidationService coreValidator,
            ILogger<WinFormsDiValidator> logger,
            IConfiguration configuration)
        {
            _coreValidator = coreValidator ?? throw new ArgumentNullException(nameof(coreValidator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        private bool IsFastValidationEnabled => _configuration.GetValue<bool>("Validation:FastValidation", true);

        private WileyWidget.Services.Abstractions.DiValidationResult ValidateCategoryWithFastSupport(
            IServiceProvider serviceProvider,
            IEnumerable<Type> serviceTypes,
            string categoryName)
        {
            var isService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IServiceProviderIsService>(serviceProvider);

            if (IsFastValidationEnabled && isService != null)
            {
                var result = new WileyWidget.Services.Abstractions.DiValidationResult();
                var sw = System.Diagnostics.Stopwatch.StartNew();

                foreach (var type in serviceTypes)
                {
                    if (isService.IsService(type))
                    {
                        result.SuccessMessages.Add($"{type.Name} registered successfully (Fast)");
                    }
                    else
                    {
                        result.Errors.Add($"{type.Name} is NOT registered (Fast)");
                        result.IsValid = false;
                    }
                }

                sw.Stop();
                result.ValidationDuration = sw.Elapsed;
                result.CategoryResults[categoryName] = result.SuccessMessages.Concat(result.Errors).ToList();
                result.IsValid = result.Errors.Count == 0;
                return result;
            }

            // Fallback to core validator (which may instantiate if fastValidation=false)
            var coreResult = _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                categoryName);

            coreResult.CategoryResults[categoryName] = coreResult.SuccessMessages.Concat(coreResult.Errors).Concat(coreResult.Warnings).ToList();
            return coreResult;
        }

        public WileyWidget.Services.Abstractions.DiValidationResult ValidateCriticalServices(IServiceProvider serviceProvider)
        {
            var serviceTypes = new[]
            {
                typeof(Microsoft.Extensions.Configuration.IConfiguration),
                typeof(Serilog.ILogger),
                typeof(WileyWidget.Services.ErrorReportingService),
                typeof(WileyWidget.Services.Abstractions.ITelemetryService)
            };

            return ValidateCategoryWithFastSupport(serviceProvider, serviceTypes, "Critical Services");
        }

        public WileyWidget.Services.Abstractions.DiValidationResult ValidateRepositories(IServiceProvider serviceProvider)
        {
            var serviceTypes = new[]
            {
                typeof(IAccountsRepository),
                typeof(WileyWidget.Business.Interfaces.IActivityLogRepository),
                typeof(IAuditRepository),
                typeof(IBudgetRepository),
                typeof(IDepartmentRepository),
                typeof(IEnterpriseRepository),
                typeof(IMunicipalAccountRepository),
                typeof(IUtilityBillRepository),
                typeof(IUtilityCustomerRepository)
            };

            return ValidateCategoryWithFastSupport(serviceProvider, serviceTypes, "Repositories");
        }

        public WileyWidget.Services.Abstractions.DiValidationResult ValidateServices(IServiceProvider serviceProvider)
        {

            var serviceTypes = new List<Type>
            {
                // Core Services
                typeof(WileyWidget.Services.SettingsService),
                typeof(WileyWidget.Services.Abstractions.ISecretVaultService),
                typeof(WileyWidget.Services.HealthCheckService),
                typeof(global::WileyWidget.Services.DiValidationService),

                // QuickBooks Services (validate as interfaces, not concrete types)
                typeof(WileyWidget.Services.Abstractions.IQuickBooksApiClient),
                typeof(WileyWidget.Services.Abstractions.IQuickBooksService),

                // Dashboard & Budget Services
                typeof(WileyWidget.Services.Abstractions.IDashboardService),
                typeof(IBudgetCategoryService),

                // AI Services
                typeof(WileyWidget.Services.Abstractions.IAIService),
                typeof(WileyWidget.Services.Abstractions.IAILoggingService),
                typeof(WileyWidget.Services.Abstractions.IAuditService),

                // Reporting Services
                typeof(WileyWidget.Services.Abstractions.IReportExportService),
                typeof(WileyWidget.Services.Abstractions.IReportService),
                typeof(WileyWidget.Services.Excel.IExcelReaderService),
                typeof(WileyWidget.Services.Export.IExcelExportService),

                // Utility Services
                typeof(WileyWidget.Services.Abstractions.IDataAnonymizerService),
                typeof(WileyWidget.Services.Abstractions.IChargeCalculatorService),
                typeof(WileyWidget.Services.Abstractions.IAnalyticsService),
                typeof(WileyWidget.Services.Abstractions.IAnalyticsPipeline),
                typeof(WileyWidget.Services.Abstractions.IGrokSupercomputer)
            };

            return ValidateCategoryWithFastSupport(serviceProvider, serviceTypes, "Business Services");
        }

        public WileyWidget.Services.Abstractions.DiValidationResult ValidateViewModels(IServiceProvider serviceProvider)
        {
            var serviceTypes = new[]
            {
                typeof(ChartViewModel),
                typeof(SettingsViewModel),
                typeof(AccountsViewModel),
                typeof(DashboardViewModel),
                typeof(AnalyticsViewModel),
                typeof(BudgetOverviewViewModel),
                typeof(BudgetViewModel),
                typeof(CustomersViewModel),
                typeof(WileyWidget.WinForms.ViewModels.MainViewModel),
                typeof(ReportsViewModel)
            };

            return ValidateCategoryWithFastSupport(serviceProvider, serviceTypes, "ViewModels");
        }

        public WileyWidget.Services.Abstractions.DiValidationResult ValidateForms(IServiceProvider serviceProvider)
        {
            // Most UI components are now panels/controls, only MainForm is registered as a Form
            // To avoid runtime DI scope disposal issues, skip resolving MainForm during validation
            var serviceTypes = Array.Empty<Type>(); // Do not validate MainForm instance

            var result = _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Forms (MainForm skipped - panels resolved via navigation service)");

            result.CategoryResults["Forms"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();

            return result;
        }

        public WileyWidget.Services.Abstractions.DiValidationResult ValidatePanelsFromRegistry(IServiceProvider serviceProvider)
        {
            var panelTypes = PanelRegistry.Panels
                .Select(p => p.PanelType)
                .Distinct()
                .ToList();

            var result = new WileyWidget.Services.Abstractions.DiValidationResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("=== Starting Panels Validation ===");

            var isService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IServiceProviderIsService>(serviceProvider);
            if (isService == null)
            {
                _logger.LogWarning("IServiceProviderIsService is not available; falling back to instance resolution for panel validation.");
                var resolved = _coreValidator.ValidateServiceCategory(
                    serviceProvider,
                    panelTypes,
                    "Panels");

                resolved.CategoryResults["Panels"] = resolved.SuccessMessages.Concat(resolved.Errors).Concat(resolved.Warnings).ToList();
                return resolved;
            }

            foreach (var panelType in panelTypes)
            {
                try
                {
                    if (isService.IsService(panelType))
                    {
                        var success = $"{panelType.Name} registered successfully";
                        result.SuccessMessages.Add(success);
                        _logger.LogInformation("OK {Success}", success);
                    }
                    else
                    {
                        var error = $"{panelType.Name} is NOT registered in DI";
                        result.Errors.Add(error);
                        _logger.LogError("FAIL {Error}", error);
                    }
                }
                catch (Exception ex)
                {
                    var error = $"{panelType.Name} failed to validate: {ex.Message}";
                    result.Errors.Add(error);
                    _logger.LogError(ex, "FAIL {Error}", error);
                }
            }

            stopwatch.Stop();
            result.ValidationDuration = stopwatch.Elapsed;
            result.IsValid = result.Errors.Count == 0;
            result.CategoryResults["Panels"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();

            _logger.LogInformation(result.GetSummary());
            return result;
        }

        public WileyWidget.Services.Abstractions.DiValidationResult ValidateScopedPanels(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            var result = new WileyWidget.Services.Abstractions.DiValidationResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Find all types inheriting from ScopedPanelBase<T>
            var scopedPanelTypes = System.Reflection.Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.BaseType != null && t.BaseType.IsGenericType &&
                            t.BaseType.GetGenericTypeDefinition() == typeof(ScopedPanelBase<>))
                .ToList();

            IServiceScopeFactory scopeFactory;
            try
            {
                scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IServiceScopeFactory>(serviceProvider);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Errors.Add($"✗ Failed to resolve IServiceScopeFactory: {ex.Message}");
                result.ValidationDuration = stopwatch.Elapsed;
                result.IsValid = false;
                result.CategoryResults["Scoped Panels"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();
                return result;
            }

            if (scopeFactory == null)
            {
                stopwatch.Stop();
                result.Errors.Add("✗ IServiceScopeFactory is not registered");
                result.ValidationDuration = stopwatch.Elapsed;
                result.IsValid = false;
                result.CategoryResults["Scoped Panels"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();
                return result;
            }

            // Create a scope to resolve scoped services
            using var scope = scopeFactory.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            foreach (var panelType in scopedPanelTypes)
            {
                var genericArgs = panelType.BaseType?.GetGenericArguments();
                if (genericArgs == null || genericArgs.Length == 0)
                {
                    result.Errors.Add($"✗ {panelType.Name} BaseType is not generic or has no arguments");
                    continue;
                }
                var viewModelType = genericArgs[0];

                // Skip abstract ViewModel types (e.g. ObservableObject used as a no-ViewModel placeholder)
                if (viewModelType.IsAbstract)
                {
                    result.SuccessMessages.Add($"✓ {panelType.Name} ViewModel {viewModelType.Name} is abstract (no DI registration required)");
                    continue;
                }

                try
                {
                    // Check if ViewModel can be resolved from scoped provider
                    if (scopedProvider.GetService(viewModelType) != null)
                    {
                        result.SuccessMessages.Add($"✓ {panelType.Name} ViewModel {viewModelType.Name} can be resolved");
                    }
                    else
                    {
                        result.Errors.Add($"✗ {panelType.Name} ViewModel {viewModelType.Name} cannot be resolved");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"✗ Failed to validate {panelType.Name}: {ex.Message}");
                }
            }

            stopwatch.Stop();
            result.ValidationDuration = stopwatch.Elapsed;
            result.IsValid = result.Errors.Count == 0;

            result.CategoryResults["Scoped Panels"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();

            return result;
        }

        public WileyWidget.Services.Abstractions.DiValidationResult ValidateAll(IServiceProvider serviceProvider)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var combinedResult = new WileyWidget.Services.Abstractions.DiValidationResult();

            if (!_configuration.GetValue<bool>("Validation:DiValidationEnabled", true))
            {
                _logger.LogInformation("DI Validation is disabled in configuration.");
                combinedResult.IsValid = true;
                combinedResult.SuccessMessages.Add("Validation skipped per configuration.");
                return combinedResult;
            }

#if DEBUG
            _logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║         COMPREHENSIVE DI VALIDATION STARTING                   ║");
            _logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");
#else
            _logger.LogInformation("Comprehensive DI validation starting");
#endif

            try
            {
                // Validate each category
                var results = new[]
                {
                    ValidateCriticalServices(serviceProvider),
                    ValidateRepositories(serviceProvider),
                    ValidateServices(serviceProvider),
                    ValidateViewModels(serviceProvider),
                    ValidateForms(serviceProvider),
                    ValidatePanelsFromRegistry(serviceProvider),
                    ValidateScopedPanels(serviceProvider)
                };

                // Combine results
                foreach (var result in results)
                {
                    combinedResult.Errors.AddRange(result.Errors);
                    combinedResult.Warnings.AddRange(result.Warnings);
                    combinedResult.SuccessMessages.AddRange(result.SuccessMessages);
                }

                stopwatch.Stop();
                combinedResult.ValidationDuration = stopwatch.Elapsed;
                combinedResult.IsValid = combinedResult.Errors.Count == 0;
            }
            catch (OperationCanceledException oce)
            {
                stopwatch.Stop();
                var message = $"DI validation was canceled (likely timeout in service initialization): {oce.Message}";
                _logger.LogWarning(oce, message);
                combinedResult.Warnings.Add($"Validation interrupted: {oce.Message}");
                combinedResult.Errors.Add(message);
                combinedResult.ValidationDuration = stopwatch.Elapsed;
                combinedResult.IsValid = false;
            }
            catch (TimeoutException tex)
            {
                stopwatch.Stop();
                var message = $"DI validation timed out: {tex.Message}";
                _logger.LogWarning(tex, message);
                combinedResult.Warnings.Add($"Validation timeout: {tex.Message}");
                combinedResult.Errors.Add(message);
                combinedResult.ValidationDuration = stopwatch.Elapsed;
                combinedResult.IsValid = false;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var message = $"Unexpected error during DI validation ({ex.GetType().Name}): {ex.Message}";
                _logger.LogError(ex, message);
                combinedResult.Errors.Add(message);
                combinedResult.ValidationDuration = stopwatch.Elapsed;
                combinedResult.IsValid = false;
            }

            // Log final summary
#if DEBUG
            _logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║         COMPREHENSIVE DI VALIDATION COMPLETE                   ║");
            _logger.LogInformation("╠════════════════════════════════════════════════════════════════╣");
            _logger.LogInformation("║ Total Services Validated: {Count,4}                            ║", combinedResult.SuccessMessages.Count);
            _logger.LogInformation("║ Errors:                   {Count,4}                            ║", combinedResult.Errors.Count);
            _logger.LogInformation("║ Warnings:                 {Count,4}                            ║", combinedResult.Warnings.Count);
            _logger.LogInformation("║ Duration:                 {Duration,4:F0}ms                       ║", combinedResult.ValidationDuration.TotalMilliseconds);
            _logger.LogInformation("║ Status:                   {Status,-30} ║", combinedResult.IsValid ? "✓ PASSED" : "✗ FAILED");
            _logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");
#else
            _logger.LogInformation(
                "Comprehensive DI validation complete: {ServicesValidated} validated, {Errors} errors, {Warnings} warnings, duration={DurationMs}ms, status={Status}",
                combinedResult.SuccessMessages.Count,
                combinedResult.Errors.Count,
                combinedResult.Warnings.Count,
                combinedResult.ValidationDuration.TotalMilliseconds,
                combinedResult.IsValid ? "PASSED" : "FAILED");
#endif

            if (!combinedResult.IsValid)
            {
                _logger.LogError("DI Validation failed with {ErrorCount} errors:", combinedResult.Errors.Count);
                foreach (var error in combinedResult.Errors)
                {
                    _logger.LogError("  ✗ {Error}", error);
                }
            }

            return combinedResult;
        }
    }
}
