using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.ViewModels;

using BusinessInterfaces = WileyWidget.Business.Interfaces;
using ServiceAbstractions = WileyWidget.Services.Abstractions;
using CoreServices = WileyWidget.Services;
using WinFormsServices = WileyWidget.WinForms.Services;

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

            return _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Critical Services");
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

            return _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Repositories");
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

            return _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Business Services");
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

            return _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "ViewModels");
        }

        public DiValidationResult ValidateForms(IServiceProvider serviceProvider)
        {
            // Most UI components are now panels/controls, only MainForm is registered as a Form
            var serviceTypes = new[]
            {
                typeof(MainForm)
                // ChartForm, SettingsForm, AccountsForm, etc. replaced by UserControl panels
                // Panels are resolved dynamically via IPanelNavigationService
            };

            return _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Forms (MainForm only - panels resolved via navigation service)");
        }

        public DiValidationResult ValidateAll(IServiceProvider serviceProvider)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var combinedResult = new DiValidationResult();

            _logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║         COMPREHENSIVE DI VALIDATION STARTING                   ║");
            _logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");

            // Validate each category
            var results = new[]
            {
                ValidateCriticalServices(serviceProvider),
                ValidateRepositories(serviceProvider),
                ValidateServices(serviceProvider),
                ValidateViewModels(serviceProvider),
                ValidateForms(serviceProvider)
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
            _logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║         COMPREHENSIVE DI VALIDATION COMPLETE                   ║");
            _logger.LogInformation("╠════════════════════════════════════════════════════════════════╣");
            _logger.LogInformation("║ Total Services Validated: {Count,4}                            ║", combinedResult.SuccessMessages.Count);
            _logger.LogInformation("║ Errors:                   {Count,4}                            ║", combinedResult.Errors.Count);
            _logger.LogInformation("║ Warnings:                 {Count,4}                            ║", combinedResult.Warnings.Count);
            _logger.LogInformation("║ Duration:                 {Duration,4:F0}ms                       ║", combinedResult.ValidationDuration.TotalMilliseconds);
            _logger.LogInformation("║ Status:                   {Status,-30} ║", combinedResult.IsValid ? "✓ PASSED" : "✗ FAILED");
            _logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");

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
