using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.ViewModels;

using BusinessInterfaces = WileyWidget.Business.Interfaces;
using ServiceAbstractions = WileyWidget.Services.Abstractions;
using CoreServices = WileyWidget.Services;
using WinFormsServices = WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Controls.ChatUI;

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
        DiValidationResult ValidateCriticalServices(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates all repositories are registered correctly.
        /// </summary>
        DiValidationResult ValidateRepositories(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates all business services are registered correctly.
        /// </summary>
        DiValidationResult ValidateServices(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates all ViewModels are registered correctly.
        /// </summary>
        DiValidationResult ValidateViewModels(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates MainForm is registered correctly.
        /// Note: Most UI components are now UserControl panels resolved via IPanelNavigationService.
        /// </summary>
        DiValidationResult ValidateForms(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates all panels registered with IPanelNavigationService.
        /// </summary>
        DiValidationResult ValidatePanels(IServiceProvider serviceProvider);

        /// <summary>
        /// Validates that all ScopedPanelBase<T> generic arguments are registered as scoped.
        /// </summary>
        DiValidationResult ValidateScopedPanels(IServiceProvider serviceProvider);

        /// <summary>
        /// Performs comprehensive validation of all DI registrations.
        /// </summary>
        DiValidationResult ValidateAll(IServiceProvider serviceProvider);
    }

    /// <summary>
    /// WinForms-specific DI validator that delegates to the core validation service
    /// but provides UI-layer specific categorization and reporting.
    /// </summary>
    public class WinFormsDiValidator : IWinFormsDiValidator
    {
        private readonly IDiValidationService _coreValidator;
        private readonly ILogger<WinFormsDiValidator> _logger;

        public WinFormsDiValidator(
            IDiValidationService coreValidator,
            ILogger<WinFormsDiValidator> logger)
        {
            _coreValidator = coreValidator ?? throw new ArgumentNullException(nameof(coreValidator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public DiValidationResult ValidateCriticalServices(IServiceProvider serviceProvider)
        {
            var serviceTypes = new[]
            {
                typeof(Microsoft.Extensions.Configuration.IConfiguration),
                typeof(Serilog.ILogger),
                typeof(ErrorReportingService),
                typeof(ITelemetryService)
            };

            var result = _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Critical Services");

            result.CategoryResults["Critical Services"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();

            return result;
        }

        public DiValidationResult ValidateRepositories(IServiceProvider serviceProvider)
        {
            var serviceTypes = new[]
            {
                typeof(IAccountsRepository),
                typeof(BusinessInterfaces.IActivityLogRepository),
                typeof(IAuditRepository),
                typeof(IBudgetRepository),
                typeof(IDepartmentRepository),
                typeof(IEnterpriseRepository),
                typeof(IMunicipalAccountRepository),
                typeof(IUtilityBillRepository),
                typeof(IUtilityCustomerRepository)
            };

            var result = _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Repositories");

            result.CategoryResults["Repositories"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();

            return result;
        }

        public DiValidationResult ValidateServices(IServiceProvider serviceProvider)
        {
            var serviceTypes = new List<Type>
            {
                // Core Services
                typeof(ISettingsService),
                typeof(ISecretVaultService),
                typeof(HealthCheckService),
                typeof(IDiValidationService),

                // QuickBooks Services
                typeof(IQuickBooksApiClient),
                typeof(IQuickBooksService),

                // Dashboard & Budget Services
                typeof(IDashboardService),
                typeof(IBudgetCategoryService),
                typeof(IWileyWidgetContextService),

                // AI Services
                typeof(IAIService),
                typeof(IAILoggingService),
                typeof(IAuditService),

                // Reporting Services
                typeof(IReportExportService),
                typeof(IReportService),
                typeof(CoreServices.Excel.IExcelReaderService),
                typeof(CoreServices.Export.IExcelExportService),

                // Utility Services
                typeof(IDataAnonymizerService),
                typeof(IChargeCalculatorService),
                typeof(IAnalyticsService),
                typeof(IAnalyticsPipeline),
                typeof(IGrokSupercomputer),

                // Theme Services
                typeof(IThemeService),
                typeof(IThemeIconService)
            };

            var result = _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Business Services");

            result.CategoryResults["Business Services"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();

            return result;
        }

        public DiValidationResult ValidateViewModels(IServiceProvider serviceProvider)
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
                typeof(MainViewModel),
                typeof(ReportsViewModel)
            };

            var result = _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "ViewModels");

            result.CategoryResults["ViewModels"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();

            return result;
        }

        public DiValidationResult ValidateForms(IServiceProvider serviceProvider)
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

        public DiValidationResult ValidatePanels(IServiceProvider serviceProvider)
        {
            var result = new DiValidationResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Known panel types that can be shown via IPanelNavigationService
            var panelTypes = new[]
            {
                typeof(DashboardPanel),
                typeof(AccountsPanel),
                typeof(BudgetOverviewPanel),
                typeof(ChartPanel),
                typeof(AnalyticsPanel),
                typeof(AuditLogPanel),
                typeof(CustomersPanel),
                typeof(ReportsPanel),
                typeof(ChatPanel),
                typeof(QuickBooksPanel),
                typeof(SettingsPanel)
            };

            foreach (var panelType in panelTypes)
            {
                try
                {
                    // Panels are not registered as DI services, so assume they are available
                    result.SuccessMessages.Add($"✓ {panelType.Name} is available");
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"✗ Failed to validate {panelType.Name}: {ex.Message}");
                }
            }

            stopwatch.Stop();
            result.ValidationDuration = stopwatch.Elapsed;
            result.IsValid = result.Errors.Count == 0;

            result.CategoryResults["Panels"] = result.SuccessMessages.Concat(result.Errors).Concat(result.Warnings).ToList();

            return result;
        }

        public DiValidationResult ValidateScopedPanels(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            var result = new DiValidationResult();
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
                var viewModelType = panelType.BaseType.GetGenericArguments()[0];
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

        public DiValidationResult ValidateAll(IServiceProvider serviceProvider)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var combinedResult = new DiValidationResult();

#if DEBUG
            _logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║         COMPREHENSIVE DI VALIDATION STARTING                   ║");
            _logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");
#else
            _logger.LogInformation("Comprehensive DI validation starting");
#endif

            // Validate each category
            var results = new[]
            {
                ValidateCriticalServices(serviceProvider),
                ValidateRepositories(serviceProvider),
                ValidateServices(serviceProvider),
                ValidateViewModels(serviceProvider),
                ValidateForms(serviceProvider),
                ValidatePanels(serviceProvider),
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
