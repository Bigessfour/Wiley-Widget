using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// ViewModel for the QuickBooks Dashboard landing page.
    /// Displays connection status, company information, and key financial metrics.
    /// </summary>
    public partial class QuickBooksDashboardViewModel : ObservableRecipient
    {
        private readonly QuickBooksService _quickBooksService;
        private readonly AILoggingService _aiLogger;
        private CancellationTokenSource? _loadCancellationTokenSource;

        // Connection Status Properties
        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _connectionStatusText = "Disconnected";

        [ObservableProperty]
        private string _connectionStatusColor = "#DC3545"; // Red

        [ObservableProperty]
        private string _companyName = "No Company";

        [ObservableProperty]
        private string _fiscalYear = "N/A";

        [ObservableProperty]
        private string _lastSyncTime = "Never";

        // Financial KPI Properties
        [ObservableProperty]
        private decimal _revenueYTD;

        [ObservableProperty]
        private string _revenueYTDFormatted = "$0.00";

        [ObservableProperty]
        private decimal _netIncomeYTD;

        [ObservableProperty]
        private string _netIncomeYTDFormatted = "$0.00";

        [ObservableProperty]
        private decimal _accountsReceivableBalance;

        [ObservableProperty]
        private string _accountsReceivableFormatted = "$0.00";

        [ObservableProperty]
        private decimal _accountsPayableBalance;

        [ObservableProperty]
        private string _accountsPayableFormatted = "$0.00";

        // UI State Properties
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isSyncing;

        [ObservableProperty]
        private bool _hasData;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _showEmptyState;

        // Fiscal Year Properties
        [ObservableProperty]
        private DateTime _fiscalYearStart;

        [ObservableProperty]
        private DateTime _fiscalYearEnd;

        public QuickBooksDashboardViewModel(
            QuickBooksService quickBooksService,
            AILoggingService aiLogger)
        {
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _aiLogger = aiLogger ?? throw new ArgumentNullException(nameof(aiLogger));

            Log.Information("QuickBooksDashboardViewModel initialized");
        }

        /// <summary>
        /// Connect to QuickBooks and load dashboard data.
        /// </summary>
        [RelayCommand]
        private async Task ConnectAsync()
        {
            await ExecuteWithLoadingAsync(async (cancellationToken) =>
            {
                await _aiLogger.LogAsync("User initiated QuickBooks connection");
                Log.Information("Attempting to connect to QuickBooks...");

                // TODO: Replace with actual QuickBooksService.ConnectAsync() when implemented
                // Simulate connection for now
                await Task.Delay(1500, cancellationToken);
                
                // Mock successful connection
                IsConnected = true;
                ConnectionStatusText = "Connected";
                ConnectionStatusColor = "#28A745"; // Green
                CompanyName = "Demo Company";
                
                // Get company info including fiscal year
                var companyInfo = await _quickBooksService.GetCompanyInfoAsync(cancellationToken);
                CalculateFiscalYear(companyInfo.FiscalYearStartMonth);
                
                LastSyncTime = DateTime.Now.ToString("g", System.Globalization.CultureInfo.InvariantCulture);

                Log.Information("Connected to QuickBooks successfully");
                await LoadDashboardDataAsync(cancellationToken);
            });
        }

        /// <summary>
        /// Sync data from QuickBooks and refresh dashboard.
        /// </summary>
        [RelayCommand]
        private async Task SyncNowAsync()
        {
            await ExecuteWithLoadingAsync(async (cancellationToken) =>
            {
                await _aiLogger.LogAsync("User initiated QuickBooks sync");
                Log.Information("Starting QuickBooks data sync...");

                // TODO: Replace with actual QuickBooksService.SyncDataAsync() when implemented
                await Task.Delay(2000, cancellationToken);

                LastSyncTime = DateTime.Now.ToString("g", System.Globalization.CultureInfo.InvariantCulture);
                Log.Information("QuickBooks sync completed successfully");
                
                await LoadDashboardDataAsync(cancellationToken);
            });
        }

        /// <summary>
        /// Cancel the current sync operation.
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            Log.Information("User cancelled sync operation");
            _loadCancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Retry loading data after an error.
        /// </summary>
        [RelayCommand]
        private async Task RetryAsync()
        {
            HasError = false;
            ErrorMessage = string.Empty;
            await LoadAsync();
        }

        /// <summary>
        /// Main entry point for loading dashboard data.
        /// Called from OnNavigatedTo or when the view is initialized.
        /// </summary>
        public async Task LoadAsync()
        {
            await ExecuteWithLoadingAsync(async (cancellationToken) =>
            {
                Log.Information("Loading dashboard data...");
                await LoadDashboardDataAsync(cancellationToken);
            });
        }

        /// <summary>
        /// Load all dashboard data including connection status and financial metrics.
        /// </summary>
        private async Task LoadDashboardDataAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace with actual QuickBooksService calls when implemented
            // For now, simulate loading with demo data

            // Simulate checking connection status
            await Task.Delay(500, cancellationToken);

            if (!IsConnected)
            {
                // Show empty state - not connected
                ShowEmptyState = true;
                HasData = false;
                Log.Information("Not connected to QuickBooks - showing empty state");
                return;
            }

            // Simulate loading financial data
            await Task.Delay(1000, cancellationToken);

            // Mock financial data (will be replaced with real QuickBooks data)
            RevenueYTD = 1_250_000.00m;
            RevenueYTDFormatted = RevenueYTD.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);

            var expensesYTD = 850_000.00m;
            NetIncomeYTD = RevenueYTD - expensesYTD;
            NetIncomeYTDFormatted = NetIncomeYTD.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);

            AccountsReceivableBalance = 185_500.00m;
            AccountsReceivableFormatted = AccountsReceivableBalance.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);

            AccountsPayableBalance = 125_750.00m;
            AccountsPayableFormatted = AccountsPayableBalance.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);

            // Populate financial metrics collection for SfDataGrid
            FinancialMetrics.Clear();
            FinancialMetrics.Add(new FinancialMetric
            {
                Category = "Revenue",
                Metric = "Revenue YTD",
                Value = RevenueYTD,
                FormattedValue = RevenueYTDFormatted,
                Color = "#28A745",
                Icon = "\uE8A1"
            });
            FinancialMetrics.Add(new FinancialMetric
            {
                Category = "Profit",
                Metric = "Net Income YTD",
                Value = NetIncomeYTD,
                FormattedValue = NetIncomeYTDFormatted,
                Color = NetIncomeYTD >= 0 ? "#28A745" : "#DC3545",
                Icon = "\uE8B5"
            });
            FinancialMetrics.Add(new FinancialMetric
            {
                Category = "Receivables",
                Metric = "A/R Balance",
                Value = AccountsReceivableBalance,
                FormattedValue = AccountsReceivableFormatted,
                Color = "#007BFF",
                Icon = "\uE7C5"
            });
            FinancialMetrics.Add(new FinancialMetric
            {
                Category = "Payables",
                Metric = "A/P Balance",
                Value = AccountsPayableBalance,
                FormattedValue = AccountsPayableFormatted,
                Color = "#DC3545",
                Icon = "\uE8F0"
            });

            // Mark data as loaded
            HasData = true;
            ShowEmptyState = false;
            Log.Information("Dashboard data loaded successfully - Revenue: {Revenue}, Net Income: {NetIncome}", 
                RevenueYTDFormatted, NetIncomeYTDFormatted);
        }

        /// <summary>
        /// Execute an async action with loading state management and error handling.
        /// </summary>
        private async Task ExecuteWithLoadingAsync(Func<CancellationToken, Task> action)
        {
            // Cancel any existing operation
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource = new CancellationTokenSource();

            IsLoading = true;
            IsSyncing = true;
            HasError = false;
            ErrorMessage = string.Empty;

            try
            {
                await action(_loadCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Dashboard load operation was cancelled");
                await _aiLogger.LogAsync("User cancelled dashboard operation");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading dashboard data");
                await _aiLogger.LogAsync($"Dashboard load error: {ex.Message}");
                ShowError($"Failed to load dashboard: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                IsSyncing = false;
            }
        }

        /// <summary>
        /// Show an error message and update UI state.
        /// </summary>
        private void ShowError(string message)
        {
            HasError = true;
            ErrorMessage = message;
            HasData = false;
            ShowEmptyState = false;
        }

        /// <summary>
        /// Calculate fiscal year start and end dates based on the fiscal year start month.
        /// </summary>
        private void CalculateFiscalYear(int fiscalYearStartMonth)
        {
            var now = DateTime.Now;
            
            // Determine fiscal year start date
            // If current month is before fiscal year start, FY started last calendar year
            var fiscalYearStartYear = now.Month >= fiscalYearStartMonth ? now.Year : now.Year - 1;
            
            FiscalYearStart = new DateTime(fiscalYearStartYear, fiscalYearStartMonth, 1);
            FiscalYearEnd = FiscalYearStart.AddYears(1).AddDays(-1);
            
            // Format fiscal year display
            if (FiscalYearStart.Year == FiscalYearEnd.Year)
            {
                FiscalYear = FiscalYearStart.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                FiscalYear = $"{FiscalYearStart.Year}-{FiscalYearEnd.Year}";
            }
            
            Log.Information("Calculated fiscal year: {FiscalYear} (Start: {Start}, End: {End})", 
                FiscalYear, FiscalYearStart.ToString("d", System.Globalization.CultureInfo.InvariantCulture), 
                FiscalYearEnd.ToString("d", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
