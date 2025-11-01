using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Prism.Dialogs;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Views.Dialogs;

namespace WileyWidget.ViewModels.Main {
    public partial class SettingsViewModel : BindableBase, INotifyDataErrorInfo, INavigationAware, IDisposable
    {
        // Prism navigation lifecycle hooks - kept minimal
    private CancellationTokenSource? _settingsNavCts;

    public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger?.LogInformation("SettingsViewModel navigated to");

            try
            {
                _settingsNavCts?.Cancel();
                _settingsNavCts?.Dispose();
            }
            catch { }

            _settingsNavCts = new CancellationTokenSource();
            var ct = _settingsNavCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    // If a 'tab' parameter is supplied, open that settings tab
                    if (navigationContext?.Parameters != null && navigationContext.Parameters.ContainsKey("tab") &&
                        navigationContext.Parameters["tab"] is string tabName)
                    {
                        _logger?.LogInformation("Opening settings tab: {Tab}", tabName);
                        // UI view should bind to the SelectedTab property; set it if available
                        // Fallback: just log if no property exists
                    }

                    // If refresh param provided, reload setting groups
                    if (navigationContext?.Parameters != null && navigationContext.Parameters.ContainsKey("refresh") &&
                        navigationContext.Parameters["refresh"] is bool refresh && refresh)
                    {
                        await ExecuteSaveSettingsAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Settings navigation canceled");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during Settings OnNavigatedTo");
                }
            }, ct);
        }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger?.LogInformation("SettingsViewModel navigated from");

            try
            {
                _settingsNavCts?.Cancel();
                _settingsNavCts?.Dispose();
                _settingsNavCts = null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error cancelling settings navigation token");
            }
        }
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly IOptions<AppOptions> _appOptions;
        private readonly IOptionsMonitor<AppOptions> _appOptionsMonitor;
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _dbContext;
        private readonly ISecretVaultService _secretVaultService;
        private readonly IQuickBooksService? _quickBooksService;
        private readonly ISyncfusionLicenseService _syncfusionLicenseService;
        private readonly IAIService _aiService;
        private readonly IAuditService _auditService;
        // NOTE: ThemeManager removed - SfSkinManager.ApplicationTheme handles all theming globally
    private readonly ISettingsService _settingsService;
    private readonly Prism.Dialogs.IDialogService _dialogService;

        private readonly Dictionary<string, List<string>> _errors = new();
        public Prism.Commands.DelegateCommand OpenXaiConsoleCommand { get; private set; }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool HasErrors => _errors.Any();

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return _errors.Values.SelectMany(x => x);

            return _errors.TryGetValue(propertyName, out var errors) ? errors : Enumerable.Empty<string>();
        }

        private void AddError(string propertyName, string error)
        {
            if (!_errors.ContainsKey(propertyName))
                _errors[propertyName] = new List<string>();

            if (!_errors[propertyName].Contains(error))
            {
                _errors[propertyName].Add(error);
                OnErrorsChanged(propertyName);
            }
        }

        private void ClearErrors(string propertyName)
        {
            if (_errors.Remove(propertyName))
                OnErrorsChanged(propertyName);
        }

        private void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        // General Settings
        private ObservableCollection<string> availableThemes = new() { "FluentDark", "FluentLight" };
        [Category("Appearance")]
        [DisplayName("Available Themes")]
        [Description("List of available application themes")]
        public ObservableCollection<string> AvailableThemes
        {
            get => availableThemes;
            set => SetProperty(ref availableThemes, value);
        }

        private string selectedTheme = "FluentDark";
        [Category("Appearance")]
        [DisplayName("Selected Theme")]
        [Description("Currently selected application theme")]
        public string SelectedTheme
        {
            get => selectedTheme;
            set
            {
                if (SetProperty(ref selectedTheme, value))
                {
                    try { OnSelectedThemeChanged(value); } catch { }
                }
            }
        }

        private bool isDarkMode;
        [Category("Appearance")]
        [DisplayName("Dark Mode")]
        [Description("Whether dark mode is currently enabled")]
        public bool IsDarkMode
        {
            get => isDarkMode;
            set => SetProperty(ref isDarkMode, value);
        }

        private bool isLoading;
        [Category("System")]
        [Browsable(false)]
        public bool IsLoading
        {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        private string statusMessage = "Ready";
        [Category("System")]
        [DisplayName("Status Message")]
        [Description("Current operation status message")]
        public string StatusMessage
        {
            get => statusMessage;
            set => SetProperty(ref statusMessage, value);
        }

        private void OnWindowWidthChanged(int value)
        {
            ValidateWindowWidth(value);
        }

        private void OnWindowHeightChanged(int value)
        {
            ValidateWindowHeight(value);
        }

        private void OnXaiTimeoutSecondsChanged(int value)
        {
            ValidateXaiTimeout(value);
        }

        private void OnContextWindowSizeChanged(int value)
        {
            ValidateContextWindowSize(value);
        }

        private void OnCacheExpirationMinutesChanged(int value)
        {
            ValidateCacheExpiration(value);
        }

        private void OnFiscalYearStartDayChanged(int value)
        {
            ValidateFiscalYearDay(value);
        }

        private void OnTemperatureChanged(double value)
        {
            ValidateTemperature(value);
        }

        private void OnMaxTokensChanged(int value)
        {
            ValidateMaxTokens(value);
        }

        private void OnSelectedThemeChanged(string value)
        {
            IsDarkMode = value?.Contains("Dark", StringComparison.OrdinalIgnoreCase) == true;

            // Apply theme globally using SfSkinManager.ApplicationTheme
            // Reference: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
            try
            {
                if (!string.IsNullOrEmpty(value))
                {
                    SfSkinManager.ApplicationTheme = new Theme(value);
                    _logger.LogInformation("Theme changed to: {Theme}", value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme: {Theme}", value);
            }
        }

        private int windowWidth = 1200;
        [Category("General")]
        [DisplayName("Window Width")]
        [Description("Default window width in pixels (800-3840)")]
        [Range(800, 3840)]
        public int WindowWidth
        {
            get => windowWidth;
            set
            {
                if (SetProperty(ref windowWidth, value))
                {
                    try { OnWindowWidthChanged(value); } catch { }
                }
            }
        }

        private int windowHeight = 800;
        [Category("General")]
        [DisplayName("Window Height")]
        [Description("Default window height in pixels (600-2160)")]
        [Range(600, 2160)]
        public int WindowHeight
        {
            get => windowHeight;
            set
            {
                if (SetProperty(ref windowHeight, value))
                {
                    try { OnWindowHeightChanged(value); } catch { }
                }
            }
        }

        private bool maximizeOnStartup;
        [Category("General")]
        [DisplayName("Maximize on Startup")]
        [Description("Maximize the window when the application starts")]
        public bool MaximizeOnStartup
        {
            get => maximizeOnStartup;
            set => SetProperty(ref maximizeOnStartup, value);
        }

        private bool showSplashScreen = true;
        [Category("General")]
        [DisplayName("Show Splash Screen")]
        [Description("Display the splash screen during application startup")]
        public bool ShowSplashScreen
        {
            get => showSplashScreen;
            set => SetProperty(ref showSplashScreen, value);
        }

        // Database Settings
        private string databaseConnectionString;
        [Category("Database")]
        [DisplayName("Connection String")]
        [Description("Database connection string (read-only)")]
        [ReadOnly(true)]
        public string DatabaseConnectionString
        {
            get => databaseConnectionString;
            set => SetProperty(ref databaseConnectionString, value);
        }

        private string databaseStatus = "Checking...";
        [Category("Database")]
        [DisplayName("Status")]
        [Description("Current database connection status")]
        [ReadOnly(true)]
        public string DatabaseStatus
        {
            get => databaseStatus;
            set => SetProperty(ref databaseStatus, value);
        }

        private Brush databaseStatusColor = Brushes.Orange;
        [Category("Database")]
        [Browsable(false)]
        public Brush DatabaseStatusColor
        {
            get => databaseStatusColor;
            set => SetProperty(ref databaseStatusColor, value);
        }

        // QuickBooks Settings
        private string quickBooksClientId;
        [Category("QuickBooks")]
        [DisplayName("Client ID")]
        [Description("QuickBooks OAuth2 Client ID from Intuit Developer Portal")]
        public string QuickBooksClientId
        {
            get => quickBooksClientId;
            set
            {
                if (SetProperty(ref quickBooksClientId, value))
                {
                    try { OnQuickBooksClientIdChanged(value); } catch { }
                }
            }
        }

        private string quickBooksClientSecret;
        [Category("QuickBooks")]
        [DisplayName("Client Secret")]
        [Description("QuickBooks OAuth2 Client Secret (securely stored)")]
        [PasswordPropertyText(true)]
        public string QuickBooksClientSecret
        {
            get => quickBooksClientSecret;
            set => SetProperty(ref quickBooksClientSecret, value);
        }

        private string quickBooksRedirectUri;
        [Category("QuickBooks")]
        [DisplayName("Redirect URI")]
        [Description("OAuth2 redirect URI (e.g., https://localhost:5001/callback)")]
        public string QuickBooksRedirectUri
        {
            get => quickBooksRedirectUri;
            set => SetProperty(ref quickBooksRedirectUri, value);
        }

        private ObservableCollection<string> quickBooksEnvironments = new() { "Sandbox", "Production" };
        [Category("QuickBooks")]
        [Browsable(false)]
        public ObservableCollection<string> QuickBooksEnvironments
        {
            get => quickBooksEnvironments;
            set => SetProperty(ref quickBooksEnvironments, value);
        }

        private string selectedQuickBooksEnvironment = "Sandbox";
        [Category("QuickBooks")]
        [DisplayName("Environment")]
        [Description("Select Sandbox for testing or Production for live data")]
        public string SelectedQuickBooksEnvironment
        {
            get => selectedQuickBooksEnvironment;
            set => SetProperty(ref selectedQuickBooksEnvironment, value);
        }

        private string quickBooksConnectionStatus = "Not Connected";
        [Category("QuickBooks")]
        [DisplayName("Connection Status")]
        [Description("Current QuickBooks connection status")]
        [ReadOnly(true)]
        public string QuickBooksConnectionStatus
        {
            get => quickBooksConnectionStatus;
            set => SetProperty(ref quickBooksConnectionStatus, value);
        }

        private Brush quickBooksStatusColor = Brushes.Red;
        [Category("QuickBooks")]
        [Browsable(false)]
        public Brush QuickBooksStatusColor
        {
            get => quickBooksStatusColor;
            set => SetProperty(ref quickBooksStatusColor, value);
        }

        // QuickBooks URL ACL readiness
        private string quickBooksUrlAclStatus = "Unknown";
        [Category("QuickBooks")]
        [DisplayName("OAuth Callback URL Status")]
        [Description("Checks if the local HTTP listener URL is permitted (URL ACL). Required for OAuth callback.")]
        [ReadOnly(true)]
        public string QuickBooksUrlAclStatus
        {
            get => quickBooksUrlAclStatus;
            set => SetProperty(ref quickBooksUrlAclStatus, value);
        }

        private Brush quickBooksUrlAclStatusColor = Brushes.Orange;
        [Browsable(false)]
        public Brush QuickBooksUrlAclStatusColor
        {
            get => quickBooksUrlAclStatusColor;
            set => SetProperty(ref quickBooksUrlAclStatusColor, value);
        }

        // Syncfusion License
        private string syncfusionLicenseKey;
        [Category("Syncfusion")]
        [DisplayName("License Key")]
        [Description("Syncfusion license key (get from syncfusion.com/account)")]
        [PasswordPropertyText(true)]
        public string SyncfusionLicenseKey
        {
            get => syncfusionLicenseKey;
            set
            {
                if (SetProperty(ref syncfusionLicenseKey, value))
                {
                    try { OnSyncfusionLicenseKeyChanged(value); } catch { }
                }
            }
        }

        private string syncfusionLicenseStatus = "Checking...";
        [Category("Syncfusion")]
        [DisplayName("License Status")]
        [Description("Current license validation status")]
        [ReadOnly(true)]
        public string SyncfusionLicenseStatus
        {
            get => syncfusionLicenseStatus;
            set => SetProperty(ref syncfusionLicenseStatus, value);
        }

        private Brush syncfusionLicenseStatusColor = Brushes.Orange;
        [Category("Syncfusion")]
        [Browsable(false)]
        public Brush SyncfusionLicenseStatusColor
        {
            get => syncfusionLicenseStatusColor;
            set => SetProperty(ref syncfusionLicenseStatusColor, value);
        }

        // XAI Settings
        private string xaiApiKey;
        [Category("XAI")]
        [DisplayName("API Key")]
        [Description("XAI API key for AI functionality (format: xai-xxxxxxxx)")]
        [PasswordPropertyText(true)]
        public string XaiApiKey
        {
            get => xaiApiKey;
            set
            {
                if (SetProperty(ref xaiApiKey, value))
                {
                    try { OnXaiApiKeyChanged(value); } catch { }
                }
            }
        }

        private string xaiBaseUrl = "https://api.x.ai/v1/";
        [Category("XAI")]
        [DisplayName("Base URL")]
        [Description("XAI API base URL (default: https://api.x.ai/v1/)")]
        public string XaiBaseUrl
        {
            get => xaiBaseUrl;
            set => SetProperty(ref xaiBaseUrl, value);
        }

        private int xaiTimeoutSeconds = 15;
        [Category("XAI")]
        [DisplayName("Timeout (seconds)")]
        [Description("Maximum time to wait for AI responses (5-300 seconds)")]
        [Range(5, 300)]
        public int XaiTimeoutSeconds
        {
            get => xaiTimeoutSeconds;
            set
            {
                if (SetProperty(ref xaiTimeoutSeconds, value))
                {
                    try { OnXaiTimeoutSecondsChanged(value); } catch { }
                }
            }
        }

        private ObservableCollection<string> availableModels = new() { "grok-4-0709", "grok-beta", "grok-1" };
        [Category("XAI")]
        [Browsable(false)]
        public ObservableCollection<string> AvailableModels
        {
            get => availableModels;
            set => SetProperty(ref availableModels, value);
        }

        private ObservableCollection<string> availableResponseStyles = new() { "Balanced", "Creative", "Precise", "Concise" };
        [Category("XAI")]
        [Browsable(false)]
        public ObservableCollection<string> AvailableResponseStyles
        {
            get => availableResponseStyles;
            set => SetProperty(ref availableResponseStyles, value);
        }

        private ObservableCollection<string> availablePersonalities = new() { "Professional", "Friendly", "Technical", "Casual" };
        [Category("XAI")]
        [Browsable(false)]
        public ObservableCollection<string> AvailablePersonalities
        {
            get => availablePersonalities;
            set => SetProperty(ref availablePersonalities, value);
        }

        private string xaiModel = "grok-4-0709";
        [Category("XAI")]
        [DisplayName("Model")]
        [Description("AI model to use (grok-4-0709 recommended)")]
        public string XaiModel
        {
            get => xaiModel;
            set => SetProperty(ref xaiModel, value);
        }

        private string responseStyle = "Balanced";
        [Category("XAI")]
        [DisplayName("Response Style")]
        [Description("How detailed and structured AI responses should be")]
        public string ResponseStyle
        {
            get => responseStyle;
            set => SetProperty(ref responseStyle, value);
        }

        private string personality = "Professional";
        [Category("XAI")]
        [DisplayName("Personality")]
        [Description("Communication style for AI responses")]
        public string Personality
        {
            get => personality;
            set => SetProperty(ref personality, value);
        }

        private int contextWindowSize = 4096;
        [Category("XAI")]
        [DisplayName("Context Window Size")]
        [Description("Maximum tokens AI can process (1024-32768)")]
        [Range(1024, 32768)]
        public int ContextWindowSize
        {
            get => contextWindowSize;
            set
            {
                if (SetProperty(ref contextWindowSize, value))
                {
                    try { OnContextWindowSizeChanged(value); } catch { }
                }
            }
        }

        private bool enableSafetyFilters = true;
        [Category("XAI")]
        [DisplayName("Enable Safety Filters")]
        [Description("Enable content safety filtering")]
        public bool EnableSafetyFilters
        {
            get => enableSafetyFilters;
            set => SetProperty(ref enableSafetyFilters, value);
        }

        private double temperature = 0.7;
        [Category("XAI")]
        [DisplayName("Temperature")]
        [Description("Response randomness (0.0-2.0, lower = more focused)")]
        [Range(0.0, 2.0)]
        public double Temperature
        {
            get => temperature;
            set
            {
                if (SetProperty(ref temperature, value))
                {
                    try { OnTemperatureChanged(value); } catch { }
                }
            }
        }

        private int maxTokens = 2048;
        [Category("XAI")]
        [DisplayName("Max Tokens")]
        [Description("Maximum tokens in response (1-4096)")]
        [Range(1, 4096)]
        public int MaxTokens
        {
            get => maxTokens;
            set
            {
                if (SetProperty(ref maxTokens, value))
                {
                    try { OnMaxTokensChanged(value); } catch { }
                }
            }
        }

        private bool enableStreaming = false;
        [Category("XAI")]
        [DisplayName("Enable Streaming")]
        [Description("Stream responses in real-time")]
        public bool EnableStreaming
        {
            get => enableStreaming;
            set => SetProperty(ref enableStreaming, value);
        }

        private string temperatureValidation = string.Empty;
        [Browsable(false)]
        public string TemperatureValidation
        {
            get => temperatureValidation;
            set => SetProperty(ref temperatureValidation, value);
        }

        private string maxTokensValidation = string.Empty;
        [Browsable(false)]
        public string MaxTokensValidation
        {
            get => maxTokensValidation;
            set => SetProperty(ref maxTokensValidation, value);
        }

        private string xaiConnectionStatus = "Not Configured";
        [Category("XAI")]
        [DisplayName("Connection Status")]
        [Description("Current XAI connection status")]
        [ReadOnly(true)]
        public string XaiConnectionStatus
        {
            get => xaiConnectionStatus;
            set => SetProperty(ref xaiConnectionStatus, value);
        }

        private Brush xaiStatusColor = Brushes.Orange;
        [Browsable(false)]
        public Brush XaiStatusColor
        {
            get => xaiStatusColor;
            set => SetProperty(ref xaiStatusColor, value);
        }

        private bool isXaiKeyValidated;
        [Browsable(false)]
        public bool IsXaiKeyValidated
        {
            get => isXaiKeyValidated;
            set => SetProperty(ref isXaiKeyValidated, value);
        }

        private string xaiValidationMessage = string.Empty;
        [Browsable(false)]
        public string XaiValidationMessage
        {
            get => xaiValidationMessage;
            set => SetProperty(ref xaiValidationMessage, value);
        }

        // Fiscal Year Settings
        private ObservableCollection<MonthOption> fiscalYearMonths = new()
        {
            new MonthOption { Name = "January", Value = 1 },
            new MonthOption { Name = "February", Value = 2 },
            new MonthOption { Name = "March", Value = 3 },
            new MonthOption { Name = "April", Value = 4 },
            new MonthOption { Name = "May", Value = 5 },
            new MonthOption { Name = "June", Value = 6 },
            new MonthOption { Name = "July (Common)", Value = 7 },
            new MonthOption { Name = "August", Value = 8 },
            new MonthOption { Name = "September", Value = 9 },
            new MonthOption { Name = "October", Value = 10 },
            new MonthOption { Name = "November", Value = 11 },
            new MonthOption { Name = "December", Value = 12 }
        };
        [Category("Fiscal Year")]
        [Browsable(false)]
        public ObservableCollection<MonthOption> FiscalYearMonths
        {
            get => fiscalYearMonths;
            set => SetProperty(ref fiscalYearMonths, value);
        }

        private int fiscalYearStartMonth = 7; // Default to July
        [Category("Fiscal Year")]
        [DisplayName("Start Month")]
        [Description("Month when fiscal year begins (1-12)")]
        [Range(1, 12)]
        public int FiscalYearStartMonth
        {
            get => fiscalYearStartMonth;
            set => SetProperty(ref fiscalYearStartMonth, value);
        }

        private int fiscalYearStartDay = 1;
        [Category("Fiscal Year")]
        [DisplayName("Start Day")]
        [Description("Day of month when fiscal year begins (1-31)")]
        [Range(1, 31)]
        public int FiscalYearStartDay
        {
            get => fiscalYearStartDay;
            set
            {
                if (SetProperty(ref fiscalYearStartDay, value))
                {
                    try { OnFiscalYearStartDayChanged(value); } catch { }
                }
            }
        }

    private string currentFiscalYearDisplay = "Loading...";
        [Category("Fiscal Year")]
        [DisplayName("Current Fiscal Year")]
        [Description("Current fiscal year period")]
        [ReadOnly(true)]
        public string CurrentFiscalYearDisplay
        {
            get => currentFiscalYearDisplay;
            set => SetProperty(ref currentFiscalYearDisplay, value);
        }

        private string fiscalYearPeriodDisplay = "Loading...";
        [Category("Fiscal Year")]
        [DisplayName("Fiscal Year Period")]
        [Description("Date range of current fiscal year")]
        [ReadOnly(true)]
        public string FiscalYearPeriodDisplay
        {
            get => fiscalYearPeriodDisplay;
            set => SetProperty(ref fiscalYearPeriodDisplay, value);
        }

        private int daysRemainingInFiscalYear;
        [Category("Fiscal Year")]
        [DisplayName("Days Remaining")]
        [Description("Days remaining in current fiscal year")]
        [ReadOnly(true)]
        public int DaysRemainingInFiscalYear
        {
            get => daysRemainingInFiscalYear;
            set => SetProperty(ref daysRemainingInFiscalYear, value);
        }

        private ObservableCollection<string> availableFiscalYears = new();
        [Category("Fiscal Year")]
        [Browsable(false)]
        public ObservableCollection<string> AvailableFiscalYears
        {
            get => availableFiscalYears;
            set => SetProperty(ref availableFiscalYears, value);
        }

        // Advanced Settings
        private bool enableDynamicColumns = true;
        [Category("Advanced")]
        [DisplayName("Enable Dynamic Columns")]
        [Description("Enable dynamic column generation in data grids")]
        public bool EnableDynamicColumns
        {
            get => enableDynamicColumns;
            set => SetProperty(ref enableDynamicColumns, value);
        }

        private bool enableDataCaching = true;
        [Category("Advanced")]
        [DisplayName("Enable Data Caching")]
        [Description("Cache data to improve performance")]
        public bool EnableDataCaching
        {
            get => enableDataCaching;
            set => SetProperty(ref enableDataCaching, value);
        }

        private int cacheExpirationMinutes = 30;
        [Category("Advanced")]
        [DisplayName("Cache Expiration (minutes)")]
        [Description("How long to keep cached data (1-1440 minutes)")]
        [Range(1, 1440)]
        public int CacheExpirationMinutes
        {
            get => cacheExpirationMinutes;
            set
            {
                if (SetProperty(ref cacheExpirationMinutes, value))
                {
                    try { OnCacheExpirationMinutesChanged(value); } catch { }
                }
            }
        }

        private ObservableCollection<string> logLevels = new() { "Debug", "Information", "Warning", "Error", "Critical" };
        [Category("Advanced")]
        [Browsable(false)]
        public ObservableCollection<string> LogLevels
        {
            get => logLevels;
            set => SetProperty(ref logLevels, value);
        }

        private string selectedLogLevel = "Information";
        [Category("Advanced")]
        [DisplayName("Log Level")]
        [Description("Minimum log level (Debug for dev, Information for prod)")]
        public string SelectedLogLevel
        {
            get => selectedLogLevel;
            set => SetProperty(ref selectedLogLevel, value);
        }

        private bool enableFileLogging = true;
        [Category("Advanced")]
        [DisplayName("Enable File Logging")]
        [Description("Write logs to file")]
        public bool EnableFileLogging
        {
            get => enableFileLogging;
            set => SetProperty(ref enableFileLogging, value);
        }

        private string logFilePath = "logs/wiley-widget.log";
        [Category("Advanced")]
        [DisplayName("Log File Path")]
        [Description("Path to the log file")]
        public string LogFilePath
        {
            get => logFilePath;
            set => SetProperty(ref logFilePath, value);
        }

        // Status
        private string settingsStatus = "Ready";
        public string SettingsStatus
        {
            get => settingsStatus;
            set => SetProperty(ref settingsStatus, value);
        }

        private string lastSaved = "Never";
        public string LastSaved
        {
            get => lastSaved;
            set => SetProperty(ref lastSaved, value);
        }

        private string systemInfo;
        public string SystemInfo
        {
            get => systemInfo;
            set => SetProperty(ref systemInfo, value);
        }

        // Validation Properties
        private string windowWidthValidation = string.Empty;
        public string WindowWidthValidation
        {
            get => windowWidthValidation;
            set => SetProperty(ref windowWidthValidation, value);
        }

        private string windowHeightValidation = string.Empty;
        public string WindowHeightValidation
        {
            get => windowHeightValidation;
            set => SetProperty(ref windowHeightValidation, value);
        }

        private string xaiApiKeyValidation = string.Empty;
        public string XaiApiKeyValidation
        {
            get => xaiApiKeyValidation;
            set => SetProperty(ref xaiApiKeyValidation, value);
        }

        private string xaiTimeoutValidation = string.Empty;
        public string XaiTimeoutValidation
        {
            get => xaiTimeoutValidation;
            set => SetProperty(ref xaiTimeoutValidation, value);
        }

        private string contextWindowValidation = string.Empty;
        public string ContextWindowValidation
        {
            get => contextWindowValidation;
            set => SetProperty(ref contextWindowValidation, value);
        }

        private string cacheExpirationValidation = string.Empty;
        public string CacheExpirationValidation
        {
            get => cacheExpirationValidation;
            set => SetProperty(ref cacheExpirationValidation, value);
        }

        private string fiscalYearDayValidation = string.Empty;
        public string FiscalYearDayValidation
        {
            get => fiscalYearDayValidation;
            set => SetProperty(ref fiscalYearDayValidation, value);
        }

        private string quickBooksClientIdValidation = string.Empty;
        public string QuickBooksClientIdValidation
        {
            get => quickBooksClientIdValidation;
            set => SetProperty(ref quickBooksClientIdValidation, value);
        }

        private string quickBooksClientSecretValidation = string.Empty;
        [Browsable(false)]
        public string QuickBooksClientSecretValidation
        {
            get => quickBooksClientSecretValidation;
            set => SetProperty(ref quickBooksClientSecretValidation, value);
        }

        private string quickBooksRedirectUriValidation = string.Empty;
        [Browsable(false)]
        public string QuickBooksRedirectUriValidation
        {
            get => quickBooksRedirectUriValidation;
            set => SetProperty(ref quickBooksRedirectUriValidation, value);
        }

        private string syncfusionLicenseKeyValidation = string.Empty;
        [Browsable(false)]
        public string SyncfusionLicenseKeyValidation
        {
            get => syncfusionLicenseKeyValidation;
            set => SetProperty(ref syncfusionLicenseKeyValidation, value);
        }

        // UI State
        private bool isBusy;
        [Browsable(false)]
        public bool IsBusy
        {
            get => isBusy;
            set => SetProperty(ref isBusy, value);
        }

        private string busyMessage;
        [Browsable(false)]
        public string BusyMessage
        {
            get => busyMessage;
            set => SetProperty(ref busyMessage, value);
        }

        // Search and Filter
        private string searchText = string.Empty;
        [Browsable(false)]
        public string SearchText
        {
            get => searchText;
            set => SetProperty(ref searchText, value);
        }

        private bool showAdvancedSettings = true;
        [Browsable(false)]
        public bool ShowAdvancedSettings
        {
            get => showAdvancedSettings;
            set => SetProperty(ref showAdvancedSettings, value);
        }

        public bool HasUnsavedChanges { get; private set; }

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            IOptions<AppOptions> appOptions,
            IOptionsMonitor<AppOptions> appOptionsMonitor,
            IUnitOfWork unitOfWork,
            AppDbContext dbContext,
            ISecretVaultService secretVaultService,
            IQuickBooksService? quickBooksService = null,
            ISyncfusionLicenseService syncfusionLicenseService = null!,
            IAIService aiService = null!,
            IAuditService auditService = null!,
            ISettingsService settingsService = null!,
            Prism.Dialogs.IDialogService dialogService = null!)
        {
            // Validate required dependencies
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appOptions = appOptions ?? throw new ArgumentNullException(nameof(appOptions));
            _appOptionsMonitor = appOptionsMonitor ?? throw new ArgumentNullException(nameof(appOptionsMonitor));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _secretVaultService = secretVaultService ?? throw new ArgumentNullException(nameof(secretVaultService));
            _quickBooksService = quickBooksService; // Null OK; check in methods
            _syncfusionLicenseService = syncfusionLicenseService ?? throw new ArgumentNullException(nameof(syncfusionLicenseService));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            // NOTE: ThemeManager removed - SfSkinManager.ApplicationTheme handles theming globally
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // Initialize system info
            SystemInfo = $"OS: {Environment.OSVersion}\n" +
                        $".NET Version: {Environment.Version}\n" +
                        $"Machine: {Environment.MachineName}\n" +
                        $"User: {Environment.UserName}";

            // Set up property change tracking for unsaved changes
            PropertyChanged += OnPropertyChanged;

            // Initialize Prism DelegateCommands
            SaveSettingsCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteSaveSettingsAsync(), () => !IsBusy && !HasErrors)
                .ObservesProperty(() => IsBusy)
                .ObservesProperty(() => HasErrors);
            ResetSettingsCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteResetSettingsAsync(), () => !IsBusy);
            TestConnectionCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteTestConnectionAsync(), () => !IsBusy);
            TestQuickBooksConnectionCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteTestQuickBooksConnectionAsync(), () => !IsBusy);
            ConnectQuickBooksCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteConnectQuickBooksAsync(), () => !IsBusy);
            CheckQuickBooksUrlAclCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteCheckQuickBooksUrlAclAsync(), () => !IsBusy);
            ValidateLicenseCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteValidateLicenseAsync(), () => !IsBusy);
            TestXaiConnectionCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteTestXaiConnectionAsync(), () => !IsBusy);
            ValidateXaiKeyCommand = new Prism.Commands.DelegateCommand<string>(async (key) => await ExecuteValidateXaiKeyAsync(key), (key) => !IsBusy);
            ValidateAndSaveXaiKeyCommand = new Prism.Commands.DelegateCommand<string>(async (key) => await ExecuteValidateAndSaveXaiKeyAsync(key), (key) => !IsBusy);
            OpenActivateXaiDialogCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteOpenActivateXaiDialogAsync(), () => !IsBusy);
            OpenXaiConsoleCommand = new Prism.Commands.DelegateCommand(() => OpenXaiConsolePublic());
            SaveFiscalYearSettingsCommand = new Prism.Commands.DelegateCommand(async () => await SaveFiscalYearSettingsAsync(), () => !IsBusy);
            ImportBudgetsCommand = new Prism.Commands.DelegateCommand(async () => await ImportBudgetsAsync(), () => !IsBusy);
        }

        // Validation rules (simple length checks). Update as APIs require.
        private const int XaiApiKeyExpectedMinLength = 20;
        private const int XaiApiKeyExpectedMaxLength = 128;

        private const int QuickBooksClientIdMinLength = 10;
        private const int QuickBooksClientSecretMinLength = 20;

        private static readonly System.Text.RegularExpressions.Regex QuickBooksGuidRegex =
            new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        // Syncfusion license keys are often long base64-like strings; allow base64 character set and common separators
        private static readonly System.Text.RegularExpressions.Regex SyncfusionBase64LikeRegex =
            new(@"^[A-Za-z0-9+/=\-_]{20,}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private void OnQuickBooksClientIdChanged(string value)
        {
            ClearErrors(nameof(QuickBooksClientId));
            if (string.IsNullOrWhiteSpace(value) || value.Length < QuickBooksClientIdMinLength)
            {
                AddError(nameof(QuickBooksClientId), $"QuickBooks Client ID must be at least {QuickBooksClientIdMinLength} characters.");
                try
                {
                    var envPath = System.IO.Path.Combine(AppContext.BaseDirectory, ".env");
                    var tmpPath = envPath + ".tmp";
                    var line = $"XAI_API_KEY={XaiApiKey}\n";
                    System.IO.File.WriteAllText(tmpPath, line);
                    if (System.IO.File.Exists(envPath))
                        System.IO.File.Replace(tmpPath, envPath, null);
                    else
                        System.IO.File.Move(tmpPath, envPath);

                    // Apply best-effort file ACL restriction on Windows
                    try
                    {
                        var applied = FileSecurityHelper.RestrictFileToCurrentUser(envPath);
                        if (!applied)
                            _logger.LogWarning("File ACL restriction not applied to .env (platform or permission issue)");
                        else
                            _logger.LogInformation("Successfully restricted .env file access to current user only");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to apply file ACLs to .env (non-fatal)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write .env entry for XAI_API_KEY (non-fatal)");
                }
                AddError(nameof(QuickBooksClientSecret), $"QuickBooks Client Secret must be at least {QuickBooksClientSecretMinLength} characters.");
            }
        }

        private void OnXaiApiKeyChanged(string value)
        {
            ClearErrors(nameof(XaiApiKey));
            if (!string.IsNullOrEmpty(value))
            {
                if (value.Length < XaiApiKeyExpectedMinLength || value.Length > XaiApiKeyExpectedMaxLength)
                {
                    AddError(nameof(XaiApiKey), $"XAI API key length must be between {XaiApiKeyExpectedMinLength} and {XaiApiKeyExpectedMaxLength} characters.");
                    return;
                }

                // Allow optional 'sk-' prefix used by some providers (e.g., OpenAI-like patterns)
                if (value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
                {
                    // ensure the rest is at least the minimum length
                    var rest = value.Substring(3);
                    if (rest.Length < (XaiApiKeyExpectedMinLength - 3))
                        AddError(nameof(XaiApiKey), "XAI API key appears too short after 'sk-' prefix.");
                }
            }
        }

        // Provider guidance texts (can be localized later)
        public string GetXaiActivationGuidance()
        {
            return "Steps to activate xAI API key:\n" +
                   "1. Open the xAI Console (opens in your browser).\n" +
                   "2. Create a new API key under 'API Keys'.\n" +
                   "3. If prompted, ensure billing/credits are enabled for your account.\n" +
                   "4. Copy the key and paste it into the app. Click 'Validate' to check it.\n" +
                   "Troubleshooting:\n" +
                   "• 403 / AuthFailure: The key exists but lacks permission or wasn't activated. Open console and check key status.\n" +
                   "• 402 / Payment Required: Enable billing or add credits.\n" +
                   "• 429 / RateLimited: Too many requests; try again later.";
        }

        private void OnSyncfusionLicenseKeyChanged(string value)
        {
            ClearErrors(nameof(SyncfusionLicenseKey));
            if (!string.IsNullOrEmpty(value))
            {
                if (value.Length < 20)
                {
                    AddError(nameof(SyncfusionLicenseKey), "Syncfusion license key appears to be too short.");
                    return;
                }

                if (!SyncfusionBase64LikeRegex.IsMatch(value))
                {
                    AddError(nameof(SyncfusionLicenseKey), "Syncfusion license key format looks invalid. It should be a long Base64-like token.");
                }
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HasUnsavedChanges) &&
                e.PropertyName != nameof(SettingsStatus) &&
                e.PropertyName != nameof(LastSaved))
            {
                HasUnsavedChanges = true;
            }
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                SettingsStatus = "Loading settings...";

                // Load from configuration and database
                await LoadGeneralSettingsAsync();
                await LoadDatabaseSettingsAsync();
                await LoadQuickBooksSettingsAsync();
                await LoadSyncfusionSettingsAsync();
                await LoadXaiSettingsAsync();
                await LoadAdvancedSettingsAsync();
                await LoadFiscalYearDisplayAsync();

                SettingsStatus = "Settings loaded successfully";
                HasUnsavedChanges = false;
                LastSaved = DateTime.Now.ToString("g", CultureInfo.InvariantCulture);

                _logger.LogInformation("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                SettingsStatus = "Error loading settings";
                _logger.LogError(ex, "Error loading settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadGeneralSettingsAsync()
        {
            try
            {
                // Load from options (which are configured from database and configuration)
                var options = _appOptions.Value;
                SelectedTheme = options.Theme;
                WindowWidth = options.WindowWidth;
                WindowHeight = options.WindowHeight;
                MaximizeOnStartup = options.MaximizeOnStartup;
                ShowSplashScreen = options.ShowSplashScreen;
                IsDarkMode = options.IsDarkMode;

                // Also load from database for any additional settings not in options
                var settings = await _dbContext.AppSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
                if (settings != null)
                {
                    // Override with database values if they exist
                    SelectedTheme = settings.Theme ?? options.Theme;
                    WindowWidth = (int)(settings.WindowWidth ?? options.WindowWidth);
                    WindowHeight = (int)(settings.WindowHeight ?? options.WindowHeight);
                    MaximizeOnStartup = settings.WindowMaximized ?? options.MaximizeOnStartup;
                    IsDarkMode = SelectedTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load general settings");
                // Fall back to defaults
                SelectedTheme = "FluentDark";
                WindowWidth = 1200;
                WindowHeight = 800;
                MaximizeOnStartup = false;
                ShowSplashScreen = true;
                IsDarkMode = true;
            }
        }

        private async Task LoadDatabaseSettingsAsync()
        {
            try
            {
                DatabaseConnectionString = "Connection string not available in this EF Core version";

                // Test database connection
                var canConnect = await _dbContext.Database.CanConnectAsync();
                DatabaseStatus = canConnect ? "Connected" : "Connection Failed";
                DatabaseStatusColor = canConnect ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                DatabaseStatus = $"Error: {ex.Message}";
                DatabaseStatusColor = Brushes.Red;
            }
        }

        private async Task LoadQuickBooksSettingsAsync()
        {
            try
            {
                // Load QuickBooks settings from encrypted secret vault
                QuickBooksClientId = await _secretVaultService.GetSecretAsync("QuickBooks-ClientId") ?? "";
                QuickBooksClientSecret = await _secretVaultService.GetSecretAsync("QuickBooks-ClientSecret") ?? "";
                QuickBooksRedirectUri = await _secretVaultService.GetSecretAsync("QuickBooks-RedirectUri") ?? "";
                SelectedQuickBooksEnvironment = await _secretVaultService.GetSecretAsync("QuickBooks-Environment") ?? "Sandbox";

                // Test connection if credentials are available
                if (!string.IsNullOrEmpty(QuickBooksClientId))
                {
                    var isConnected = await _quickBooksService.TestConnectionAsync();
                    QuickBooksConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                    QuickBooksStatusColor = isConnected ? Brushes.Green : Brushes.Red;

                    // Also check URL ACL readiness for a smoother auth flow
                    await ExecuteCheckQuickBooksUrlAclAsync();
                }
                else
                {
                    QuickBooksConnectionStatus = "Not Configured";
                    QuickBooksStatusColor = Brushes.Orange;
                    QuickBooksUrlAclStatus = "Not Checked";
                    QuickBooksUrlAclStatusColor = Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = $"Error: {ex.Message}";
                QuickBooksStatusColor = Brushes.Red;
            }
        }

        private async Task LoadSyncfusionSettingsAsync()
        {
            try
            {
                // Load Syncfusion license from encrypted secret vault
                SyncfusionLicenseKey = await _secretVaultService.GetSecretAsync("Syncfusion-LicenseKey") ?? "";

                // Simple license validation - check if key exists and is not empty
                var isValid = !string.IsNullOrEmpty(SyncfusionLicenseKey);
                SyncfusionLicenseStatus = isValid ? "Valid" : "Invalid or Missing";
                SyncfusionLicenseStatusColor = isValid ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                SyncfusionLicenseStatus = $"Error: {ex.Message}";
                SyncfusionLicenseStatusColor = Brushes.Red;
            }
        }

        private async Task LoadXaiSettingsAsync()
        {
            try
            {
                // Load XAI settings from encrypted secret vault
                XaiApiKey = await _secretVaultService.GetSecretAsync("XAI-ApiKey") ?? "";
                XaiBaseUrl = await _secretVaultService.GetSecretAsync("XAI-BaseUrl") ?? "https://api.x.ai/v1/";
                XaiModel = await _secretVaultService.GetSecretAsync("XAI-Model") ?? "grok-4-0709";

                // Parse timeout from vault
                var timeoutStr = await _secretVaultService.GetSecretAsync("XAI-TimeoutSeconds");
                if (int.TryParse(timeoutStr, out var timeout))
                {
                    XaiTimeoutSeconds = timeout;
                }
                else
                {
                    XaiTimeoutSeconds = 15;
                }

                // Load additional AI settings from vault
                ResponseStyle = await _secretVaultService.GetSecretAsync("XAI-ResponseStyle") ?? "Balanced";
                Personality = await _secretVaultService.GetSecretAsync("XAI-Personality") ?? "Professional";

                var contextSizeStr = await _secretVaultService.GetSecretAsync("XAI-ContextWindowSize");
                if (int.TryParse(contextSizeStr, out var contextSize))
                {
                    ContextWindowSize = contextSize;
                }
                else
                {
                    ContextWindowSize = 4096;
                }

                var safetyFiltersStr = await _secretVaultService.GetSecretAsync("XAI-EnableSafetyFilters");
                if (bool.TryParse(safetyFiltersStr, out var safetyFilters))
                {
                    EnableSafetyFilters = safetyFilters;
                }
                else
                {
                    EnableSafetyFilters = true;
                }

                var tempStr = await _secretVaultService.GetSecretAsync("XAI-Temperature");
                if (double.TryParse(tempStr, out var temp))
                {
                    Temperature = temp;
                }
                else
                {
                    Temperature = 0.7;
                }

                var maxTokStr = await _secretVaultService.GetSecretAsync("XAI-MaxTokens");
                if (int.TryParse(maxTokStr, out var maxTok))
                {
                    MaxTokens = maxTok;
                }
                else
                {
                    MaxTokens = 2048;
                }

                var streamingStr = await _secretVaultService.GetSecretAsync("XAI-EnableStreaming");
                if (bool.TryParse(streamingStr, out var streaming))
                {
                    EnableStreaming = streaming;
                }
                else
                {
                    EnableStreaming = false;
                }

                // Test connection if API key is configured
                if (!string.IsNullOrEmpty(XaiApiKey))
                {
                    var isConnected = await TestXaiConnectionInternalAsync();
                    XaiConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                    XaiStatusColor = isConnected ? Brushes.Green : Brushes.Red;
                }
                else
                {
                    XaiConnectionStatus = "Not Configured";
                    XaiStatusColor = Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                XaiConnectionStatus = $"Error: {ex.Message}";
                XaiStatusColor = Brushes.Red;
            }
        }

        private async Task LoadAdvancedSettingsAsync()
        {
            try
            {
                // Load advanced settings from database
                var settings = await _dbContext.AppSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
                if (settings != null)
                {
                    EnableDynamicColumns = settings.UseDynamicColumns;
                    EnableDataCaching = settings.EnableDataCaching;
                    CacheExpirationMinutes = settings.CacheExpirationMinutes;
                    SelectedLogLevel = settings.SelectedLogLevel ?? "Information";
                    EnableFileLogging = settings.EnableFileLogging;
                    LogFilePath = settings.LogFilePath ?? "logs/wiley-widget.log";
                }
                else
                {
                    // Use default values if no settings exist
                    EnableDynamicColumns = true;
                    EnableDataCaching = true;
                    CacheExpirationMinutes = 30;
                    SelectedLogLevel = "Information";
                    EnableFileLogging = true;
                    LogFilePath = "logs/wiley-widget.log";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load advanced settings from database");
                // Fall back to defaults
                EnableDynamicColumns = true;
                EnableDataCaching = true;
                CacheExpirationMinutes = 30;
                SelectedLogLevel = "Information";
                EnableFileLogging = true;
                LogFilePath = "logs/wiley-widget.log";
            }
        }

        public Prism.Commands.DelegateCommand SaveSettingsCommand { get; private set; }
        public Prism.Commands.DelegateCommand ResetSettingsCommand { get; private set; }
        public Prism.Commands.DelegateCommand TestConnectionCommand { get; private set; }
        public Prism.Commands.DelegateCommand TestQuickBooksConnectionCommand { get; private set; }
        public Prism.Commands.DelegateCommand ConnectQuickBooksCommand { get; private set; }
        public Prism.Commands.DelegateCommand ValidateLicenseCommand { get; private set; }
        public Prism.Commands.DelegateCommand TestXaiConnectionCommand { get; private set; }
        public Prism.Commands.DelegateCommand<string> ValidateXaiKeyCommand { get; private set; }
        public Prism.Commands.DelegateCommand<string> ValidateAndSaveXaiKeyCommand { get; private set; }
        public Prism.Commands.DelegateCommand OpenActivateXaiDialogCommand { get; private set; }
        public Prism.Commands.DelegateCommand SaveFiscalYearSettingsCommand { get; private set; }
        public Prism.Commands.DelegateCommand CheckQuickBooksUrlAclCommand { get; private set; }
        public Prism.Commands.DelegateCommand ImportBudgetsCommand { get; private set; }
        private async Task ExecuteSaveSettingsAsync()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Saving settings...";

                // Use UnitOfWork transaction pattern for database operations
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Save to secure storage and configuration
                    await SaveGeneralSettingsAsync();
                    await SaveQuickBooksSettingsAsync();
                    await SaveSyncfusionSettingsAsync();
                    await SaveXaiSettingsAsync();
                    await SaveAdvancedSettingsAsync();
                });

                SettingsStatus = "Settings saved successfully";
                HasUnsavedChanges = false;
                LastSaved = DateTime.Now.ToString("g", CultureInfo.InvariantCulture);

                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                SettingsStatus = "Error saving settings";
                _logger.LogError(ex, "Error saving settings");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private async Task ExecuteOpenActivateXaiDialogAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var dialog = new ActivateXaiDialog();
                    dialog.Owner = Application.Current.MainWindow;
                    dialog.ShowDialog();
                });
                _logger.LogInformation("Opened ActivateXaiDialog");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open ActivateXaiDialog");
            }
        }

        private async Task SaveGeneralSettingsAsync()
        {
            try
            {
                // Save general settings to database using async EF operations
                var settings = await _dbContext.AppSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();

                if (settings == null)
                {
                    settings = new Models.AppSettings
                    {
                        Theme = SelectedTheme,
                        WindowWidth = WindowWidth,
                        WindowHeight = WindowHeight,
                        WindowMaximized = MaximizeOnStartup
                    };
                    _dbContext.AppSettings.Add(settings);
                }
                else
                {
                    settings.Theme = SelectedTheme;
                    settings.WindowWidth = WindowWidth;
                    settings.WindowHeight = WindowHeight;
                    settings.WindowMaximized = MaximizeOnStartup;
                }

                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save general settings to database");
                throw; // Re-throw to be handled by the calling method
            }
        }

        private async Task SaveQuickBooksSettingsAsync()
        {
            if (!string.IsNullOrEmpty(QuickBooksClientId))
                await _secretVaultService.SetSecretAsync("QuickBooks-ClientId", QuickBooksClientId);

            if (!string.IsNullOrEmpty(QuickBooksClientSecret))
                await _secretVaultService.SetSecretAsync("QuickBooks-ClientSecret", QuickBooksClientSecret);

            if (!string.IsNullOrEmpty(QuickBooksRedirectUri))
                await _secretVaultService.SetSecretAsync("QuickBooks-RedirectUri", QuickBooksRedirectUri);

            await _secretVaultService.SetSecretAsync("QuickBooks-Environment", SelectedQuickBooksEnvironment);

            // Clear sensitive data from memory after persisting
            QuickBooksClientSecret = string.Empty;
        }

        private async Task SaveSyncfusionSettingsAsync()
        {
            if (!string.IsNullOrEmpty(SyncfusionLicenseKey))
                await _secretVaultService.SetSecretAsync("Syncfusion-LicenseKey", SyncfusionLicenseKey);

            // Clear license key from memory
            SyncfusionLicenseKey = string.Empty;
        }

        private async Task SaveXaiSettingsAsync()
        {
            // Validate XAI API key before persisting
            if (!string.IsNullOrEmpty(XaiApiKey))
            {
                // Validate the provided key first
                var validationResult = await _aiService.ValidateApiKeyAsync(XaiApiKey);
                if (validationResult.HttpStatusCode != 200)
                {
                    AddError(nameof(XaiApiKey), "XAI API key validation failed. Please activate or verify the key in your xAI Console.");
                    return; // abort saving XAI settings
                }

                // Rotate the secret atomically in the vault (write new, verify, delete old)
                try
                {
                    await _secretVaultService.RotateSecretAsync("XAI-ApiKey", XaiApiKey);

                    // Update runtime service to use new key
                    try
                    {
                        await _aiService.UpdateApiKeyAsync(XaiApiKey);
                    }
                    catch (Exception ex)
                    {
                        // Attempt rollback: restore previous secret from vault (if possible)
                        _logger.LogError(ex, "Failed to update runtime AI service with new API key after rotation. Attempting rollback.");
                        // Try to read last-known key from vault (best-effort)
                        var previous = await _secretVaultService.GetSecretAsync("XAI-ApiKey");
                        if (!string.IsNullOrEmpty(previous))
                        {
                            try { await _aiService.UpdateApiKeyAsync(previous); }
                            catch (Exception rex) { _logger.LogError(rex, "Rollback of AI runtime key failed"); }
                        }
                        throw; // rethrow to surface failure
                    }

                    // Persist key to a local .env file for downstream tools (atomic write, restricted ACL)
                    try
                    {
                        var envPath = System.IO.Path.Combine(AppContext.BaseDirectory, ".env");
                        var tmpPath = envPath + ".tmp";
                        var line = $"XAI_API_KEY={XaiApiKey}\n";
                        System.IO.File.WriteAllText(tmpPath, line);
                        if (System.IO.File.Exists(envPath))
                            System.IO.File.Replace(tmpPath, envPath, null);
                        else
                            System.IO.File.Move(tmpPath, envPath);

                        // Apply best-effort file ACL restriction on Windows
                        try
                        {
                            var applied = FileSecurityHelper.RestrictFileToCurrentUser(envPath);
                            if (!applied)
                                _logger.LogWarning("File ACL restriction not applied to .env (platform or permission issue)");
                            else
                                _logger.LogInformation("Successfully restricted .env file access to current user only");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to apply file ACLs to .env (non-fatal)");
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to write .env entry for XAI_API_KEY (non-fatal)");
                    }

                    // Clear XAI API key from memory
                    XaiApiKey = string.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to rotate XAI API key in vault");
                    AddError(nameof(XaiApiKey), "Failed to persist the new API key. Check logs for details.");
                    return;
                }
            }

            await _secretVaultService.SetSecretAsync("XAI-BaseUrl", XaiBaseUrl);
            await _secretVaultService.SetSecretAsync("XAI-Model", XaiModel);
            await _secretVaultService.SetSecretAsync("XAI-TimeoutSeconds", XaiTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
            await _secretVaultService.SetSecretAsync("XAI-ResponseStyle", ResponseStyle);
            await _secretVaultService.SetSecretAsync("XAI-Personality", Personality);
            await _secretVaultService.SetSecretAsync("XAI-ContextWindowSize", ContextWindowSize.ToString(CultureInfo.InvariantCulture));
            await _secretVaultService.SetSecretAsync("XAI-EnableSafetyFilters", EnableSafetyFilters.ToString(CultureInfo.InvariantCulture));
            await _secretVaultService.SetSecretAsync("XAI-Temperature", Temperature.ToString(CultureInfo.InvariantCulture));
            await _secretVaultService.SetSecretAsync("XAI-MaxTokens", MaxTokens.ToString(CultureInfo.InvariantCulture));
            await _secretVaultService.SetSecretAsync("XAI-EnableStreaming", EnableStreaming.ToString(CultureInfo.InvariantCulture));
        }

        private async Task SaveAdvancedSettingsAsync()
        {
            try
            {
                // Save advanced settings to database using async EF operations
                var settings = await _dbContext.AppSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new Models.AppSettings
                    {
                        UseDynamicColumns = EnableDynamicColumns,
                        EnableDataCaching = EnableDataCaching,
                        CacheExpirationMinutes = CacheExpirationMinutes,
                        SelectedLogLevel = SelectedLogLevel,
                        EnableFileLogging = EnableFileLogging,
                        LogFilePath = LogFilePath
                    };
                    _dbContext.AppSettings.Add(settings);
                }
                else
                {
                    settings.UseDynamicColumns = EnableDynamicColumns;
                    settings.EnableDataCaching = EnableDataCaching;
                    settings.CacheExpirationMinutes = CacheExpirationMinutes;
                    settings.SelectedLogLevel = SelectedLogLevel;
                    settings.EnableFileLogging = EnableFileLogging;
                    settings.LogFilePath = LogFilePath;
                }

                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save advanced settings to database");
                throw; // Re-throw to be handled by the calling method
            }
        }

        private async Task ExecuteResetSettingsAsync()
        {
            var confirmed = await ShowConfirmationAsync(
                "Reset All Settings",
                "Are you sure you want to reset ALL settings to their default values?\n\n" +
                "This action cannot be undone and will:\n" +
                "• Reset all application preferences\n" +
                "• Clear API keys and connection settings\n" +
                "• Restore default themes and window sizes\n" +
                "• Reset fiscal year and advanced configurations\n\n" +
                "Continue with reset?",
                "Reset",
                "Cancel");

            if (!confirmed)
            {
                _logger.LogInformation("Settings reset cancelled by user");
                SettingsStatus = "Settings reset cancelled";
                return;
            }

            await LoadSettingsAsync();
            SettingsStatus = "Settings reset to defaults";
            _logger.LogInformation("All settings reloaded to defaults after confirmation");
        }

        private async Task ExecuteTestConnectionAsync()
        {
            await LoadDatabaseSettingsAsync();
        }

        private async Task ExecuteTestQuickBooksConnectionAsync()
        {
            if (_quickBooksService == null)
            {
                QuickBooksConnectionStatus = "QuickBooks service not available";
                QuickBooksStatusColor = Brushes.Orange;
                _logger?.LogWarning("TestQuickBooksConnection called but IQuickBooksService is null");
                return;
            }

            try
            {
                QuickBooksConnectionStatus = "Testing...";
                QuickBooksStatusColor = Brushes.Orange;

                var isConnected = await _quickBooksService.TestConnectionAsync();
                QuickBooksConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                QuickBooksStatusColor = isConnected ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = $"Error: {ex.Message}";
                QuickBooksStatusColor = Brushes.Red;
            }
        }

        private async Task ExecuteConnectQuickBooksAsync()
        {
            if (_quickBooksService == null)
            {
                QuickBooksConnectionStatus = "QuickBooks service not available";
                QuickBooksStatusColor = Brushes.Orange;
                _logger?.LogWarning("ConnectQuickBooks called but IQuickBooksService is null");
                return;
            }

            try
            {
                QuickBooksConnectionStatus = "Authorizing...";
                QuickBooksStatusColor = Brushes.Orange;

                var authorized = await _quickBooksService.AuthorizeAsync();
                if (authorized)
                {
                    // Re-test the connection now that tokens should be present
                    var isConnected = await _quickBooksService.TestConnectionAsync();
                    QuickBooksConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                    QuickBooksStatusColor = isConnected ? Brushes.Green : Brushes.Red;
                }
                else
                {
                    QuickBooksConnectionStatus = "Authorization Cancelled or Failed";
                    QuickBooksStatusColor = Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = $"Error: {ex.Message}";
                QuickBooksStatusColor = Brushes.Red;
            }
        }

        /// <summary>
        /// Imports budgets from QuickBooks Online.
        /// Uses SfDataGrid.RefreshDataSource() post-import for Syncfusion tie-in.
        /// Reference: https://help.syncfusion.com/wpf/datagrid/data-binding
        /// </summary>
        public async Task ImportBudgetsAsync()
        {
            if (_quickBooksService == null)
            {
                // Fallback UI: "Load QuickBooks module first" (nav command)
                QuickBooksConnectionStatus = "QuickBooks service not available. Load QuickBooks module first.";
                QuickBooksStatusColor = Brushes.Orange;
                _logger?.LogWarning("ImportBudgetsAsync called but IQuickBooksService is null");

                // TODO: Add navigation command to load QuickBooks module
                // Example: _regionManager.RequestNavigate("ContentRegion", "QuickBooksView");
                return;
            }

            try
            {
                IsBusy = true;
                QuickBooksConnectionStatus = "Importing budgets...";
                QuickBooksStatusColor = Brushes.Orange;

                // First, get budgets from QuickBooks
                var budgets = await _quickBooksService.GetBudgetsAsync();

                if (budgets != null && budgets.Count > 0)
                {
                    // Sync budgets to the app
                    var syncResult = await _quickBooksService.SyncBudgetsToAppAsync(budgets);

                    if (syncResult.Success)
                    {
                        QuickBooksConnectionStatus = $"Budgets imported successfully ({syncResult.RecordsSynced} records)";
                        QuickBooksStatusColor = Brushes.Green;
                        _logger?.LogInformation("Budgets imported successfully from QuickBooks: {Count} records", syncResult.RecordsSynced);
                    }
                    else
                    {
                        QuickBooksConnectionStatus = $"Import partially failed: {syncResult.ErrorMessage}";
                        QuickBooksStatusColor = Brushes.Orange;
                        _logger?.LogWarning("Budget import had errors: {Error}", syncResult.ErrorMessage);
                    }
                }
                else
                {
                    QuickBooksConnectionStatus = "No budgets found in QuickBooks";
                    QuickBooksStatusColor = Brushes.Orange;
                    _logger?.LogInformation("No budgets found in QuickBooks to import");
                }

                // Syncfusion Tie-In: Refresh the data grid
                // Note: The actual SfDataGrid instance should call RefreshDataSource()
                // This would typically be done in the View's code-behind or via a message/event
                // Example in View: BudgetsDataGrid?.RefreshDataSource();

                // Notify property changes to refresh any bound collections
                RaisePropertyChanged(string.Empty); // Refresh all bindings
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = $"Import failed: {ex.Message}";
                QuickBooksStatusColor = Brushes.Red;
                _logger?.LogError(ex, "Failed to import budgets from QuickBooks");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteCheckQuickBooksUrlAclAsync()
        {
            if (_quickBooksService == null)
            {
                QuickBooksUrlAclStatus = "QuickBooks service not available";
                QuickBooksUrlAclStatusColor = Brushes.Orange;
                _logger?.LogWarning("CheckQuickBooksUrlAcl called but IQuickBooksService is null");
                return;
            }

            try
            {
                QuickBooksUrlAclStatus = "Checking...";
                QuickBooksUrlAclStatusColor = Brushes.Orange;

                var result = await _quickBooksService.CheckUrlAclAsync(QuickBooksRedirectUri);
                if (result.IsReady)
                {
                    QuickBooksUrlAclStatus = $"Ready for {result.ListenerPrefix}";
                    QuickBooksUrlAclStatusColor = Brushes.Green;
                }
                else
                {
                    QuickBooksUrlAclStatus = string.IsNullOrWhiteSpace(result.Guidance) ? "Not configured" : result.Guidance;
                    QuickBooksUrlAclStatusColor = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                QuickBooksUrlAclStatus = $"Error: {ex.Message}";
                QuickBooksUrlAclStatusColor = Brushes.Red;
            }
        }

        private async Task ExecuteValidateLicenseAsync()
        {
            try
            {
                SyncfusionLicenseStatus = "Validating...";
                SyncfusionLicenseStatusColor = Brushes.Orange;

                var isValid = await _syncfusionLicenseService.ValidateLicenseAsync(SyncfusionLicenseKey);
                SyncfusionLicenseStatus = isValid ? "Valid" : "Invalid";
                SyncfusionLicenseStatusColor = isValid ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                SyncfusionLicenseStatus = $"Error: {ex.Message}";
                SyncfusionLicenseStatusColor = Brushes.Red;
            }
        }

        private async Task ExecuteTestXaiConnectionAsync()
        {
            try
            {
                XaiConnectionStatus = "Testing...";
                XaiStatusColor = Brushes.Orange;

                var isConnected = await TestXaiConnectionInternalAsync();
                XaiConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                XaiStatusColor = isConnected ? Brushes.Green : Brushes.Red;
            }
            catch (Exception ex)
            {
                XaiConnectionStatus = $"Error: {ex.Message}";
                XaiStatusColor = Brushes.Red;
            }
        }

        private async Task ExecuteValidateXaiKeyAsync(string? apiKey)
        {
            try
            {
                XaiConnectionStatus = "Validating key...";
                XaiStatusColor = Brushes.Orange;

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    AddError(nameof(XaiApiKey), "API key cannot be empty for validation.");
                    XaiConnectionStatus = "Validation Failed";
                    XaiStatusColor = Brushes.Red;
                    return;
                }

                var result = await _aiService.ValidateApiKeyAsync(apiKey);
                if (result.HttpStatusCode == 200)
                {
                    // Key validated successfully
                    ClearErrors(nameof(XaiApiKey));
                    XaiConnectionStatus = "Key Validated";
                    XaiStatusColor = Brushes.Green;
                    // Audit successful validation (no secret content)
                    try
                    {
                        var fingerprint = ComputeKeyFingerprint(apiKey);
                        await _auditService.AuditAsync("XAI.KeyValidated", new { Provider = "xAI", Fingerprint = fingerprint, User = Environment.UserName, Machine = Environment.MachineName });
                    }
                    catch { }
                }
                else
                {
                    // Map known error codes to user-friendly messages
                    var message = result.Content ?? "Validation failed";
                    if (result.ErrorCode == "AuthFailure" || result.HttpStatusCode == 401 || result.HttpStatusCode == 403)
                        message = "API key invalid or not activated. Open the xAI Console to verify permissions and activation.";
                    else if (result.ErrorCode == "RateLimited" || result.HttpStatusCode == 429)
                        message = "API key is rate limited. Try again later.";
                    else if (result.HttpStatusCode == 402)
                        message = "API key requires billing/credits. Please enable billing in the provider console.";

                    AddError(nameof(XaiApiKey), message);
                    XaiConnectionStatus = "Validation Failed";
                    XaiStatusColor = Brushes.Red;
                    // Audit failed validation
                    try
                    {
                        var fingerprint = ComputeKeyFingerprint(apiKey);
                        await _auditService.AuditAsync("XAI.KeyValidationFailed", new { Provider = "xAI", Fingerprint = fingerprint, Reason = message, User = Environment.UserName, Machine = Environment.MachineName });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "XAI key validation failed");
                AddError(nameof(XaiApiKey), "Unexpected error during validation. Check network connectivity and try again.");
                XaiConnectionStatus = "Validation Error";
                XaiStatusColor = Brushes.Red;
            }
        }

        private async Task ExecuteValidateAndSaveXaiKeyAsync(string? apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                AddError(nameof(XaiApiKey), "API key cannot be empty.");
                return;
            }

            IsBusy = true;
            BusyMessage = "Validating and saving API key...";

            try
            {
                // Validate the key
                var validationResult = await _aiService.ValidateApiKeyAsync(apiKey);
                if (validationResult.HttpStatusCode != 200)
                {
                    IsXaiKeyValidated = false;
                    XaiValidationMessage = validationResult.Content ?? "Validation failed";
                    AddError(nameof(XaiApiKey), XaiValidationMessage);
                    _logger.LogInformation("XAI key validation failed (code={Code})", validationResult.ErrorCode ?? validationResult.HttpStatusCode.ToString(CultureInfo.InvariantCulture));
                    return;
                }

                // Rotate secret in vault
                await _secretVaultService.RotateSecretAsync("XAI-ApiKey", apiKey);

                // Update runtime service
                await _aiService.UpdateApiKeyAsync(apiKey);

                // Atomically write .env (best-effort)
                try
                {
                    var envPath = System.IO.Path.Combine(AppContext.BaseDirectory, ".env");
                    var tmpPath = envPath + ".tmp";
                    var line = $"XAI_API_KEY={apiKey}\n";
                    System.IO.File.WriteAllText(tmpPath, line);
                    if (System.IO.File.Exists(envPath))
                        System.IO.File.Replace(tmpPath, envPath, null);
                    else
                        System.IO.File.Move(tmpPath, envPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist .env entry for XAI key (non-fatal)");
                }

                // Success: flag validated and clear UI secret
                IsXaiKeyValidated = true;
                XaiValidationMessage = "Key validated and saved";
                _logger.LogInformation("XAI API key validated and rotated successfully (no secret logged)");

                // Audit event (no secret content)
                try
                {
                    var fingerprint = ComputeKeyFingerprint(apiKey);
                    await _auditService.AuditAsync("XAI.KeyRotated", new { Provider = "xAI", Fingerprint = fingerprint, User = Environment.UserName, Machine = Environment.MachineName });
                }
                catch { }

                // Clear in-memory key
                XaiApiKey = string.Empty;
                ClearErrors(nameof(XaiApiKey));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to validate and save XAI API key");
                AddError(nameof(XaiApiKey), "Failed to save API key. Check logs for details.");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        // Public wrappers for UI code-behind to call validation/save asynchronously
        public Task ValidateXaiKeyAsyncPublic(string? apiKey) => ExecuteValidateXaiKeyAsync(apiKey);
        public Task ValidateAndSaveXaiKeyAsyncPublic(string? apiKey) => ExecuteValidateAndSaveXaiKeyAsync(apiKey);

        private async Task<bool> TestXaiConnectionInternalAsync(string? apiKey = null)
        {
            try
            {
                // Test XAI connection by making a lightweight API call using IAIService
                // If an explicit apiKey was provided (user is validating a new key), validate it using the new validation API
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    var result = await _aiService.ValidateApiKeyAsync(apiKey);
                    if (result.HttpStatusCode == 200)
                        return true;

                    _logger.LogWarning("XAI validation returned status {Status} - {ErrorCode}", result.HttpStatusCode, result.ErrorCode);
                    return false;
                }

                // Otherwise validate using the currently configured key (existing behavior)
                var currentKey = XaiApiKey;
                if (string.IsNullOrWhiteSpace(currentKey))
                    return false;

                try
                {
                    var result = await _aiService.GetInsightsWithStatusAsync("validation", "Ping: Are you there?", CancellationToken.None);
                    if (result.HttpStatusCode == 200)
                        return true;

                    _logger.LogWarning("XAI validation returned status {Status} - {ErrorCode}", result.HttpStatusCode, result.ErrorCode);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "XAI validation call failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing XAI connection");
                return false;
            }
        }

        private void OpenXaiConsole()
        {
            try
            {
                var url = "https://console.x.ai/team/default/api-keys";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open xAI console URL");
            }
        }

        private static string ComputeKeyFingerprint(string? key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            try
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var bytes = System.Text.Encoding.UTF8.GetBytes(key);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).Substring(0, 8).ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        // Public wrapper for UI to open the xAI console
        public void OpenXaiConsolePublic() => OpenXaiConsole();

        /// <summary>
        /// Apply theme to all open windows in the application
        /// </summary>
        private void ApplyThemeToAllWindows(string themeName)
        {
            try
            {
                // Apply to main window
                if (Application.Current.MainWindow != null)
                {
                    ThemeUtility.TryApplyTheme(Application.Current.MainWindow, themeName);
                }

                // Apply to all other windows
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != Application.Current.MainWindow)
                    {
                        ThemeUtility.TryApplyTheme(window, themeName);
                    }
                }

                // Save the theme preference
                _settingsService.Current.Theme = themeName;
                _settingsService.Save();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme {ThemeName}", themeName);
            }
        }

        private async Task SaveFiscalYearSettingsAsync()
        {
            try
            {
                SettingsStatus = "Saving fiscal year settings...";

                // Update FiscalYearSettings in database using async EF calls (no Task.Run)
                var fySettings = await _dbContext.FiscalYearSettings.FindAsync(1);

                if (fySettings == null)
                {
                    fySettings = new Models.FiscalYearSettings
                    {
                        Id = 1,
                        FiscalYearStartMonth = FiscalYearStartMonth,
                        FiscalYearStartDay = FiscalYearStartDay,
                        LastModified = DateTime.UtcNow
                    };
                    _dbContext.FiscalYearSettings.Add(fySettings);
                }
                else
                {
                    fySettings.FiscalYearStartMonth = FiscalYearStartMonth;
                    fySettings.FiscalYearStartDay = FiscalYearStartDay;
                    fySettings.LastModified = DateTime.UtcNow;
                }

                await _unitOfWork.SaveChangesAsync();

                await LoadFiscalYearDisplayAsync();

                SettingsStatus = "Fiscal year settings saved successfully";
                LastSaved = DateTime.Now.ToString("g", CultureInfo.InvariantCulture);

                await ShowInformationAsync(
                    "Settings Saved",
                    "Fiscal year settings saved successfully.\nChanges will affect budget periods and financial reports.");
            }
            catch (Exception ex)
            {
                SettingsStatus = $"Error saving fiscal year settings: {ex.Message}";
                _logger.LogError(ex, "Failed to save fiscal year settings");

                await ShowErrorAsync(
                    "Error",
                    $"Failed to save fiscal year settings:\n{ex.Message}");
            }
        }

        #region Dialog helpers (use Prism IDialogService directly)

        private Task<IDialogResult?> ShowDialogByNameAsync(string dialogName, string title, DialogParameters parameters)
        {
            var tcs = new TaskCompletionSource<IDialogResult?>();

            var dialogParams = new DialogParameters
            {
                { "Title", title }
            };

            if (parameters != null)
            {
                foreach (var p in parameters)
                    dialogParams.Add(p.Key, p.Value);
            }

            _dialogService.ShowDialog(dialogName, dialogParams, result => tcs.SetResult(result));

            return tcs.Task;
        }

        private async Task<bool> ShowConfirmationAsync(string title, string message, string confirmButtonText = "Yes", string cancelButtonText = "No")
        {
            var parameters = new DialogParameters
            {
                { "Message", message },
                { "ConfirmButtonText", confirmButtonText },
                { "CancelButtonText", cancelButtonText }
            };

            var result = await ShowDialogByNameAsync("ConfirmationDialog", title, parameters);
            return result?.Result == ButtonResult.Yes || result?.Result == ButtonResult.OK;
        }

        private async Task ShowInformationAsync(string title, string message, string buttonText = "OK")
        {
            var parameters = new DialogParameters
            {
                { "Message", message },
                { "ButtonText", buttonText }
            };

            await ShowDialogByNameAsync("NotificationDialog", title, parameters);
        }

        private async Task ShowErrorAsync(string title, string message, string buttonText = "OK")
        {
            var parameters = new DialogParameters
            {
                { "Message", message },
                { "ButtonText", buttonText }
            };

            await ShowDialogByNameAsync("ErrorDialog", title, parameters);
        }

        #endregion

        private async Task LoadFiscalYearDisplayAsync()
        {
            try
            {
                var fySettings = await _dbContext.FiscalYearSettings.FindAsync(1);

                if (fySettings != null)
                {
                    FiscalYearStartMonth = fySettings.FiscalYearStartMonth;
                    FiscalYearStartDay = fySettings.FiscalYearStartDay;

                    var fyStart = fySettings.GetCurrentFiscalYearStart(DateTime.Now);
                    var fyEnd = fySettings.GetCurrentFiscalYearEnd(DateTime.Now);

                    var fyNumber = fyStart.Month >= 7 ? fyStart.Year + 1 : fyStart.Year;
                    CurrentFiscalYearDisplay = $"FY{fyStart.Year}-{fyEnd.Year}";
                    FiscalYearPeriodDisplay = $"{fyStart:MMMM d, yyyy} - {fyEnd:MMMM d, yyyy}";

                    var daysRemaining = (int)(fyEnd - DateTime.Now).TotalDays;
                    DaysRemainingInFiscalYear = Math.Max(0, daysRemaining);

                    // Populate available fiscal years (current ± 3 years)
                    AvailableFiscalYears.Clear();
                    for (int i = -3; i <= 1; i++)
                    {
                        var year = fyNumber + i;
                        var startYear = fySettings.FiscalYearStartMonth >= 7 ? year - 1 : year;
                        AvailableFiscalYears.Add($"FY{startYear}-{startYear + 1}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load fiscal year display");
                CurrentFiscalYearDisplay = "Error loading";
                FiscalYearPeriodDisplay = "Error loading";
            }
        }

        // Validation Methods
        private void ValidateWindowWidth(int value)
        {
            ClearErrors(nameof(WindowWidth));

            if (value < 800)
            {
                AddError(nameof(WindowWidth), "Minimum width is 800 pixels");
                WindowWidthValidation = "Minimum width is 800 pixels";
            }
            else if (value > 3840)
            {
                AddError(nameof(WindowWidth), "Maximum width is 3840 pixels");
                WindowWidthValidation = "Maximum width is 3840 pixels";
            }
            else
            {
                WindowWidthValidation = string.Empty;
            }
        }

        private void ValidateWindowHeight(int value)
        {
            ClearErrors(nameof(WindowHeight));

            if (value < 600)
            {
                AddError(nameof(WindowHeight), "Minimum height is 600 pixels");
                WindowHeightValidation = "Minimum height is 600 pixels";
            }
            else if (value > 2160)
            {
                AddError(nameof(WindowHeight), "Maximum height is 2160 pixels");
                WindowHeightValidation = "Maximum height is 2160 pixels";
            }
            else
            {
                WindowHeightValidation = string.Empty;
            }
        }

        private void ValidateXaiApiKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                XaiApiKeyValidation = string.Empty; // API key is optional
            }
            else if (value.Length < 20)
            {
                XaiApiKeyValidation = "API key appears to be too short";
            }
            else if (!value.StartsWith("xai-", StringComparison.Ordinal))
            {
                XaiApiKeyValidation = "API key should start with 'xai-'";
            }
            else
            {
                XaiApiKeyValidation = string.Empty;
            }
        }

        private void ValidateXaiTimeout(int value)
        {
            if (value < 5)
            {
                XaiTimeoutValidation = "Minimum timeout is 5 seconds";
            }
            else if (value > 300)
            {
                XaiTimeoutValidation = "Maximum timeout is 300 seconds";
            }
            else
            {
                XaiTimeoutValidation = string.Empty;
            }
        }

        private void ValidateContextWindowSize(int value)
        {
            if (value < 1024)
            {
                ContextWindowValidation = "Minimum context window is 1024 tokens";
            }
            else if (value > 32768)
            {
                ContextWindowValidation = "Maximum context window is 32768 tokens";
            }
            else if (value % 1024 != 0)
            {
                ContextWindowValidation = "Context window should be a multiple of 1024";
            }
            else
            {
                ContextWindowValidation = string.Empty;
            }
        }

        private void ValidateCacheExpiration(int value)
        {
            if (value < 1)
            {
                CacheExpirationValidation = "Minimum cache expiration is 1 minute";
            }
            else if (value > 1440)
            {
                CacheExpirationValidation = "Maximum cache expiration is 1440 minutes (24 hours)";
            }
            else
            {
                CacheExpirationValidation = string.Empty;
            }
        }

        private void ValidateFiscalYearDay(int value)
        {
            if (value < 1)
            {
                FiscalYearDayValidation = "Day must be between 1 and 31";
            }
            else if (value > 31)
            {
                FiscalYearDayValidation = "Day must be between 1 and 31";
            }
            else
            {
                FiscalYearDayValidation = string.Empty;
            }
        }

        private void ValidateTemperature(double value)
        {
            if (value < 0.0)
            {
                TemperatureValidation = "Temperature must be between 0.0 and 2.0";
            }
            else if (value > 2.0)
            {
                TemperatureValidation = "Temperature must be between 0.0 and 2.0";
            }
            else
            {
                TemperatureValidation = string.Empty;
            }
        }

        private void ValidateMaxTokens(int value)
        {
            if (value < 1)
            {
                MaxTokensValidation = "Max tokens must be at least 1";
            }
            else if (value > ContextWindowSize)
            {
                MaxTokensValidation = $"Max tokens cannot exceed context window size ({ContextWindowSize})";
            }
            else
            {
                MaxTokensValidation = string.Empty;
            }
        }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingsNavCts?.Dispose();
        }
    }

    /// <summary>
    /// Helper class for month dropdown
    /// </summary>
    public class MonthOption
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
}
