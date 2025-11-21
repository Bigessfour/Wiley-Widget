using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// ViewModel for the QuickBooks Dashboard landing page.
    /// Displays connection status, company information, and key financial metrics.
    /// </summary>
    public partial class QuickBooksDashboardViewModel : ObservableRecipient, IDisposable
    {
        private readonly QuickBooksService _quickBooksService;
        private readonly AILoggingService _aiLogger;
        private CancellationTokenSource? _loadCancellationTokenSource;

        // Connection Status Properties
        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private string _connectionStatusText = "Disconnected";
        public string ConnectionStatusText { get => _connectionStatusText; set => SetProperty(ref _connectionStatusText, value); }

        private string _connectionStatusColor = "#DC3545"; // Red
        public string ConnectionStatusColor { get => _connectionStatusColor; set => SetProperty(ref _connectionStatusColor, value); }

        private string _companyName = "No Company";
        public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }

        private string _fiscalYear = "N/A";
        public string FiscalYear { get => _fiscalYear; set => SetProperty(ref _fiscalYear, value); }

        private string _lastSyncTime = "Never";
        public string LastSyncTime { get => _lastSyncTime; set => SetProperty(ref _lastSyncTime, value); }

        // Financial KPI Properties
        private decimal _revenueYTD;
        public decimal RevenueYTD { get => _revenueYTD; set => SetProperty(ref _revenueYTD, value); }

        private string _revenueYTDFormatted = "$0.00";
        public string RevenueYTDFormatted { get => _revenueYTDFormatted; set => SetProperty(ref _revenueYTDFormatted, value); }

        private decimal _netIncomeYTD;
        public decimal NetIncomeYTD { get => _netIncomeYTD; set => SetProperty(ref _netIncomeYTD, value); }

        private string _netIncomeYTDFormatted = "$0.00";
        public string NetIncomeYTDFormatted { get => _netIncomeYTDFormatted; set => SetProperty(ref _netIncomeYTDFormatted, value); }

        private decimal _accountsReceivableBalance;
        public decimal AccountsReceivableBalance { get => _accountsReceivableBalance; set => SetProperty(ref _accountsReceivableBalance, value); }

        private string _accountsReceivableFormatted = "$0.00";
        public string AccountsReceivableFormatted { get => _accountsReceivableFormatted; set => SetProperty(ref _accountsReceivableFormatted, value); }

        private decimal _accountsPayableBalance;
        public decimal AccountsPayableBalance { get => _accountsPayableBalance; set => SetProperty(ref _accountsPayableBalance, value); }

        private string _accountsPayableFormatted = "$0.00";
        public string AccountsPayableFormatted { get => _accountsPayableFormatted; set => SetProperty(ref _accountsPayableFormatted, value); }

        // UI State Properties
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        private bool _isSyncing;
        public bool IsSyncing { get => _isSyncing; set => SetProperty(ref _isSyncing, value); }

        private bool _hasData;
        public bool HasData { get => _hasData; set => SetProperty(ref _hasData, value); }

        private bool _hasError;
        public bool HasError { get => _hasError; set => SetProperty(ref _hasError, value); }

        private string _errorMessage = string.Empty;
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        private bool _showEmptyState;
        public bool ShowEmptyState { get => _showEmptyState; set => SetProperty(ref _showEmptyState, value); }

        // Fiscal Year Properties
        private DateTime _fiscalYearStart;
        public DateTime FiscalYearStart { get => _fiscalYearStart; set => SetProperty(ref _fiscalYearStart, value); }

        private DateTime _fiscalYearEnd;
        public DateTime FiscalYearEnd { get => _fiscalYearEnd; set => SetProperty(ref _fiscalYearEnd, value); }

        private ObservableCollection<FinancialMetric> _financialMetrics = new();
        public ObservableCollection<FinancialMetric> FinancialMetrics { get => _financialMetrics; set => SetProperty(ref _financialMetrics, value); }

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
                
                LastSyncTime = DateTime.Now.ToString("g", CultureInfo.InvariantCulture);

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

                LastSyncTime = DateTime.Now.ToString("g", CultureInfo.InvariantCulture);
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
            RevenueYTDFormatted = RevenueYTD.ToString("C2", CultureInfo.CurrentCulture);

            var expensesYTD = 850_000.00m;
            NetIncomeYTD = RevenueYTD - expensesYTD;
            NetIncomeYTDFormatted = NetIncomeYTD.ToString("C2", CultureInfo.CurrentCulture);

            AccountsReceivableBalance = 185_500.00m;
            AccountsReceivableFormatted = AccountsReceivableBalance.ToString("C2", CultureInfo.CurrentCulture);

            AccountsPayableBalance = 125_750.00m;
            AccountsPayableFormatted = AccountsPayableBalance.ToString("C2", CultureInfo.CurrentCulture);

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
                FiscalYear = FiscalYearStart.Year.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                FiscalYear = $"{FiscalYearStart.Year}-{FiscalYearEnd.Year}";
            }
            
            Log.Information("Calculated fiscal year: {FiscalYear} (Start: {Start}, End: {End})", 
                FiscalYear, FiscalYearStart.ToString("d", CultureInfo.InvariantCulture), 
                FiscalYearEnd.ToString("d", CultureInfo.InvariantCulture));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource?.Dispose();
            }
        }
    }
}
