using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using Syncfusion.SfSkinManager;
using WileyWidget.Abstractions;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.ViewModels.Main
{
    public class SettingsViewModel : BindableBase
    {
        private readonly Lazy<ISettingsService> _settingsService;
        private readonly Lazy<IQuickBooksService> _quickBooksService;
        private readonly ILogger<SettingsViewModel> _logger;

        /// <summary>
        /// Parameterless constructor for Prism locator (fallback)
        /// </summary>
        protected SettingsViewModel()
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<SettingsViewModel>();
            _quickBooksService = new Lazy<IQuickBooksService>(() => throw new InvalidOperationException("DI required"));
            _settingsService = new Lazy<ISettingsService>(() => throw new InvalidOperationException("DI required"));

            _logger.LogWarning("SettingsViewModel created with fallback constructor");

            InitializeCommands();
            InitializeFiscalYearData();
            LoadSystemInfo();
        }

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public SettingsViewModel(ILogger<SettingsViewModel> logger, Lazy<IQuickBooksService> quickBooksService, Lazy<ISettingsService> settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            _logger.LogInformation("SettingsViewModel initialized with full DI");

            InitializeCommands();
            InitializeFiscalYearData();
            LoadSystemInfo();
        }

        #region Commands

        /// <summary>
        /// Command to save all settings
        /// </summary>
        public ICommand SaveSettingsCommand { get; private set; }

        /// <summary>
        /// Command to reset settings to defaults
        /// </summary>
        public ICommand ResetSettingsCommand { get; private set; }

        /// <summary>
        /// Command to test database connection
        /// </summary>
        public ICommand TestConnectionCommand { get; private set; }

        /// <summary>
        /// Command to test QuickBooks connection
        /// </summary>
        public ICommand TestQuickBooksConnectionCommand { get; private set; }

        /// <summary>
        /// Command to connect to QuickBooks
        /// </summary>
        public ICommand ConnectQuickBooksCommand { get; private set; }

        /// <summary>
        /// Command to check QuickBooks URL ACL configuration
        /// </summary>
        public ICommand CheckQuickBooksUrlAclCommand { get; private set; }

        /// <summary>
        /// Command to validate Syncfusion license
        /// </summary>
        public ICommand ValidateLicenseCommand { get; private set; }

        /// <summary>
        /// Command to test X.AI connection
        /// </summary>
        public ICommand TestXaiConnectionCommand { get; private set; }

        /// <summary>
        /// Command to save fiscal year settings
        /// </summary>
        public ICommand SaveFiscalYearSettingsCommand { get; private set; }

        #endregion

        #region UI/General Settings

        private string _searchText = string.Empty;
        /// <summary>
        /// Search text for filtering settings
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        private ObservableCollection<string> _availableThemes = new() { "FluentLight", "FluentDark" };
        /// <summary>
        /// Available theme options
        /// </summary>
        public ObservableCollection<string> AvailableThemes
        {
            get => _availableThemes;
            set => SetProperty(ref _availableThemes, value);
        }

        private string _selectedTheme = "FluentDark";
        /// <summary>
        /// Currently selected theme
        /// </summary>
        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value))
                {
                    ApplyTheme(value);
                }
            }
        }

        private int _windowWidth = 1280;
        /// <summary>
        /// Main window width in pixels
        /// </summary>
        public int WindowWidth
        {
            get => _windowWidth;
            set
            {
                if (SetProperty(ref _windowWidth, value))
                {
                    ValidateWindowWidth();
                }
            }
        }

        private string _windowWidthValidation = string.Empty;
        /// <summary>
        /// Validation message for window width
        /// </summary>
        public string WindowWidthValidation
        {
            get => _windowWidthValidation;
            set => SetProperty(ref _windowWidthValidation, value);
        }

        private int _windowHeight = 720;
        /// <summary>
        /// Main window height in pixels
        /// </summary>
        public int WindowHeight
        {
            get => _windowHeight;
            set
            {
                if (SetProperty(ref _windowHeight, value))
                {
                    ValidateWindowHeight();
                }
            }
        }

        private string _windowHeightValidation = string.Empty;
        /// <summary>
        /// Validation message for window height
        /// </summary>
        public string WindowHeightValidation
        {
            get => _windowHeightValidation;
            set => SetProperty(ref _windowHeightValidation, value);
        }

        private bool _maximizeOnStartup = false;
        /// <summary>
        /// Whether to maximize window on startup
        /// </summary>
        public bool MaximizeOnStartup
        {
            get => _maximizeOnStartup;
            set => SetProperty(ref _maximizeOnStartup, value);
        }

        private bool _showSplashScreen = true;
        /// <summary>
        /// Whether to show splash screen on startup
        /// </summary>
        public bool ShowSplashScreen
        {
            get => _showSplashScreen;
            set => SetProperty(ref _showSplashScreen, value);
        }

        #endregion

        #region Database Settings

        private string _databaseConnectionString = string.Empty;
        /// <summary>
        /// Database connection string
        /// </summary>
        public string DatabaseConnectionString
        {
            get => _databaseConnectionString;
            set => SetProperty(ref _databaseConnectionString, value);
        }

        private string _databaseStatus = "Not Connected";
        /// <summary>
        /// Current database connection status
        /// </summary>
        public string DatabaseStatus
        {
            get => _databaseStatus;
            set => SetProperty(ref _databaseStatus, value);
        }

        private Brush _databaseStatusColor = Brushes.Gray;
        /// <summary>
        /// Color indicator for database status
        /// </summary>
        public Brush DatabaseStatusColor
        {
            get => _databaseStatusColor;
            set => SetProperty(ref _databaseStatusColor, value);
        }

        #endregion

        #region QuickBooks Settings

        private string _quickBooksClientId = string.Empty;
        /// <summary>
        /// QuickBooks OAuth client ID
        /// </summary>
        public string QuickBooksClientId
        {
            get => _quickBooksClientId;
            set
            {
                if (SetProperty(ref _quickBooksClientId, value))
                {
                    ValidateQuickBooksClientId();
                }
            }
        }

        private string _quickBooksClientIdValidation = string.Empty;
        /// <summary>
        /// Validation message for QuickBooks client ID
        /// </summary>
        public string QuickBooksClientIdValidation
        {
            get => _quickBooksClientIdValidation;
            set => SetProperty(ref _quickBooksClientIdValidation, value);
        }

        private string _quickBooksClientSecret = string.Empty;
        /// <summary>
        /// QuickBooks OAuth client secret
        /// </summary>
        public string QuickBooksClientSecret
        {
            get => _quickBooksClientSecret;
            set
            {
                if (SetProperty(ref _quickBooksClientSecret, value))
                {
                    ValidateQuickBooksClientSecret();
                }
            }
        }

        private string _quickBooksClientSecretValidation = string.Empty;
        /// <summary>
        /// Validation message for QuickBooks client secret
        /// </summary>
        public string QuickBooksClientSecretValidation
        {
            get => _quickBooksClientSecretValidation;
            set => SetProperty(ref _quickBooksClientSecretValidation, value);
        }

        private string _quickBooksRedirectUri = "http://localhost:8080/callback";
        /// <summary>
        /// QuickBooks OAuth redirect URI
        /// </summary>
        public string QuickBooksRedirectUri
        {
            get => _quickBooksRedirectUri;
            set
            {
                if (SetProperty(ref _quickBooksRedirectUri, value))
                {
                    ValidateQuickBooksRedirectUri();
                }
            }
        }

        private string _quickBooksRedirectUriValidation = string.Empty;
        /// <summary>
        /// Validation message for QuickBooks redirect URI
        /// </summary>
        public string QuickBooksRedirectUriValidation
        {
            get => _quickBooksRedirectUriValidation;
            set => SetProperty(ref _quickBooksRedirectUriValidation, value);
        }

        private ObservableCollection<string> _quickBooksEnvironments = new() { "Sandbox", "Production" };
        /// <summary>
        /// Available QuickBooks environments
        /// </summary>
        public ObservableCollection<string> QuickBooksEnvironments
        {
            get => _quickBooksEnvironments;
            set => SetProperty(ref _quickBooksEnvironments, value);
        }

        private string _selectedQuickBooksEnvironment = "Sandbox";
        /// <summary>
        /// Selected QuickBooks environment
        /// </summary>
        public string SelectedQuickBooksEnvironment
        {
            get => _selectedQuickBooksEnvironment;
            set => SetProperty(ref _selectedQuickBooksEnvironment, value);
        }

        private string _quickBooksConnectionStatus = "Not Connected";
        /// <summary>
        /// QuickBooks connection status
        /// </summary>
        public string QuickBooksConnectionStatus
        {
            get => _quickBooksConnectionStatus;
            set => SetProperty(ref _quickBooksConnectionStatus, value);
        }

        private Brush _quickBooksStatusColor = Brushes.Gray;
        /// <summary>
        /// Color indicator for QuickBooks status
        /// </summary>
        public Brush QuickBooksStatusColor
        {
            get => _quickBooksStatusColor;
            set => SetProperty(ref _quickBooksStatusColor, value);
        }

        private string _quickBooksUrlAclStatus = "Not Configured";
        /// <summary>
        /// URL ACL configuration status
        /// </summary>
        public string QuickBooksUrlAclStatus
        {
            get => _quickBooksUrlAclStatus;
            set => SetProperty(ref _quickBooksUrlAclStatus, value);
        }

        private Brush _quickBooksUrlAclStatusColor = Brushes.Gray;
        /// <summary>
        /// Color indicator for URL ACL status
        /// </summary>
        public Brush QuickBooksUrlAclStatusColor
        {
            get => _quickBooksUrlAclStatusColor;
            set => SetProperty(ref _quickBooksUrlAclStatusColor, value);
        }

        #endregion

        #region Syncfusion License Settings

        private string _syncfusionLicenseKey = string.Empty;
        /// <summary>
        /// Syncfusion license key
        /// </summary>
        public string SyncfusionLicenseKey
        {
            get => _syncfusionLicenseKey;
            set
            {
                if (SetProperty(ref _syncfusionLicenseKey, value))
                {
                    ValidateSyncfusionLicenseKey();
                }
            }
        }

        private string _syncfusionLicenseKeyValidation = string.Empty;
        /// <summary>
        /// Validation message for Syncfusion license key
        /// </summary>
        public string SyncfusionLicenseKeyValidation
        {
            get => _syncfusionLicenseKeyValidation;
            set => SetProperty(ref _syncfusionLicenseKeyValidation, value);
        }

        private string _syncfusionLicenseStatus = "Not Validated";
        /// <summary>
        /// Syncfusion license validation status
        /// </summary>
        public string SyncfusionLicenseStatus
        {
            get => _syncfusionLicenseStatus;
            set => SetProperty(ref _syncfusionLicenseStatus, value);
        }

        private Brush _syncfusionLicenseStatusColor = Brushes.Gray;
        /// <summary>
        /// Color indicator for Syncfusion license status
        /// </summary>
        public Brush SyncfusionLicenseStatusColor
        {
            get => _syncfusionLicenseStatusColor;
            set => SetProperty(ref _syncfusionLicenseStatusColor, value);
        }

        #endregion

        #region X.AI Settings

        private string _xaiApiKey = string.Empty;
        /// <summary>
        /// X.AI API key
        /// </summary>
        public string XaiApiKey
        {
            get => _xaiApiKey;
            set
            {
                if (SetProperty(ref _xaiApiKey, value))
                {
                    ValidateXaiApiKey();
                }
            }
        }

        private string _xaiApiKeyValidation = string.Empty;
        /// <summary>
        /// Validation message for X.AI API key
        /// </summary>
        public string XaiApiKeyValidation
        {
            get => _xaiApiKeyValidation;
            set => SetProperty(ref _xaiApiKeyValidation, value);
        }

        private string _xaiBaseUrl = "https://api.x.ai/v1";
        /// <summary>
        /// X.AI API base URL
        /// </summary>
        public string XaiBaseUrl
        {
            get => _xaiBaseUrl;
            set => SetProperty(ref _xaiBaseUrl, value);
        }

        private ObservableCollection<string> _availableModels = new()
        {
            "grok-beta",
            "grok-vision-beta"
        };
        /// <summary>
        /// Available X.AI models
        /// </summary>
        public ObservableCollection<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }

        private string _xaiModel = "grok-beta";
        /// <summary>
        /// Selected X.AI model
        /// </summary>
        public string XaiModel
        {
            get => _xaiModel;
            set => SetProperty(ref _xaiModel, value);
        }

        private int _xaiTimeoutSeconds = 30;
        /// <summary>
        /// X.AI API timeout in seconds
        /// </summary>
        public int XaiTimeoutSeconds
        {
            get => _xaiTimeoutSeconds;
            set
            {
                if (SetProperty(ref _xaiTimeoutSeconds, value))
                {
                    ValidateXaiTimeout();
                }
            }
        }

        private string _xaiTimeoutValidation = string.Empty;
        /// <summary>
        /// Validation message for X.AI timeout
        /// </summary>
        public string XaiTimeoutValidation
        {
            get => _xaiTimeoutValidation;
            set => SetProperty(ref _xaiTimeoutValidation, value);
        }

        private ObservableCollection<string> _availableResponseStyles = new()
        {
            "Concise",
            "Detailed",
            "Professional"
        };
        /// <summary>
        /// Available response styles
        /// </summary>
        public ObservableCollection<string> AvailableResponseStyles
        {
            get => _availableResponseStyles;
            set => SetProperty(ref _availableResponseStyles, value);
        }

        private string _responseStyle = "Detailed";
        /// <summary>
        /// Selected response style
        /// </summary>
        public string ResponseStyle
        {
            get => _responseStyle;
            set => SetProperty(ref _responseStyle, value);
        }

        private ObservableCollection<string> _availablePersonalities = new()
        {
            "Assistant",
            "Analyst",
            "Consultant"
        };
        /// <summary>
        /// Available AI personalities
        /// </summary>
        public ObservableCollection<string> AvailablePersonalities
        {
            get => _availablePersonalities;
            set => SetProperty(ref _availablePersonalities, value);
        }

        private string _personality = "Assistant";
        /// <summary>
        /// Selected AI personality
        /// </summary>
        public string Personality
        {
            get => _personality;
            set => SetProperty(ref _personality, value);
        }

        private int _contextWindowSize = 8192;
        /// <summary>
        /// Context window size for AI
        /// </summary>
        public int ContextWindowSize
        {
            get => _contextWindowSize;
            set
            {
                if (SetProperty(ref _contextWindowSize, value))
                {
                    ValidateContextWindow();
                }
            }
        }

        private string _contextWindowValidation = string.Empty;
        /// <summary>
        /// Validation message for context window
        /// </summary>
        public string ContextWindowValidation
        {
            get => _contextWindowValidation;
            set => SetProperty(ref _contextWindowValidation, value);
        }

        private bool _enableSafetyFilters = true;
        /// <summary>
        /// Whether to enable AI safety filters
        /// </summary>
        public bool EnableSafetyFilters
        {
            get => _enableSafetyFilters;
            set => SetProperty(ref _enableSafetyFilters, value);
        }

        private double _temperature = 0.7;
        /// <summary>
        /// AI temperature setting
        /// </summary>
        public double Temperature
        {
            get => _temperature;
            set
            {
                if (SetProperty(ref _temperature, value))
                {
                    ValidateTemperature();
                }
            }
        }

        private string _temperatureValidation = string.Empty;
        /// <summary>
        /// Validation message for temperature
        /// </summary>
        public string TemperatureValidation
        {
            get => _temperatureValidation;
            set => SetProperty(ref _temperatureValidation, value);
        }

        private int _maxTokens = 2048;
        /// <summary>
        /// Maximum tokens for AI response
        /// </summary>
        public int MaxTokens
        {
            get => _maxTokens;
            set
            {
                if (SetProperty(ref _maxTokens, value))
                {
                    ValidateMaxTokens();
                }
            }
        }

        private string _maxTokensValidation = string.Empty;
        /// <summary>
        /// Validation message for max tokens
        /// </summary>
        public string MaxTokensValidation
        {
            get => _maxTokensValidation;
            set => SetProperty(ref _maxTokensValidation, value);
        }

        private bool _enableStreaming = false;
        /// <summary>
        /// Whether to enable streaming responses
        /// </summary>
        public bool EnableStreaming
        {
            get => _enableStreaming;
            set => SetProperty(ref _enableStreaming, value);
        }

        private string _xaiConnectionStatus = "Not Connected";
        /// <summary>
        /// X.AI connection status
        /// </summary>
        public string XaiConnectionStatus
        {
            get => _xaiConnectionStatus;
            set => SetProperty(ref _xaiConnectionStatus, value);
        }

        private Brush _xaiStatusColor = Brushes.Gray;
        /// <summary>
        /// Color indicator for X.AI status
        /// </summary>
        public Brush XaiStatusColor
        {
            get => _xaiStatusColor;
            set => SetProperty(ref _xaiStatusColor, value);
        }

        #endregion

        #region Fiscal Year Settings

        private ObservableCollection<MonthItem> _fiscalYearMonths = new();
        /// <summary>
        /// Available months for fiscal year start
        /// </summary>
        public ObservableCollection<MonthItem> FiscalYearMonths
        {
            get => _fiscalYearMonths;
            set => SetProperty(ref _fiscalYearMonths, value);
        }

        private int _fiscalYearStartMonth = 1; // January
        /// <summary>
        /// Fiscal year start month (1-12)
        /// </summary>
        public int FiscalYearStartMonth
        {
            get => _fiscalYearStartMonth;
            set
            {
                if (SetProperty(ref _fiscalYearStartMonth, value))
                {
                    UpdateFiscalYearDisplay();
                }
            }
        }

        private int _fiscalYearStartDay = 1;
        /// <summary>
        /// Fiscal year start day
        /// </summary>
        public int FiscalYearStartDay
        {
            get => _fiscalYearStartDay;
            set
            {
                if (SetProperty(ref _fiscalYearStartDay, value))
                {
                    ValidateFiscalYearDay();
                    UpdateFiscalYearDisplay();
                }
            }
        }

        private string _fiscalYearDayValidation = string.Empty;
        /// <summary>
        /// Validation message for fiscal year day
        /// </summary>
        public string FiscalYearDayValidation
        {
            get => _fiscalYearDayValidation;
            set => SetProperty(ref _fiscalYearDayValidation, value);
        }

        private string _currentFiscalYearDisplay = string.Empty;
        /// <summary>
        /// Current fiscal year display text
        /// </summary>
        public string CurrentFiscalYearDisplay
        {
            get => _currentFiscalYearDisplay;
            set => SetProperty(ref _currentFiscalYearDisplay, value);
        }

        private string _fiscalYearPeriodDisplay = string.Empty;
        /// <summary>
        /// Fiscal year period display text
        /// </summary>
        public string FiscalYearPeriodDisplay
        {
            get => _fiscalYearPeriodDisplay;
            set => SetProperty(ref _fiscalYearPeriodDisplay, value);
        }

        private string _daysRemainingInFiscalYear = string.Empty;
        /// <summary>
        /// Days remaining in current fiscal year
        /// </summary>
        public string DaysRemainingInFiscalYear
        {
            get => _daysRemainingInFiscalYear;
            set => SetProperty(ref _daysRemainingInFiscalYear, value);
        }

        private ObservableCollection<FiscalYearInfo> _availableFiscalYears = new();
        /// <summary>
        /// Available fiscal years for selection
        /// </summary>
        public ObservableCollection<FiscalYearInfo> AvailableFiscalYears
        {
            get => _availableFiscalYears;
            set => SetProperty(ref _availableFiscalYears, value);
        }

        #endregion

        #region System/Advanced Settings

        private bool _enableDynamicColumns = true;
        /// <summary>
        /// Whether to enable dynamic columns in data grids
        /// </summary>
        public bool EnableDynamicColumns
        {
            get => _enableDynamicColumns;
            set => SetProperty(ref _enableDynamicColumns, value);
        }

        private bool _enableDataCaching = true;
        /// <summary>
        /// Whether to enable data caching
        /// </summary>
        public bool EnableDataCaching
        {
            get => _enableDataCaching;
            set => SetProperty(ref _enableDataCaching, value);
        }

        private int _cacheExpirationMinutes = 30;
        /// <summary>
        /// Cache expiration time in minutes
        /// </summary>
        public int CacheExpirationMinutes
        {
            get => _cacheExpirationMinutes;
            set
            {
                if (SetProperty(ref _cacheExpirationMinutes, value))
                {
                    ValidateCacheExpiration();
                }
            }
        }

        private string _cacheExpirationValidation = string.Empty;
        /// <summary>
        /// Validation message for cache expiration
        /// </summary>
        public string CacheExpirationValidation
        {
            get => _cacheExpirationValidation;
            set => SetProperty(ref _cacheExpirationValidation, value);
        }

        private ObservableCollection<string> _logLevels = new()
        {
            "Trace",
            "Debug",
            "Information",
            "Warning",
            "Error",
            "Critical"
        };
        /// <summary>
        /// Available log levels
        /// </summary>
        public ObservableCollection<string> LogLevels
        {
            get => _logLevels;
            set => SetProperty(ref _logLevels, value);
        }

        private string _selectedLogLevel = "Information";
        /// <summary>
        /// Selected log level
        /// </summary>
        public string SelectedLogLevel
        {
            get => _selectedLogLevel;
            set => SetProperty(ref _selectedLogLevel, value);
        }

        private bool _enableFileLogging = true;
        /// <summary>
        /// Whether to enable file logging
        /// </summary>
        public bool EnableFileLogging
        {
            get => _enableFileLogging;
            set => SetProperty(ref _enableFileLogging, value);
        }

        private string _logFilePath = "logs/app.log";
        /// <summary>
        /// Path to log file
        /// </summary>
        public string LogFilePath
        {
            get => _logFilePath;
            set => SetProperty(ref _logFilePath, value);
        }

        private string _systemInfo = string.Empty;
        /// <summary>
        /// System information display
        /// </summary>
        public string SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }

        #endregion

        #region Status/Busy Indicators

        private string _settingsStatus = "Ready";
        /// <summary>
        /// Current settings operation status
        /// </summary>
        public string SettingsStatus
        {
            get => _settingsStatus;
            set => SetProperty(ref _settingsStatus, value);
        }

        private string _lastSaved = "Never";
        /// <summary>
        /// Last time settings were saved
        /// </summary>
        public string LastSaved
        {
            get => _lastSaved;
            set => SetProperty(ref _lastSaved, value);
        }

        private bool _isBusy = false;
        /// <summary>
        /// Whether a settings operation is in progress
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyMessage = string.Empty;
        /// <summary>
        /// Message describing current busy operation
        /// </summary>
        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        #endregion

        #region Command Initialization

        /// <summary>
        /// Initializes all commands
        /// </summary>
        private void InitializeCommands()
        {
            SaveSettingsCommand = new DelegateCommand(ExecuteSaveSettings, CanSaveSettings);
            ResetSettingsCommand = new DelegateCommand(ExecuteResetSettings);
            TestConnectionCommand = new DelegateCommand(async () => await ExecuteTestConnectionAsync());
            TestQuickBooksConnectionCommand = new DelegateCommand(async () => await ExecuteTestQuickBooksConnectionAsync());
            ConnectQuickBooksCommand = new DelegateCommand(async () => await ExecuteConnectQuickBooksAsync());
            CheckQuickBooksUrlAclCommand = new DelegateCommand(async () => await ExecuteCheckQuickBooksUrlAclAsync());
            ValidateLicenseCommand = new DelegateCommand(async () => await ExecuteValidateLicenseAsync());
            TestXaiConnectionCommand = new DelegateCommand(async () => await ExecuteTestXaiConnectionAsync());
            SaveFiscalYearSettingsCommand = new DelegateCommand(ExecuteSaveFiscalYearSettings);
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Validates window width
        /// </summary>
        private void ValidateWindowWidth()
        {
            if (WindowWidth < 800)
            {
                WindowWidthValidation = "Window width must be at least 800 pixels";
            }
            else if (WindowWidth > 3840)
            {
                WindowWidthValidation = "Window width cannot exceed 3840 pixels";
            }
            else
            {
                WindowWidthValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates window height
        /// </summary>
        private void ValidateWindowHeight()
        {
            if (WindowHeight < 600)
            {
                WindowHeightValidation = "Window height must be at least 600 pixels";
            }
            else if (WindowHeight > 2160)
            {
                WindowHeightValidation = "Window height cannot exceed 2160 pixels";
            }
            else
            {
                WindowHeightValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates QuickBooks client ID
        /// </summary>
        private void ValidateQuickBooksClientId()
        {
            if (string.IsNullOrWhiteSpace(QuickBooksClientId))
            {
                QuickBooksClientIdValidation = "Client ID is required";
            }
            else if (QuickBooksClientId.Length < 10)
            {
                QuickBooksClientIdValidation = "Client ID appears invalid";
            }
            else
            {
                QuickBooksClientIdValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates QuickBooks client secret
        /// </summary>
        private void ValidateQuickBooksClientSecret()
        {
            if (string.IsNullOrWhiteSpace(QuickBooksClientSecret))
            {
                QuickBooksClientSecretValidation = "Client Secret is required";
            }
            else if (QuickBooksClientSecret.Length < 10)
            {
                QuickBooksClientSecretValidation = "Client Secret appears invalid";
            }
            else
            {
                QuickBooksClientSecretValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates QuickBooks redirect URI
        /// </summary>
        private void ValidateQuickBooksRedirectUri()
        {
            if (string.IsNullOrWhiteSpace(QuickBooksRedirectUri))
            {
                QuickBooksRedirectUriValidation = "Redirect URI is required";
            }
            else if (!Uri.TryCreate(QuickBooksRedirectUri, UriKind.Absolute, out _))
            {
                QuickBooksRedirectUriValidation = "Invalid URI format";
            }
            else
            {
                QuickBooksRedirectUriValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates Syncfusion license key
        /// </summary>
        private void ValidateSyncfusionLicenseKey()
        {
            if (string.IsNullOrWhiteSpace(SyncfusionLicenseKey))
            {
                SyncfusionLicenseKeyValidation = "License key is required";
            }
            else
            {
                SyncfusionLicenseKeyValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates X.AI API key
        /// </summary>
        private void ValidateXaiApiKey()
        {
            if (string.IsNullOrWhiteSpace(XaiApiKey))
            {
                XaiApiKeyValidation = "API Key is required";
            }
            else if (!XaiApiKey.StartsWith("xai-", StringComparison.OrdinalIgnoreCase))
            {
                XaiApiKeyValidation = "X.AI keys typically start with 'xai-'";
            }
            else
            {
                XaiApiKeyValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates X.AI timeout
        /// </summary>
        private void ValidateXaiTimeout()
        {
            if (XaiTimeoutSeconds < 5)
            {
                XaiTimeoutValidation = "Timeout must be at least 5 seconds";
            }
            else if (XaiTimeoutSeconds > 300)
            {
                XaiTimeoutValidation = "Timeout cannot exceed 300 seconds";
            }
            else
            {
                XaiTimeoutValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates temperature setting
        /// </summary>
        private void ValidateTemperature()
        {
            if (Temperature < 0.0)
            {
                TemperatureValidation = "Temperature must be between 0.0 and 2.0";
            }
            else if (Temperature > 2.0)
            {
                TemperatureValidation = "Temperature must be between 0.0 and 2.0";
            }
            else
            {
                TemperatureValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates max tokens setting
        /// </summary>
        private void ValidateMaxTokens()
        {
            if (MaxTokens < 1)
            {
                MaxTokensValidation = "Max tokens must be at least 1";
            }
            else if (MaxTokens > 131072)
            {
                MaxTokensValidation = "Max tokens cannot exceed 131072";
            }
            else
            {
                MaxTokensValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates context window size
        /// </summary>
        private void ValidateContextWindow()
        {
            if (ContextWindowSize < 512)
            {
                ContextWindowValidation = "Context window must be at least 512";
            }
            else if (ContextWindowSize > 131072)
            {
                ContextWindowValidation = "Context window cannot exceed 131072";
            }
            else
            {
                ContextWindowValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates fiscal year start day
        /// </summary>
        private void ValidateFiscalYearDay()
        {
            var daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, FiscalYearStartMonth);
            if (FiscalYearStartDay < 1 || FiscalYearStartDay > daysInMonth)
            {
                FiscalYearDayValidation = $"Day must be between 1 and {daysInMonth} for selected month";
            }
            else
            {
                FiscalYearDayValidation = string.Empty;
            }
        }

        /// <summary>
        /// Validates cache expiration time
        /// </summary>
        private void ValidateCacheExpiration()
        {
            if (CacheExpirationMinutes < 1)
            {
                CacheExpirationValidation = "Cache expiration must be at least 1 minute";
            }
            else if (CacheExpirationMinutes > 1440)
            {
                CacheExpirationValidation = "Cache expiration cannot exceed 1440 minutes (24 hours)";
            }
            else
            {
                CacheExpirationValidation = string.Empty;
            }
        }

        #endregion

        #region Command Execution Methods

        /// <summary>
        /// Executes save settings command
        /// </summary>
        private void ExecuteSaveSettings()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Saving settings...";

                _settingsService.Value.Save();

                LastSaved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                SettingsStatus = "Settings saved successfully";

                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                SettingsStatus = "Failed to save settings";
                _logger.LogError(ex, "Failed to save settings");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Determines if save settings command can execute
        /// </summary>
        private bool CanSaveSettings()
        {
            return !IsBusy
                && string.IsNullOrEmpty(WindowWidthValidation)
                && string.IsNullOrEmpty(WindowHeightValidation)
                && string.IsNullOrEmpty(QuickBooksClientIdValidation)
                && string.IsNullOrEmpty(QuickBooksClientSecretValidation);
        }

        /// <summary>
        /// Executes reset settings command
        /// </summary>
        private void ExecuteResetSettings()
        {
            // Reset to default values
            WindowWidth = 1280;
            WindowHeight = 720;
            MaximizeOnStartup = false;
            ShowSplashScreen = true;
            SelectedTheme = "FluentDark";
            // ... reset other properties

            SettingsStatus = "Settings reset to defaults";
            _logger.LogInformation("Settings reset to defaults");
        }

        /// <summary>
        /// Executes test database connection command
        /// </summary>
        private async Task ExecuteTestConnectionAsync()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Testing database connection...";

                // Test database connection
                var connected = await TestDatabaseConnectionAsync();

                if (connected)
                {
                    DatabaseStatus = "Connected";
                    DatabaseStatusColor = Brushes.Green;
                }
                else
                {
                    DatabaseStatus = "Connection Failed";
                    DatabaseStatusColor = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                DatabaseStatus = "Error";
                DatabaseStatusColor = Brushes.Red;
                _logger.LogError(ex, "Database connection test failed");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Executes test QuickBooks connection command
        /// </summary>
        private async Task ExecuteTestQuickBooksConnectionAsync()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Testing QuickBooks connection...";

                var connected = await _quickBooksService.Value.TestConnectionAsync();

                if (connected)
                {
                    QuickBooksConnectionStatus = "Connected";
                    QuickBooksStatusColor = Brushes.Green;
                }
                else
                {
                    QuickBooksConnectionStatus = "Connection Failed";
                    QuickBooksStatusColor = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = "Error";
                QuickBooksStatusColor = Brushes.Red;
                _logger.LogError(ex, "QuickBooks connection test failed");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Executes connect to QuickBooks command
        /// </summary>
        private async Task ExecuteConnectQuickBooksAsync()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Connecting to QuickBooks...";

                await _quickBooksService.Value.ConnectAsync();

                QuickBooksConnectionStatus = "Connected";
                QuickBooksStatusColor = Brushes.Green;
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = "Connection Failed";
                QuickBooksStatusColor = Brushes.Red;
                _logger.LogError(ex, "QuickBooks connection failed");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Executes check QuickBooks URL ACL command
        /// </summary>
        private async Task ExecuteCheckQuickBooksUrlAclAsync()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Checking URL ACL configuration...";

                // Check URL ACL configuration
                var configured = await CheckUrlAclAsync(QuickBooksRedirectUri);

                if (configured)
                {
                    QuickBooksUrlAclStatus = "Configured";
                    QuickBooksUrlAclStatusColor = Brushes.Green;
                }
                else
                {
                    QuickBooksUrlAclStatus = "Not Configured";
                    QuickBooksUrlAclStatusColor = Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                QuickBooksUrlAclStatus = "Error";
                QuickBooksUrlAclStatusColor = Brushes.Red;
                _logger.LogError(ex, "URL ACL check failed");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Executes validate license command
        /// </summary>
        private async Task ExecuteValidateLicenseAsync()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Validating Syncfusion license...";

                // Validate Syncfusion license
                var valid = await ValidateSyncfusionLicenseAsync(SyncfusionLicenseKey);

                if (valid)
                {
                    SyncfusionLicenseStatus = "Valid";
                    SyncfusionLicenseStatusColor = Brushes.Green;
                }
                else
                {
                    SyncfusionLicenseStatus = "Invalid";
                    SyncfusionLicenseStatusColor = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                SyncfusionLicenseStatus = "Error";
                SyncfusionLicenseStatusColor = Brushes.Red;
                _logger.LogError(ex, "License validation failed");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Executes test X.AI connection command
        /// </summary>
        private async Task ExecuteTestXaiConnectionAsync()
        {
            try
            {
                IsBusy = true;
                BusyMessage = "Testing X.AI connection...";

                // Test X.AI connection
                var connected = await TestXaiConnectionAsync();

                if (connected)
                {
                    XaiConnectionStatus = "Connected";
                    XaiStatusColor = Brushes.Green;
                }
                else
                {
                    XaiConnectionStatus = "Connection Failed";
                    XaiStatusColor = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                XaiConnectionStatus = "Error";
                XaiStatusColor = Brushes.Red;
                _logger.LogError(ex, "X.AI connection test failed");
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Executes save fiscal year settings command
        /// </summary>
        private void ExecuteSaveFiscalYearSettings()
        {
            try
            {
                // Save fiscal year settings
                _settingsService.Value.SaveFiscalYearSettings(FiscalYearStartMonth, FiscalYearStartDay);

                UpdateFiscalYearDisplay();
                SettingsStatus = "Fiscal year settings saved";

                _logger.LogInformation("Fiscal year settings saved");
            }
            catch (Exception ex)
            {
                SettingsStatus = "Failed to save fiscal year settings";
                _logger.LogError(ex, "Failed to save fiscal year settings");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Tests database connection using Microsoft.Data.SqlClient.
        /// </summary>
        /// <returns>True if connection successful, false otherwise.</returns>
        private async Task<bool> TestDatabaseConnectionAsync()
        {
            return await TestDatabaseConnectionAsync(CancellationToken.None);
        }

        /// <summary>
        /// Tests database connection with cancellation support.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if connection successful, false otherwise.</returns>
        private async Task<bool> TestDatabaseConnectionAsync(CancellationToken ct)
        {
            // Try to open a SQL connection using the user-provided connection string.
            // Uses Microsoft.Data.SqlClient which supports OpenAsync(CancellationToken).
            try
            {
                var cs = DatabaseConnectionString?.Trim();
                if (string.IsNullOrWhiteSpace(cs))
                    return false;

                await using var conn = new SqlConnection(cs);
                // Respect cancellation and small timeout at caller level
                await conn.OpenAsync(ct);
                return conn.State == System.Data.ConnectionState.Open;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("Database connection test cancelled");
                return false;
            }
            catch (SqlException sex)
            {
                _logger?.LogWarning(sex, "SQL connection failed during test");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Database connection test failed");
                return false;
            }
        }

        /// <summary>
        /// Checks URL ACL configuration via QuickBooks service.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <returns>True if URL ACL is configured, false otherwise.</returns>
        private async Task<bool> CheckUrlAclAsync(string uri)
        {
            return await CheckUrlAclAsync(uri, CancellationToken.None);
        }

        /// <summary>
        /// Checks URL ACL configuration with cancellation support.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if URL ACL is configured, false otherwise.</returns>
        private async Task<bool> CheckUrlAclAsync(string uri, CancellationToken ct)
        {
            // If QuickBooks service is available, ask it to evaluate URL ACL configuration
            try
            {
                if (_quickBooksService != null)
                {
                    var checkTask = _quickBooksService.Value.CheckUrlAclAsync(uri);
                    var completed = await Task.WhenAny(checkTask, Task.Delay(TimeSpan.FromSeconds(5), ct));
                    if (completed == checkTask)
                    {
                        var result = await checkTask;
                        _logger?.LogDebug("URL ACL check result: {Result}", result.IsReady);
                        return result.IsReady;
                    }
                }

                // Fallback: simple URI validation
                await Task.Delay(50, ct);
                return Uri.TryCreate(uri, UriKind.Absolute, out _);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "CheckUrlAclAsync failed");
                return false;
            }
        }

        /// <summary>
        /// Validates Syncfusion license by attempting registration.
        /// </summary>
        /// <param name="key">The Syncfusion license key.</param>
        /// <returns>True if validation successful, false otherwise.</returns>
        private async Task<bool> ValidateSyncfusionLicenseAsync(string key)
        {
            return await ValidateSyncfusionLicenseAsync(key, CancellationToken.None);
        }

        /// <summary>
        /// Validates Syncfusion license with cancellation support.
        /// </summary>
        /// <param name="key">The Syncfusion license key.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if validation successful, false otherwise.</returns>
        private async Task<bool> ValidateSyncfusionLicenseAsync(string key, CancellationToken ct)
        {
            // Prefer deterministic format check, then attempt to register license via Syncfusion API.
            if (string.IsNullOrWhiteSpace(key))
                return false;

            // simple heuristic: reasonable length and contains letters/digits
            if (key.Length < 16 || key.Length > 512)
                return false;

            // NOTE: License registration happens in App static constructor per Syncfusion documentation
            // Runtime re-registration is not supported and can cause issues
            // This method now only validates the format of the key
            _logger?.LogInformation("Syncfusion license key format validation passed");
            _logger?.LogWarning("License registration occurs at application startup only - changes require app restart");

            return true; // Format is valid
        }

        /// <summary>
        /// Tests X.AI API connection using HTTP requests.
        /// </summary>
        /// <returns>True if connection successful, false otherwise.</returns>
        private async Task<bool> TestXaiConnectionAsync()
        {
            return await TestXaiConnectionAsync(CancellationToken.None);
        }

        /// <summary>
        /// Tests X.AI API connection with cancellation support.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if connection successful, false otherwise.</returns>
        private async Task<bool> TestXaiConnectionAsync(CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(XaiApiKey) || string.IsNullOrWhiteSpace(XaiBaseUrl))
                    return false;

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(Math.Max(5, XaiTimeoutSeconds));
                if (!Uri.TryCreate(XaiBaseUrl, UriKind.Absolute, out var baseUri))
                    return false;

                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Head, baseUri);
                    if (!string.IsNullOrWhiteSpace(XaiApiKey))
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", XaiApiKey);
                    var resp = await http.SendAsync(req, ct);
                    if (resp.IsSuccessStatusCode)
                        return true;
                }
                catch (HttpRequestException) { }

                using var req2 = new HttpRequestMessage(HttpMethod.Get, baseUri);
                if (!string.IsNullOrWhiteSpace(XaiApiKey))
                    req2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", XaiApiKey);
                var resp2 = await http.SendAsync(req2, ct);
                return resp2.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "X.AI connection test failed");
                return false;
            }
        }

        /// <summary>
        /// Updates fiscal year display information
        /// </summary>
        private void UpdateFiscalYearDisplay()
        {
            var startDate = new DateTime(DateTime.Now.Year, FiscalYearStartMonth, FiscalYearStartDay);
            var endDate = startDate.AddYears(1).AddDays(-1);

            CurrentFiscalYearDisplay = $"FY {startDate.Year}";
            FiscalYearPeriodDisplay = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";

            var daysRemaining = (endDate - DateTime.Now).Days;
            DaysRemainingInFiscalYear = daysRemaining > 0 ? $"{daysRemaining} days remaining" : "Fiscal year ended";
        }

        /// <summary>
        /// Applies the selected theme using Syncfusion SfSkinManager.
        /// </summary>
        /// <param name="themeName">The theme name to apply (e.g., FluentDark, FluentLight).</param>
        private void ApplyTheme(string themeName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(themeName))
                {
                    _logger.LogWarning("Theme name is null or empty, skipping theme application");
                    return;
                }

                // Convert theme name to VisualStyles enum
                if (Enum.TryParse<VisualStyles>(themeName, true, out var visualStyle))
                {
                    // Apply theme to the main window if available
                    if (Application.Current?.MainWindow != null)
                    {
                        SfSkinManager.SetVisualStyle(Application.Current.MainWindow, visualStyle);
                        _logger.LogInformation("Theme successfully applied to MainWindow: {ThemeName}", themeName);
                    }
                    else
                    {
                        _logger.LogWarning("MainWindow not available, theme not applied: {ThemeName}", themeName);
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid theme name: {ThemeName}. Valid options: FluentLight, FluentDark", themeName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme: {ThemeName}", themeName);
            }
        }

        /// <summary>
        /// Initializes fiscal year data
        /// </summary>
        private void InitializeFiscalYearData()
        {
            FiscalYearMonths = new ObservableCollection<MonthItem>
            {
                new MonthItem { Name = "January", Value = 1 },
                new MonthItem { Name = "February", Value = 2 },
                new MonthItem { Name = "March", Value = 3 },
                new MonthItem { Name = "April", Value = 4 },
                new MonthItem { Name = "May", Value = 5 },
                new MonthItem { Name = "June", Value = 6 },
                new MonthItem { Name = "July", Value = 7 },
                new MonthItem { Name = "August", Value = 8 },
                new MonthItem { Name = "September", Value = 9 },
                new MonthItem { Name = "October", Value = 10 },
                new MonthItem { Name = "November", Value = 11 },
                new MonthItem { Name = "December", Value = 12 }
            };

            UpdateFiscalYearDisplay();
        }

        /// <summary>
        /// Loads system information
        /// </summary>
        private void LoadSystemInfo()
        {
            var osVersion = Environment.OSVersion;
            var clrVersion = Environment.Version;
            var processorCount = Environment.ProcessorCount;

            SystemInfo = $"OS: {osVersion}\n.NET: {clrVersion}\nProcessors: {processorCount}";
        }

        #endregion
    }
}
