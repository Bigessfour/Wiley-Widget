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
        /// Validates all Forms are registered correctly.
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
        private readonly ServiceAbstractions.IDiValidationService _coreValidator;
        private readonly ILogger<WinFormsDiValidator> _logger;

        public WinFormsDiValidator(
            ServiceAbstractions.IDiValidationService coreValidator,
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
                typeof(CoreServices.ErrorReportingService),
                typeof(ServiceAbstractions.ITelemetryService)
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
                typeof(BusinessInterfaces.IAccountsRepository),
                typeof(BusinessInterfaces.IActivityLogRepository),
                typeof(BusinessInterfaces.IAuditRepository),
                typeof(BusinessInterfaces.IBudgetRepository),
                typeof(BusinessInterfaces.IDepartmentRepository),
                typeof(BusinessInterfaces.IEnterpriseRepository),
                typeof(BusinessInterfaces.IMunicipalAccountRepository),
                typeof(BusinessInterfaces.IUtilityBillRepository),
                typeof(BusinessInterfaces.IUtilityCustomerRepository)
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
                typeof(ServiceAbstractions.ISettingsService),
                typeof(ServiceAbstractions.ISecretVaultService),
                typeof(CoreServices.HealthCheckService),
                typeof(CoreServices.IDialogTrackingService),
                typeof(ServiceAbstractions.IDiValidationService),

                // QuickBooks Services
                typeof(ServiceAbstractions.IQuickBooksApiClient),
                typeof(ServiceAbstractions.IQuickBooksService),

                // Dashboard & Budget Services
                typeof(ServiceAbstractions.IDashboardService),
                typeof(WinFormsServices.IBudgetCategoryService),
                typeof(CoreServices.IWileyWidgetContextService),

                // AI Services
                typeof(ServiceAbstractions.IAIService),
                typeof(ServiceAbstractions.IAILoggingService),
                typeof(ServiceAbstractions.IAuditService),

                // Reporting Services
                typeof(ServiceAbstractions.IReportExportService),
                typeof(ServiceAbstractions.IReportService),
                typeof(CoreServices.Excel.IExcelReaderService),
                typeof(CoreServices.Export.IExcelExportService),

                // Utility Services
                typeof(ServiceAbstractions.IDataAnonymizerService),
                typeof(ServiceAbstractions.IChargeCalculatorService),
                typeof(ServiceAbstractions.IAnalyticsService),
                typeof(ServiceAbstractions.IAnalyticsPipeline),
                typeof(ServiceAbstractions.IGrokSupercomputer),

                // Theme Services
                typeof(WinFormsServices.IThemeService),
                typeof(WinFormsServices.IThemeIconService)
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
            // Legacy forms replaced by panels - only MainForm remains
            var serviceTypes = new[]
            {
                typeof(MainForm)
                // ChartForm, SettingsForm, AccountsForm, etc. replaced by UserControl panels
            };

            return _coreValidator.ValidateServiceCategory(
                serviceProvider,
                serviceTypes,
                "Forms");
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
