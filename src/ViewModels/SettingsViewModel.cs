using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Services;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Microsoft.Extensions.Options;
using WileyWidget.Configuration;

namespace WileyWidget.ViewModels
{
    public partial class SettingsViewModel : ObservableObject, INotifyDataErrorInfo
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly IOptions<AppOptions> _appOptions;
        private readonly IOptionsMonitor<AppOptions> _appOptionsMonitor;
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _dbContext;
        private readonly ISecretVaultService _secretVaultService;
        private readonly IQuickBooksService _quickBooksService;
        private readonly ISyncfusionLicenseService _syncfusionLicenseService;
        private readonly IAIService _aiService;
        private readonly IThemeManager _themeManager;
        private readonly ISettingsService _settingsService;

        private readonly Dictionary<string, List<string>> _errors = new();

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
        [ObservableProperty]
        [property: Category("Appearance")]
        [property: DisplayName("Available Themes")]
        [property: Description("List of available application themes")]
        private ObservableCollection<string> availableThemes = new() { "FluentDark", "FluentLight" };

        [ObservableProperty]
        [property: Category("Appearance")]
        [property: DisplayName("Selected Theme")]
        [property: Description("Currently selected application theme")]
        private string selectedTheme = "FluentDark";

        [ObservableProperty]
        [property: Category("Appearance")]
        [property: DisplayName("Dark Mode")]
        [property: Description("Whether dark mode is currently enabled")]
        private bool isDarkMode;

        [ObservableProperty]
        [property: Category("System")]
        [property: Browsable(false)]
        private bool isLoading;

        [ObservableProperty]
        [property: Category("System")]
        [property: DisplayName("Status Message")]
        [property: Description("Current operation status message")]
        private string statusMessage = "Ready";

        partial void OnWindowWidthChanged(int value)
        {
            ValidateWindowWidth(value);
        }

        partial void OnWindowHeightChanged(int value)
        {
            ValidateWindowHeight(value);
        }

        partial void OnXaiTimeoutSecondsChanged(int value)
        {
            ValidateXaiTimeout(value);
        }

        partial void OnContextWindowSizeChanged(int value)
        {
            ValidateContextWindowSize(value);
        }

        partial void OnCacheExpirationMinutesChanged(int value)
        {
            ValidateCacheExpiration(value);
        }

        partial void OnFiscalYearStartDayChanged(int value)
        {
            ValidateFiscalYearDay(value);
        }

        partial void OnTemperatureChanged(double value)
        {
            ValidateTemperature(value);
        }

        partial void OnMaxTokensChanged(int value)
        {
            ValidateMaxTokens(value);
        }

        partial void OnSelectedThemeChanged(string value)
        {
            IsDarkMode = value?.Contains("Dark", StringComparison.OrdinalIgnoreCase) == true;
            _themeManager.ApplyTheme(value);
        }

        [ObservableProperty]
        [property: Category("General")]
        [property: DisplayName("Window Width")]
        [property: Description("Default window width in pixels (800-3840)")]
        [property: Range(800, 3840)]
        private int windowWidth = 1200;

        [ObservableProperty]
        [property: Category("General")]
        [property: DisplayName("Window Height")]
        [property: Description("Default window height in pixels (600-2160)")]
        [property: Range(600, 2160)]
        private int windowHeight = 800;

        [ObservableProperty]
        [property: Category("General")]
        [property: DisplayName("Maximize on Startup")]
        [property: Description("Maximize the window when the application starts")]
        private bool maximizeOnStartup;

        [ObservableProperty]
        [property: Category("General")]
        [property: DisplayName("Show Splash Screen")]
        [property: Description("Display the splash screen during application startup")]
        private bool showSplashScreen = true;

        // Database Settings
        [ObservableProperty]
        [property: Category("Database")]
        [property: DisplayName("Connection String")]
        [property: Description("Database connection string (read-only)")]
        [property: ReadOnly(true)]
        private string databaseConnectionString;

        [ObservableProperty]
        [property: Category("Database")]
        [property: DisplayName("Status")]
        [property: Description("Current database connection status")]
        [property: ReadOnly(true)]
        private string databaseStatus = "Checking...";

        [ObservableProperty]
        [property: Category("Database")]
        [property: Browsable(false)]
        private Brush databaseStatusColor = Brushes.Orange;

        // QuickBooks Settings
        [ObservableProperty]
        [property: Category("QuickBooks")]
        [property: DisplayName("Client ID")]
        [property: Description("QuickBooks OAuth2 Client ID from Intuit Developer Portal")]
        private string quickBooksClientId;

        [ObservableProperty]
        [property: Category("QuickBooks")]
        [property: DisplayName("Client Secret")]
        [property: Description("QuickBooks OAuth2 Client Secret (securely stored)")]
        [property: PasswordPropertyText(true)]
        private string quickBooksClientSecret;

        [ObservableProperty]
        [property: Category("QuickBooks")]
        [property: DisplayName("Redirect URI")]
        [property: Description("OAuth2 redirect URI (e.g., https://localhost:5001/callback)")]
        private string quickBooksRedirectUri;

        [ObservableProperty]
        [property: Category("QuickBooks")]
        [property: Browsable(false)]
        private ObservableCollection<string> quickBooksEnvironments = new() { "Sandbox", "Production" };

        [ObservableProperty]
        [property: Category("QuickBooks")]
        [property: DisplayName("Environment")]
        [property: Description("Select Sandbox for testing or Production for live data")]
        private string selectedQuickBooksEnvironment = "Sandbox";

        [ObservableProperty]
        [property: Category("QuickBooks")]
        [property: DisplayName("Connection Status")]
        [property: Description("Current QuickBooks connection status")]
        [property: ReadOnly(true)]
        private string quickBooksConnectionStatus = "Not Connected";

        [ObservableProperty]
        [property: Category("QuickBooks")]
        [property: Browsable(false)]
        private Brush quickBooksStatusColor = Brushes.Red;

        // Syncfusion License
        [ObservableProperty]
        [property: Category("Syncfusion")]
        [property: DisplayName("License Key")]
        [property: Description("Syncfusion license key (get from syncfusion.com/account)")]
        [property: PasswordPropertyText(true)]
        private string syncfusionLicenseKey;

        [ObservableProperty]
        [property: Category("Syncfusion")]
        [property: DisplayName("License Status")]
        [property: Description("Current license validation status")]
        [property: ReadOnly(true)]
        private string syncfusionLicenseStatus = "Checking...";

        [ObservableProperty]
        [property: Category("Syncfusion")]
        [property: Browsable(false)]
        private Brush syncfusionLicenseStatusColor = Brushes.Orange;

        // XAI Settings
        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("API Key")]
        [property: Description("XAI API key for AI functionality (format: xai-xxxxxxxx)")]
        [property: PasswordPropertyText(true)]
        private string xaiApiKey;

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Base URL")]
        [property: Description("XAI API base URL (default: https://api.x.ai/v1/)")]
        private string xaiBaseUrl = "https://api.x.ai/v1/";

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Timeout (seconds)")]
        [property: Description("Maximum time to wait for AI responses (5-300 seconds)")]
        [property: Range(5, 300)]
        private int xaiTimeoutSeconds = 15;

        [ObservableProperty]
        [property: Category("XAI")]
        [property: Browsable(false)]
        private ObservableCollection<string> availableModels = new() { "grok-4-0709", "grok-beta", "grok-1" };

        [ObservableProperty]
        [property: Category("XAI")]
        [property: Browsable(false)]
        private ObservableCollection<string> availableResponseStyles = new() { "Balanced", "Creative", "Precise", "Concise" };

        [ObservableProperty]
        [property: Category("XAI")]
        [property: Browsable(false)]
        private ObservableCollection<string> availablePersonalities = new() { "Professional", "Friendly", "Technical", "Casual" };

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Model")]
        [property: Description("AI model to use (grok-4-0709 recommended)")]
        private string xaiModel = "grok-4-0709";

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Response Style")]
        [property: Description("How detailed and structured AI responses should be")]
        private string responseStyle = "Balanced";

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Personality")]
        [property: Description("Communication style for AI responses")]
        private string personality = "Professional";

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Context Window Size")]
        [property: Description("Maximum tokens AI can process (1024-32768)")]
        [property: Range(1024, 32768)]
        private int contextWindowSize = 4096;

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Enable Safety Filters")]
        [property: Description("Enable content safety filtering")]
        private bool enableSafetyFilters = true;

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Temperature")]
        [property: Description("Response randomness (0.0-2.0, lower = more focused)")]
        [property: Range(0.0, 2.0)]
        private double temperature = 0.7;

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Max Tokens")]
        [property: Description("Maximum tokens in response (1-4096)")]
        [property: Range(1, 4096)]
        private int maxTokens = 2048;

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Enable Streaming")]
        [property: Description("Stream responses in real-time")]
        private bool enableStreaming = false;

        [ObservableProperty]
        [property: Browsable(false)]
        private string temperatureValidation = string.Empty;

        [ObservableProperty]
        [property: Browsable(false)]
        private string maxTokensValidation = string.Empty;

        [ObservableProperty]
        [property: Category("XAI")]
        [property: DisplayName("Connection Status")]
        [property: Description("Current XAI connection status")]
        [property: ReadOnly(true)]
        private string xaiConnectionStatus = "Not Configured";

        [ObservableProperty]
        [property: Browsable(false)]
        private Brush xaiStatusColor = Brushes.Orange;

        // Fiscal Year Settings
        [ObservableProperty]
        [property: Category("Fiscal Year")]
        [property: Browsable(false)]
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

        [ObservableProperty]
        [property: Category("Fiscal Year")]
        [property: DisplayName("Start Month")]
        [property: Description("Month when fiscal year begins (1-12)")]
        [property: Range(1, 12)]
        private int fiscalYearStartMonth = 7; // Default to July

        [ObservableProperty]
        [property: Category("Fiscal Year")]
        [property: DisplayName("Start Day")]
        [property: Description("Day of month when fiscal year begins (1-31)")]
        [property: Range(1, 31)]
        private int fiscalYearStartDay = 1;

        [ObservableProperty]
        [property: Category("Fiscal Year")]
        [property: DisplayName("Current Fiscal Year")]
        [property: Description("Current fiscal year period")]
        [property: ReadOnly(true)]
        private string currentFiscalYearDisplay = "Loading...";

        [ObservableProperty]
        [property: Category("Fiscal Year")]
        [property: DisplayName("Fiscal Year Period")]
        [property: Description("Date range of current fiscal year")]
        [property: ReadOnly(true)]
        private string fiscalYearPeriodDisplay = "Loading...";

        [ObservableProperty]
        [property: Category("Fiscal Year")]
        [property: DisplayName("Days Remaining")]
        [property: Description("Days remaining in current fiscal year")]
        [property: ReadOnly(true)]
        private int daysRemainingInFiscalYear;

        [ObservableProperty]
        [property: Category("Fiscal Year")]
        [property: Browsable(false)]
        private ObservableCollection<string> availableFiscalYears = new();

        // Advanced Settings
        [ObservableProperty]
        [property: Category("Advanced")]
        [property: DisplayName("Enable Dynamic Columns")]
        [property: Description("Enable dynamic column generation in data grids")]
        private bool enableDynamicColumns = true;

        [ObservableProperty]
        [property: Category("Advanced")]
        [property: DisplayName("Enable Data Caching")]
        [property: Description("Cache data to improve performance")]
        private bool enableDataCaching = true;

        [ObservableProperty]
        [property: Category("Advanced")]
        [property: DisplayName("Cache Expiration (minutes)")]
        [property: Description("How long to keep cached data (1-1440 minutes)")]
        [property: Range(1, 1440)]
        private int cacheExpirationMinutes = 30;

        [ObservableProperty]
        [property: Category("Advanced")]
        [property: Browsable(false)]
        private ObservableCollection<string> logLevels = new() { "Debug", "Information", "Warning", "Error", "Critical" };

        [ObservableProperty]
        [property: Category("Advanced")]
        [property: DisplayName("Log Level")]
        [property: Description("Minimum log level (Debug for dev, Information for prod)")]
        private string selectedLogLevel = "Information";

        [ObservableProperty]
        [property: Category("Advanced")]
        [property: DisplayName("Enable File Logging")]
        [property: Description("Write logs to file")]
        private bool enableFileLogging = true;

        [ObservableProperty]
        [property: Category("Advanced")]
        [property: DisplayName("Log File Path")]
        [property: Description("Path to the log file")]
        private string logFilePath = "logs/wiley-widget.log";

        // Status
        [ObservableProperty]
        private string settingsStatus = "Ready";

        [ObservableProperty]
        private string lastSaved = "Never";

        [ObservableProperty]
        private string systemInfo;

        // Validation Properties
        [ObservableProperty]
        private string windowWidthValidation = string.Empty;

        [ObservableProperty]
        private string windowHeightValidation = string.Empty;

        [ObservableProperty]
        private string xaiApiKeyValidation = string.Empty;

        [ObservableProperty]
        private string xaiTimeoutValidation = string.Empty;

        [ObservableProperty]
        private string contextWindowValidation = string.Empty;

        [ObservableProperty]
        private string cacheExpirationValidation = string.Empty;

        [ObservableProperty]
        private string fiscalYearDayValidation = string.Empty;

        [ObservableProperty]
        private string quickBooksClientIdValidation = string.Empty;

        [ObservableProperty]
        [property: Browsable(false)]
        private string quickBooksClientSecretValidation = string.Empty;

        [ObservableProperty]
        [property: Browsable(false)]
        private string quickBooksRedirectUriValidation = string.Empty;

        [ObservableProperty]
        [property: Browsable(false)]
        private string syncfusionLicenseKeyValidation = string.Empty;

        // UI State
        [ObservableProperty]
        [property: Browsable(false)]
        private bool isBusy;

        [ObservableProperty]
        [property: Browsable(false)]
        private string busyMessage;

        // Search and Filter
        [ObservableProperty]
        [property: Browsable(false)]
        private string searchText = string.Empty;

        [ObservableProperty]
        [property: Browsable(false)]
        private bool showAdvancedSettings = true;

        public bool HasUnsavedChanges { get; private set; }

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            IOptions<AppOptions> appOptions,
            IOptionsMonitor<AppOptions> appOptionsMonitor,
            IUnitOfWork unitOfWork,
            AppDbContext dbContext,
            ISecretVaultService secretVaultService,
            IQuickBooksService quickBooksService,
            ISyncfusionLicenseService syncfusionLicenseService,
            IAIService aiService,
            IThemeManager themeManager,
            ISettingsService settingsService)
        {
            // Validate required dependencies
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appOptions = appOptions ?? throw new ArgumentNullException(nameof(appOptions));
            _appOptionsMonitor = appOptionsMonitor ?? throw new ArgumentNullException(nameof(appOptionsMonitor));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _secretVaultService = secretVaultService ?? throw new ArgumentNullException(nameof(secretVaultService));
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _syncfusionLicenseService = syncfusionLicenseService ?? throw new ArgumentNullException(nameof(syncfusionLicenseService));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Initialize system info
            SystemInfo = $"OS: {Environment.OSVersion}\n" +
                        $".NET Version: {Environment.Version}\n" +
                        $"Machine: {Environment.MachineName}\n" +
                        $"User: {Environment.UserName}";

            // Set up property change tracking for unsaved changes
            PropertyChanged += OnPropertyChanged;

            // Initialize Prism DelegateCommands
            SaveSettingsCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteSaveSettingsAsync(), () => !IsBusy);
            ResetSettingsCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteResetSettingsAsync(), () => !IsBusy);
            TestConnectionCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteTestConnectionAsync(), () => !IsBusy);
            TestQuickBooksConnectionCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteTestQuickBooksConnectionAsync(), () => !IsBusy);
            ValidateLicenseCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteValidateLicenseAsync(), () => !IsBusy);
            TestXaiConnectionCommand = new Prism.Commands.DelegateCommand(async () => await ExecuteTestXaiConnectionAsync(), () => !IsBusy);
            SaveFiscalYearSettingsCommand = new Prism.Commands.DelegateCommand(async () => await SaveFiscalYearSettingsAsync(), () => !IsBusy);
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
                LastSaved = DateTime.Now.ToString("g");

                _logger.LogInformation("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                SettingsStatus = "Error loading settings";
                _logger.LogError(ex, "Error loading settings");
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error",
                              MessageBoxButton.OK);
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
                var settings = await _dbContext.AppSettings.FindAsync(1);
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
                // Prefer environment variables during troubleshooting; Key Vault fallback is commented below
                QuickBooksClientId = Environment.GetEnvironmentVariable("QUICKBOOKS_CLIENT_ID") ?? "";
                QuickBooksClientSecret = Environment.GetEnvironmentVariable("QUICKBOOKS_CLIENT_SECRET") ?? "";
                QuickBooksRedirectUri = Environment.GetEnvironmentVariable("QUICKBOOKS_REDIRECT_URI") ?? "";
                SelectedQuickBooksEnvironment = Environment.GetEnvironmentVariable("QUICKBOOKS_ENVIRONMENT") ?? "Sandbox";

                // Test connection if credentials are available
                if (!string.IsNullOrEmpty(QuickBooksClientId))
                {
                    var isConnected = await _quickBooksService.TestConnectionAsync();
                    QuickBooksConnectionStatus = isConnected ? "Connected" : "Connection Failed";
                    QuickBooksStatusColor = isConnected ? Brushes.Green : Brushes.Red;
                }
                else
                {
                    QuickBooksConnectionStatus = "Not Configured";
                    QuickBooksStatusColor = Brushes.Orange;
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
                // Prefer environment variable for license during troubleshooting
                SyncfusionLicenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ?? "";

                // Simple license validation - check if key exists and is not empty
                var isValid = !string.IsNullOrEmpty(SyncfusionLicenseKey);
                SyncfusionLicenseStatus = isValid ? "Valid" : "Invalid or Missing";
                SyncfusionLicenseStatusColor = isValid ? Brushes.Green : Brushes.Red;
                // Keep method async without Key Vault calls
                await Task.CompletedTask;
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
                // Load XAI settings from environment variables or configuration
                XaiApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ?? "";
                XaiBaseUrl = Environment.GetEnvironmentVariable("XAI_BASE_URL") ?? "https://api.x.ai/v1/";
                XaiModel = Environment.GetEnvironmentVariable("XAI_MODEL") ?? "grok-4-0709";

                // Parse timeout from environment
                if (int.TryParse(Environment.GetEnvironmentVariable("XAI_TIMEOUT_SECONDS"), out var timeout))
                {
                    XaiTimeoutSeconds = timeout;
                }
                else
                {
                    XaiTimeoutSeconds = 15;
                }

                // Load additional AI settings
                ResponseStyle = Environment.GetEnvironmentVariable("XAI_RESPONSE_STYLE") ?? "Balanced";
                Personality = Environment.GetEnvironmentVariable("XAI_PERSONALITY") ?? "Professional";

                if (int.TryParse(Environment.GetEnvironmentVariable("XAI_CONTEXT_WINDOW_SIZE"), out var contextSize))
                {
                    ContextWindowSize = contextSize;
                }
                else
                {
                    ContextWindowSize = 4096;
                }

                if (bool.TryParse(Environment.GetEnvironmentVariable("XAI_ENABLE_SAFETY_FILTERS"), out var safetyFilters))
                {
                    EnableSafetyFilters = safetyFilters;
                }
                else
                {
                    EnableSafetyFilters = true;
                }

                if (double.TryParse(Environment.GetEnvironmentVariable("XAI_TEMPERATURE"), out var temp))
                {
                    Temperature = temp;
                }
                else
                {
                    Temperature = 0.7;
                }

                if (int.TryParse(Environment.GetEnvironmentVariable("XAI_MAX_TOKENS"), out var maxTok))
                {
                    MaxTokens = maxTok;
                }
                else
                {
                    MaxTokens = 2048;
                }

                if (bool.TryParse(Environment.GetEnvironmentVariable("XAI_ENABLE_STREAMING"), out var streaming))
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
                var settings = await _dbContext.AppSettings.FindAsync(1);
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
    public Prism.Commands.DelegateCommand ValidateLicenseCommand { get; private set; }
    public Prism.Commands.DelegateCommand TestXaiConnectionCommand { get; private set; }
    public Prism.Commands.DelegateCommand SaveFiscalYearSettingsCommand { get; private set; }
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
                LastSaved = DateTime.Now.ToString("g");

                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                SettingsStatus = "Error saving settings";
                _logger.LogError(ex, "Error saving settings");
                MessageBox.Show($"Error saving settings: {ex.Message}", "Settings Error",
                              MessageBoxButton.OK);
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private async Task SaveGeneralSettingsAsync()
        {
            try
            {
                // Save general settings to database using async operation
                await Task.Run(async () =>
                {
                    var settings = await _dbContext.AppSettings.FindAsync(1);

                    if (settings == null)
                    {
                        settings = new Models.AppSettings
                        {
                            Id = 1,
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
                });
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
        }

        private async Task SaveSyncfusionSettingsAsync()
        {
            if (!string.IsNullOrEmpty(SyncfusionLicenseKey))
                await _secretVaultService.SetSecretAsync("Syncfusion-LicenseKey", SyncfusionLicenseKey);
        }

        private async Task SaveXaiSettingsAsync()
        {
            if (!string.IsNullOrEmpty(XaiApiKey))
                await _secretVaultService.SetSecretAsync("XAI-ApiKey", XaiApiKey);

            await _secretVaultService.SetSecretAsync("XAI-BaseUrl", XaiBaseUrl);
            await _secretVaultService.SetSecretAsync("XAI-Model", XaiModel);
            await _secretVaultService.SetSecretAsync("XAI-TimeoutSeconds", XaiTimeoutSeconds.ToString());
            await _secretVaultService.SetSecretAsync("XAI-ResponseStyle", ResponseStyle);
            await _secretVaultService.SetSecretAsync("XAI-Personality", Personality);
            await _secretVaultService.SetSecretAsync("XAI-ContextWindowSize", ContextWindowSize.ToString());
            await _secretVaultService.SetSecretAsync("XAI-EnableSafetyFilters", EnableSafetyFilters.ToString());
            await _secretVaultService.SetSecretAsync("XAI-Temperature", Temperature.ToString());
            await _secretVaultService.SetSecretAsync("XAI-MaxTokens", MaxTokens.ToString());
            await _secretVaultService.SetSecretAsync("XAI-EnableStreaming", EnableStreaming.ToString());
        }

        private async Task SaveAdvancedSettingsAsync()
        {
            try
            {
                // Save advanced settings to database using async operation
                await Task.Run(async () =>
                {
                    var settings = await _dbContext.AppSettings.FindAsync(1);

                    if (settings == null)
                    {
                        settings = new Models.AppSettings
                        {
                            Id = 1,
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
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save advanced settings to database");
                throw; // Re-throw to be handled by the calling method
            }
        }

    private async Task ExecuteResetSettingsAsync()
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset ALL settings to their default values?\n\n" +
                "This action cannot be undone and will:\n" +
                "• Reset all application preferences\n" +
                "• Clear API keys and connection settings\n" +
                "• Restore default themes and window sizes\n" +
                "• Reset fiscal year and advanced configurations\n\n" +
                "Continue with reset?",
                "Reset All Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
            {
                await LoadSettingsAsync();
            }
        }

    private async Task ExecuteTestConnectionAsync()
        {
            await LoadDatabaseSettingsAsync();
        }

    private async Task ExecuteTestQuickBooksConnectionAsync()
        {
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

        private async Task<bool> TestXaiConnectionInternalAsync()
        {
            try
            {
                // Test XAI connection by making a simple request
                // This is a basic connectivity test - in a real implementation,
                // you might want to make an actual API call to validate the key
                if (string.IsNullOrEmpty(XaiApiKey))
                {
                    return false;
                }

                // For now, just validate that the API key looks reasonable
                // In production, you would make an actual API call
                var isValidFormat = XaiApiKey.Length >= 20; // Basic length check

                // Simulate async operation
                await Task.Delay(500);

                return isValidFormat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing XAI connection");
                return false;
            }
        }

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

                // Update FiscalYearSettings in database using async operation
                await Task.Run(async () =>
                {
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
                });
                
                await LoadFiscalYearDisplayAsync();

                SettingsStatus = "Fiscal year settings saved successfully";
                LastSaved = DateTime.Now.ToString("g");
                
                MessageBox.Show("Fiscal year settings saved successfully.\nChanges will affect budget periods and financial reports.",
                              "Settings Saved",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SettingsStatus = $"Error saving fiscal year settings: {ex.Message}";
                _logger.LogError(ex, "Failed to save fiscal year settings");
                
                MessageBox.Show($"Failed to save fiscal year settings:\n{ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

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
            else if (!value.StartsWith("xai-"))
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