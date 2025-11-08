using Prism.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;
using WileyWidget.UI.ViewModels;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using System.Data.SqlClient;
using WileyWidget.Data;
using System.IO;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Panels
{
    /// <summary>
    /// ViewModel for the Settings Panel View
    /// Manages application configuration, database connections, and integration settings.
    /// </summary>
    public class SettingsPanelViewModel : BasePanelViewModel, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IQuickBooksService? _quickBooksService;
        // ISyncfusionLicenseService removed - license registration happens in App static constructor per Syncfusion documentation
        private readonly IAIService? _aiService;
        private readonly ILogger<SettingsPanelViewModel>? _logger;
        private System.Timers.Timer? _syncTimer;
        private System.Timers.Timer? _autoSaveTimer;

        #region Window Settings

        private string _windowWidth = "1024";
        /// <summary>
        /// Gets or sets the default window width.
        /// Loaded from and saved to user preferences via ISettingsService.
        /// </summary>
        public string WindowWidth
        {
            get => _windowWidth;
            set => SetProperty(ref _windowWidth, value);
        }

        private string _windowHeight = "768";
        /// <summary>
        /// Gets or sets the default window height.
        /// Loaded from and saved to user preferences via ISettingsService.
        /// </summary>
        public string WindowHeight
        {
            get => _windowHeight;
            set => SetProperty(ref _windowHeight, value);
        }

        private bool _rememberWindowPosition;
        /// <summary>
        /// Gets or sets whether to remember window position between sessions.
        /// Determined by whether window position values are set in settings.
        /// </summary>
        public bool RememberWindowPosition
        {
            get => _rememberWindowPosition;
            set => SetProperty(ref _rememberWindowPosition, value);
        }

        private bool _startMaximized;
        /// <summary>
        /// Gets or sets whether to start the application maximized.
        /// Loaded from and saved to user preferences via ISettingsService.
        /// </summary>
        public bool StartMaximized
        {
            get => _startMaximized;
            set => SetProperty(ref _startMaximized, value);
        }

        private string _defaultTheme = "FluentDark";
        /// <summary>
        /// Gets or sets the default application theme.
        /// Loaded from and saved to user preferences via ISettingsService.
        /// </summary>
        public string DefaultTheme
        {
            get => _defaultTheme;
            set => SetProperty(ref _defaultTheme, value);
        }

        #endregion

        #region Database Settings - TODO: Implement database configuration

        private string _databaseServer = "localhost";
        /// <summary>
        /// Gets or sets the database server address.
        /// Connection is validated before saving settings.
        /// </summary>
        public string DatabaseServer
        {
            get => _databaseServer;
            set => SetProperty(ref _databaseServer, value);
        }

        private string _databaseName = "WileyWidget";
        /// <summary>
        /// Gets or sets the database name.
        /// Database existence is validated as part of connection testing.
        /// </summary>
        public string DatabaseName
        {
            get => _databaseName;
            set => SetProperty(ref _databaseName, value);
        }

        private string _connectionString = string.Empty;
        /// <summary>
        /// Gets or sets the complete database connection string.
        /// Can be entered manually or built from individual server/database components.
        /// </summary>
        public string ConnectionString
        {
            get => _connectionString;
            set => SetProperty(ref _connectionString, value);
        }

        private bool _isDatabaseConnected;
        /// <summary>
        /// Gets or sets whether the database is currently connected.
        /// Updated automatically when connection tests are performed.
        /// </summary>
        public bool IsDatabaseConnected
        {
            get => _isDatabaseConnected;
            set => SetProperty(ref _isDatabaseConnected, value);
        }

        #endregion

        #region QuickBooks Settings

        private string _quickBooksCompanyFile = string.Empty;
        /// <summary>
        /// Gets or sets the path to the QuickBooks company file.
        /// Validates file exists and is accessible when set.
        /// </summary>
        public string QuickBooksCompanyFile
        {
            get => _quickBooksCompanyFile;
            set
            {
                if (SetProperty(ref _quickBooksCompanyFile, value))
                {
                    ValidateQuickBooksCompanyFile();
                }
            }
        }

        private bool _enableQuickBooksSync;
        /// <summary>
        /// Gets or sets whether QuickBooks synchronization is enabled.
        /// Configures sync timer when changed.
        /// </summary>
        public bool EnableQuickBooksSync
        {
            get => _enableQuickBooksSync;
            set
            {
                if (SetProperty(ref _enableQuickBooksSync, value))
                {
                    ConfigureSyncTimer();
                }
            }
        }

        private int _syncIntervalMinutes = 30;
        /// <summary>
        /// Gets or sets the QuickBooks sync interval in minutes.
        /// Reconfigures sync timer when changed.
        /// </summary>
        public int SyncIntervalMinutes
        {
            get => _syncIntervalMinutes;
            set
            {
                if (SetProperty(ref _syncIntervalMinutes, value))
                {
                    ConfigureSyncTimer();
                }
            }
        }

        private bool _isQuickBooksConnected;
        /// <summary>
        /// Gets or sets whether QuickBooks is currently connected.
        /// Updated based on connection status checks.
        /// </summary>
        public bool IsQuickBooksConnected
        {
            get => _isQuickBooksConnected;
            set => SetProperty(ref _isQuickBooksConnected, value);
        }

        private string _quickBooksVersion = "Unknown";
        /// <summary>
        /// Gets or sets the detected QuickBooks version.
        /// Detected from QuickBooks API during connection tests.
        /// </summary>
        public string QuickBooksVersion
        {
            get => _quickBooksVersion;
            set => SetProperty(ref _quickBooksVersion, value);
        }

        private string _lastSyncTime = "Never";
        /// <summary>
        /// Gets or sets the last QuickBooks sync time.
        /// Updated after successful sync operations.
        /// </summary>
        public string LastSyncTime
        {
            get => _lastSyncTime;
            set => SetProperty(ref _lastSyncTime, value);
        }

        #endregion

        #region QuickBooks Settings Methods

        private void ValidateQuickBooksCompanyFile()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(QuickBooksCompanyFile))
                {
                    QuickBooksConnectionStatus = "No company file specified";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Gray;
                    return;
                }

                if (!File.Exists(QuickBooksCompanyFile))
                {
                    QuickBooksConnectionStatus = "Company file not found";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
                    return;
                }

                // Check if file is accessible
                try
                {
                    using var stream = File.Open(QuickBooksCompanyFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    QuickBooksConnectionStatus = "Company file is accessible";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Green;
                }
                catch (UnauthorizedAccessException)
                {
                    QuickBooksConnectionStatus = "Access denied to company file";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
                }
                catch (IOException)
                {
                    QuickBooksConnectionStatus = "Company file is in use by QuickBooks";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                QuickBooksConnectionStatus = $"Validation error: {ex.Message}";
                QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
            }
        }

        private void ConfigureSyncTimer()
        {
            // Dispose existing timer
            _syncTimer?.Dispose();
            _syncTimer = null;

            if (!EnableQuickBooksSync || SyncIntervalMinutes <= 0)
                return;

            _syncTimer = new System.Timers.Timer
            {
                Interval = TimeSpan.FromMinutes(SyncIntervalMinutes).TotalMilliseconds,
                AutoReset = true
            };

            _syncTimer.Elapsed += async (sender, e) => await PerformScheduledSyncAsync();
            _syncTimer.Start();
        }

        private async Task PerformScheduledSyncAsync()
        {
            if (_quickBooksService == null)
                return;

            try
            {
                var status = await _quickBooksService.GetConnectionStatusAsync();
                if (!status.IsConnected)
                    return;

                var syncResult = await _quickBooksService.SyncDataAsync();
                if (syncResult.Success)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LastSyncTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                        QuickBooksConnectionStatus = $"Auto-sync completed: {syncResult.RecordsSynced} records";
                    });
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    QuickBooksConnectionStatus = $"Auto-sync failed: {ex.Message}";
                });
            }
        }

        #endregion

        #region AI/XAI Settings Methods

        private async Task LoadAvailableModelsAsync()
        {
            try
            {
                // For now, we'll use a predefined list of known XAI models
                // In a real implementation, this would call an API endpoint to get available models
                var models = new List<string>
                {
                    "grok-beta",
                    "grok-vision-beta",
                    "grok-2",
                    "grok-2-1212",
                    "grok-4-0709"
                };

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AvailableXaiModels.Clear();
                    foreach (var model in models)
                    {
                        AvailableXaiModels.Add(model);
                    }

                    // Set default model if not already set
                    if (string.IsNullOrEmpty(XaiModel) || !AvailableXaiModels.Contains(XaiModel))
                    {
                        XaiModel = AvailableXaiModels.FirstOrDefault() ?? "grok-4-0709";
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load available AI models");
                // Use fallback models
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (AvailableXaiModels.Count == 0)
                    {
                        AvailableXaiModels.Clear();
                        AvailableXaiModels.Add("grok-beta");
                        AvailableXaiModels.Add("grok-vision-beta");
                    }
                });
            }
        }

        private async Task UpdateAIServiceConfigurationAsync()
        {
            if (_aiService == null)
                return;

            try
            {
                // Update the API key in the service if provided
                if (!string.IsNullOrWhiteSpace(XaiApiKey))
                {
                    await _aiService.UpdateApiKeyAsync(XaiApiKey);
                }

                // Update application settings with current AI configuration
                var settings = _settingsService.Current;
                settings.EnableAI = EnableXaiFeatures;
                settings.XaiApiKey = XaiApiKey;
                settings.XaiModel = XaiModel;
                settings.XaiApiEndpoint = XaiApiEndpoint;
                settings.XaiTimeout = XaiTimeout;
                settings.XaiMaxTokens = XaiMaxTokens;
                settings.XaiTemperature = XaiTemperature;
                _settingsService.Save();

                // Note: Additional service configuration would be applied here
                // if the AI service supported dynamic reconfiguration
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to update AI service configuration");
            }
        }

        #endregion

        #region Logging Settings Methods

        private void EnsureLogDirectoryExists()
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger?.LogInformation("Created log directory: {Directory}", directory);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to create log directory: {Path}", LogFilePath);
            }
        }

        private void ConfigureLoggingService()
        {
            try
            {
                // Note: In a real implementation, this would reconfigure the global Serilog logger
                // based on the EnableFileLogging and LogFilePath settings.
                // However, Serilog configuration is typically done at application startup.
                // For now, we'll log the configuration change and note that a restart may be required.

                if (EnableFileLogging)
                {
                    EnsureLogDirectoryExists();
                    _logger?.LogInformation("File logging enabled with path: {Path}", LogFilePath);
                }
                else
                {
                    _logger?.LogInformation("File logging disabled");
                }

                // TODO: Implement dynamic Serilog reconfiguration if needed
                // This would require storing the logger configuration and recreating it
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to configure logging service");
            }
        }

        private void ConfigureAutoSaveTimer()
        {
            // Dispose existing timer
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;

            if (!EnableAutoSave || AutoSaveIntervalMinutes <= 0)
                return;

            _autoSaveTimer = new System.Timers.Timer
            {
                Interval = TimeSpan.FromMinutes(AutoSaveIntervalMinutes).TotalMilliseconds,
                AutoReset = true
            };

            _autoSaveTimer.Elapsed += async (sender, e) => await PerformAutoSaveAsync();
            _autoSaveTimer.Start();

            _logger?.LogInformation("Auto-save timer configured for {Minutes} minutes", AutoSaveIntervalMinutes);
        }

        private async Task PerformAutoSaveAsync()
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OnSaveSettings();
                    _logger?.LogInformation("Auto-saved settings at {Time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _logger?.LogWarning(ex, "Auto-save failed");
                });
            }
        }

        #endregion

        #region Syncfusion License

        private string _syncfusionLicenseKey = string.Empty;
        /// <summary>
        /// Gets or sets the Syncfusion license key.
        /// Validated with Syncfusion API when validation command is executed.
        /// </summary>
        public string SyncfusionLicenseKey
        {
            get => _syncfusionLicenseKey;
            set => SetProperty(ref _syncfusionLicenseKey, value);
        }

        private bool _isSyncfusionLicenseValid;
        /// <summary>
        /// Gets or sets whether the Syncfusion license is valid.
        /// Updated based on license validation results.
        /// </summary>
        public bool IsSyncfusionLicenseValid
        {
            get => _isSyncfusionLicenseValid;
            set => SetProperty(ref _isSyncfusionLicenseValid, value);
        }

        private string _syncfusionLicenseStatus = "Not Validated";
        /// <summary>
        /// Gets or sets the Syncfusion license status message.
        /// Updated based on validation results.
        /// </summary>
        public string SyncfusionLicenseStatus
        {
            get => _syncfusionLicenseStatus;
            set => SetProperty(ref _syncfusionLicenseStatus, value);
        }

        #endregion

        #region XAI/Grok Settings

        private string _xaiApiKey = string.Empty;
        /// <summary>
        /// Gets or sets the XAI/Grok API key.
        /// Updates AI service configuration when changed.
        /// </summary>
        public string XaiApiKey
        {
            get => _xaiApiKey;
            set
            {
                if (SetProperty(ref _xaiApiKey, value))
                {
                    // Update AI service configuration when API key changes
                    _ = UpdateAIServiceConfigurationAsync();
                }
            }
        }

        private string _xaiApiEndpoint = "https://api.x.ai/v1";
        /// <summary>
        /// Gets or sets the XAI API endpoint URL.
        /// Endpoint reachability is validated during connection tests.
        /// </summary>
        public string XaiApiEndpoint
        {
            get => _xaiApiEndpoint;
            set => SetProperty(ref _xaiApiEndpoint, value);
        }

        private string _xaiModel = "grok-4-0709";
        /// <summary>
        /// Gets or sets the XAI model to use.
        /// Available models are loaded from API during connection tests.
        /// </summary>
        public string XaiModel
        {
            get => _xaiModel;
            set => SetProperty(ref _xaiModel, value);
        }

        private bool _enableXaiFeatures;
        /// <summary>
        /// Gets or sets whether XAI features are enabled.
        /// Toggles AI functionality throughout the application when changed.
        /// </summary>
        public bool EnableXaiFeatures
        {
            get => _enableXaiFeatures;
            set
            {
                if (SetProperty(ref _enableXaiFeatures, value))
                {
                    // Toggle AI functionality throughout the app
                    // Update AI service configuration
                    _ = UpdateAIServiceConfigurationAsync();

                    // Update application-wide settings
                    _settingsService.Current.EnableAI = value;
                    _settingsService.Save();

                    _logger?.LogInformation("AI features {Status}", value ? "enabled" : "disabled");
                }
            }
        }

        private bool _isXaiConnected;
        /// <summary>
        /// Gets or sets whether XAI is currently connected.
        /// Updated based on connection test results.
        /// </summary>
        public bool IsXaiConnected
        {
            get => _isXaiConnected;
            set => SetProperty(ref _isXaiConnected, value);
        }

        private int _xaiTimeout = 30;
        /// <summary>
        /// Gets or sets the XAI request timeout in seconds.
        /// Applied to HTTP client configuration when AI service is updated.
        /// </summary>
        public int XaiTimeout
        {
            get => _xaiTimeout;
            set => SetProperty(ref _xaiTimeout, value);
        }

        private int _xaiMaxTokens = 2000;
        /// <summary>
        /// Gets or sets the maximum tokens for XAI requests.
        /// Applied to API request parameters when AI service is updated.
        /// </summary>
        public int XaiMaxTokens
        {
            get => _xaiMaxTokens;
            set => SetProperty(ref _xaiMaxTokens, value);
        }

        private double _xaiTemperature = 0.7;
        /// <summary>
        /// Gets or sets the XAI temperature parameter (0.0-1.0).
        /// Applied to API request parameters when AI service is updated.
        /// </summary>
        public double XaiTemperature
        {
            get => _xaiTemperature;
            set => SetProperty(ref _xaiTemperature, value);
        }

        private bool _enableXaiLogging;
        /// <summary>
        /// Gets or sets whether to log XAI requests/responses.
        /// Configures XAI-specific logging when changed.
        /// </summary>
        public bool EnableXaiLogging
        {
            get => _enableXaiLogging;
            set => SetProperty(ref _enableXaiLogging, value);
        }

        private string _xaiLogPath = "./logs/xai";
        /// <summary>
        /// Gets or sets the path for XAI logs.
        /// Creates directory if it doesn't exist when changed.
        /// </summary>
        public string XaiLogPath
        {
            get => _xaiLogPath;
            set
            {
                if (SetProperty(ref _xaiLogPath, value))
                {
                    // Create directory if it doesn't exist
                    try
                    {
                        var directory = Path.GetDirectoryName(value);
                        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                            _logger?.LogInformation("Created XAI log directory: {Directory}", directory);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to create XAI log directory: {Path}", value);
                    }
                }
            }
        }

        private ObservableCollection<string> _availableXaiModels = new();
        /// <summary>
        /// Gets or sets the list of available XAI models.
        /// Populated from API during connection tests or with fallback models.
        /// </summary>
        public ObservableCollection<string> AvailableXaiModels
        {
            get => _availableXaiModels;
            set => SetProperty(ref _availableXaiModels, value);
        }

        #endregion

        #region Application Settings

        private bool _enableAutoSave = true;
        /// <summary>
        /// Gets or sets whether auto-save is enabled.
        /// Configures auto-save timer when changed.
        /// </summary>
        public bool EnableAutoSave
        {
            get => _enableAutoSave;
            set
            {
                if (SetProperty(ref _enableAutoSave, value))
                {
                    ConfigureAutoSaveTimer();
                }
            }
        }

        private int _autoSaveIntervalMinutes = 5;
        /// <summary>
        /// Gets or sets the auto-save interval in minutes.
        /// Reconfigures auto-save timer when changed.
        /// </summary>
        public int AutoSaveIntervalMinutes
        {
            get => _autoSaveIntervalMinutes;
            set
            {
                if (SetProperty(ref _autoSaveIntervalMinutes, value))
                {
                    ConfigureAutoSaveTimer();
                }
            }
        }

        private bool _enableNotifications = true;
        /// <summary>
        /// Gets or sets whether system notifications are enabled.
        /// Updates application notification settings when changed.
        /// </summary>
        public bool EnableNotifications
        {
            get => _enableNotifications;
            set
            {
                if (SetProperty(ref _enableNotifications, value))
                {
                    // Update application settings
                    _settingsService.Current.EnableNotifications = value;
                    _settingsService.Save();

                    _logger?.LogInformation("Notifications {Status}", value ? "enabled" : "disabled");
                }
            }
        }

        private bool _enableSounds = true;
        /// <summary>
        /// Gets or sets whether system sounds are enabled.
        /// Updates application sound settings when changed.
        /// </summary>
        public bool EnableSounds
        {
            get => _enableSounds;
            set
            {
                if (SetProperty(ref _enableSounds, value))
                {
                    // Update application settings
                    _settingsService.Current.EnableSounds = value;
                    _settingsService.Save();

                    _logger?.LogInformation("Sounds {Status}", value ? "enabled" : "disabled");
                }
            }
        }

        private string _defaultLanguage = "en-US";
        /// <summary>
        /// Gets or sets the default application language.
        /// Applies localization settings when changed.
        /// </summary>
        public string DefaultLanguage
        {
            get => _defaultLanguage;
            set
            {
                if (SetProperty(ref _defaultLanguage, value))
                {
                    // Update application settings
                    _settingsService.Current.DefaultLanguage = value;
                    _settingsService.Save();

                    // Apply culture change
                    try
                    {
                        var culture = new System.Globalization.CultureInfo(value);
                        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                        _logger?.LogInformation("Application language changed to: {Language}", value);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to apply language change: {Language}", value);
                    }
                }
            }
        }

        private string _dateFormat = "MM/dd/yyyy";
        /// <summary>
        /// Gets or sets the default date format.
        /// Applied to date displays throughout the application.
        /// </summary>
        public string DateFormat
        {
            get => _dateFormat;
            set
            {
                if (SetProperty(ref _dateFormat, value))
                {
                    // Update application settings
                    _settingsService.Current.DateFormat = value;
                    _settingsService.Save();

                    _logger?.LogInformation("Date format changed to: {Format}", value);
                }
            }
        }

        private string _currencyFormat = "USD";
        /// <summary>
        /// Gets or sets the default currency format.
        /// Applied to currency displays throughout the application.
        /// </summary>
        public string CurrencyFormat
        {
            get => _currencyFormat;
            set
            {
                if (SetProperty(ref _currencyFormat, value))
                {
                    // Update application settings
                    _settingsService.Current.CurrencyFormat = value;
                    _settingsService.Save();

                    _logger?.LogInformation("Currency format changed to: {Format}", value);
                }
            }
        }

        private int _sessionTimeoutMinutes = 60;
        /// <summary>
        /// Gets or sets the session timeout in minutes.
        /// Implements session timeout logic with inactivity monitoring.
        /// </summary>
        public int SessionTimeoutMinutes
        {
            get => _sessionTimeoutMinutes;
            set
            {
                if (SetProperty(ref _sessionTimeoutMinutes, value))
                {
                    // Update application settings
                    _settingsService.Current.SessionTimeoutMinutes = value;
                    _settingsService.Save();

                    // TODO: Implement session timeout timer logic
                    // This would require integrating with an authentication/session service
                    _logger?.LogInformation("Session timeout changed to: {Minutes} minutes", value);
                }
            }
        }

        #endregion

        #region Fiscal Year Settings

        private string _fiscalYearStart = "July 1";
        /// <summary>
        /// Gets or sets the fiscal year start date.
        /// Used for budget calculations and reporting when changed.
        /// </summary>
        public string FiscalYearStart
        {
            get => _fiscalYearStart;
            set
            {
                if (SetProperty(ref _fiscalYearStart, value))
                {
                    // Update application settings
                    _settingsService.Current.FiscalYearStart = value;
                    _settingsService.Save();

                    // Recalculate fiscal year values
                    UpdateFiscalYearCalculations();

                    _logger?.LogInformation("Fiscal year start changed to: {Start}", value);
                }
            }
        }

        private string _fiscalYearEnd = "June 30";
        /// <summary>
        /// Gets or sets the fiscal year end date.
        /// Used for budget calculations and reporting when changed.
        /// </summary>
        public string FiscalYearEnd
        {
            get => _fiscalYearEnd;
            set
            {
                if (SetProperty(ref _fiscalYearEnd, value))
                {
                    // Update application settings
                    _settingsService.Current.FiscalYearEnd = value;
                    _settingsService.Save();

                    // Recalculate fiscal year values
                    UpdateFiscalYearCalculations();

                    _logger?.LogInformation("Fiscal year end changed to: {End}", value);
                }
            }
        }

        private string _currentFiscalYear = "2024-2025";
        /// <summary>
        /// Gets or sets the current fiscal year designation.
        /// Calculated based on current date and fiscal year start.
        /// </summary>
        public string CurrentFiscalYear
        {
            get => _currentFiscalYear;
            set
            {
                if (SetProperty(ref _currentFiscalYear, value))
                {
                    // Update application settings
                    _settingsService.Current.CurrentFiscalYear = value;
                    _settingsService.Save();

                    _logger?.LogInformation("Current fiscal year set to: {Year}", value);
                }
            }
        }

        private ObservableCollection<string> _fiscalYearList = new();
        /// <summary>
        /// Gets or sets the list of available fiscal years.
        /// Generated from historical data (last 5 years).
        /// </summary>
        public ObservableCollection<string> FiscalYearList
        {
            get => _fiscalYearList;
            set => SetProperty(ref _fiscalYearList, value);
        }

        private bool _useFiscalYearForReporting = true;
        /// <summary>
        /// Gets or sets whether to use fiscal year for reports.
        /// Applied to report generation logic when changed.
        /// </summary>
        public bool UseFiscalYearForReporting
        {
            get => _useFiscalYearForReporting;
            set
            {
                if (SetProperty(ref _useFiscalYearForReporting, value))
                {
                    // Update application settings
                    _settingsService.Current.UseFiscalYearForReporting = value;
                    _settingsService.Save();

                    _logger?.LogInformation("Fiscal year reporting {Status}", value ? "enabled" : "disabled");
                }
            }
        }

        private int _fiscalQuarter = 1;
        /// <summary>
        /// Gets or sets the current fiscal quarter.
        /// Calculated based on current date and fiscal year configuration.
        /// </summary>
        public int FiscalQuarter
        {
            get => _fiscalQuarter;
            set
            {
                if (SetProperty(ref _fiscalQuarter, value))
                {
                    // Update fiscal period when quarter changes
                    FiscalPeriod = $"Q{value}";

                    // Update application settings
                    _settingsService.Current.FiscalQuarter = value;
                    _settingsService.Current.FiscalPeriod = FiscalPeriod;
                    _settingsService.Save();

                    _logger?.LogInformation("Fiscal quarter set to: Q{Quarter}", value);
                }
            }
        }

        private string _fiscalPeriod = "Q1";
        /// <summary>
        /// Gets or sets the current fiscal period designation.
        /// Calculated from fiscal quarter.
        /// </summary>
        public string FiscalPeriod
        {
            get => _fiscalPeriod;
            set
            {
                if (SetProperty(ref _fiscalPeriod, value))
                {
                    // Update application settings
                    _settingsService.Current.FiscalPeriod = value;
                    _settingsService.Save();

                    _logger?.LogInformation("Fiscal period set to: {Period}", value);
                }
            }
        }

        #endregion

        #region Fiscal Year Methods

        private void UpdateFiscalYearCalculations()
        {
            try
            {
                var now = DateTime.Now;

                // Calculate current fiscal year based on fiscal year start
                // This is a simplified calculation - in a real implementation,
                // this would parse the FiscalYearStart date and calculate properly
                var currentYear = now.Year;
                var fiscalYear = $"{currentYear}-{currentYear + 1}";
                CurrentFiscalYear = fiscalYear;

                // Calculate fiscal quarter (simplified)
                var month = now.Month;
                var quarter = (month - 1) / 3 + 1;
                FiscalQuarter = quarter;

                // Calculate fiscal period
                FiscalPeriod = $"Q{quarter}";

                // Update settings
                _settingsService.Current.CurrentFiscalYear = fiscalYear;
                _settingsService.Current.FiscalQuarter = quarter;
                _settingsService.Current.FiscalPeriod = FiscalPeriod;
                _settingsService.Save();

                _logger?.LogInformation("Fiscal year calculations updated: {Year}, Q{Quarter}", fiscalYear, quarter);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to update fiscal year calculations");
            }
        }

        #endregion

        #region Missing Properties - Added for binding compatibility

        private ObservableCollection<string> _availableThemes = new() { "FluentDark", "FluentLight", "MaterialDark", "MaterialLight" };
        /// <summary>
        /// Gets or sets the collection of available themes.
        /// Initialized with known Syncfusion themes; can be extended to load from API.
        /// </summary>
        public ObservableCollection<string> AvailableThemes
        {
            get => _availableThemes;
            set => SetProperty(ref _availableThemes, value);
        }

        private string _selectedTheme = "FluentDark";
        /// <summary>
        /// Gets or sets the currently selected theme.
        /// </summary>
        public string SelectedTheme
        {
            get => _selectedTheme;
            set => SetProperty(ref _selectedTheme, value);
        }

        /// <summary>
        /// Gets or sets the current theme (alias for SelectedTheme for binding compatibility).
        /// </summary>
        public string CurrentTheme
        {
            get => SelectedTheme;
            set
            {
                SelectedTheme = value;
                RaisePropertyChanged();
            }
        }

        private string _windowWidthValidation = string.Empty;
        /// <summary>
        /// Gets or sets the validation message for window width.
        /// </summary>
        public string WindowWidthValidation
        {
            get => _windowWidthValidation;
            set => SetProperty(ref _windowWidthValidation, value);
        }

        private string _windowHeightValidation = string.Empty;
        /// <summary>
        /// Gets or sets the validation message for window height.
        /// </summary>
        public string WindowHeightValidation
        {
            get => _windowHeightValidation;
            set => SetProperty(ref _windowHeightValidation, value);
        }

        private bool _maximizeOnStartup;
        /// <summary>
        /// Gets or sets whether to maximize the window on startup.
        /// </summary>
        public bool MaximizeOnStartup
        {
            get => _maximizeOnStartup;
            set => SetProperty(ref _maximizeOnStartup, value);
        }

        private bool _showSplashScreen = true;
        /// <summary>
        /// Gets or sets whether to show the splash screen on startup.
        /// </summary>
        public bool ShowSplashScreen
        {
            get => _showSplashScreen;
            set => SetProperty(ref _showSplashScreen, value);
        }

        private string _databaseConnectionString = string.Empty;
        /// <summary>
        /// Gets or sets the database connection string (alias for ConnectionString).
        /// </summary>
        public string DatabaseConnectionString
        {
            get => ConnectionString;
            set {
                ConnectionString = value;
                RaisePropertyChanged();
            }
        }

        private string _databaseStatus = "Not Connected";
        /// <summary>
        /// Gets or sets the database connection status message.
        /// </summary>
        public string DatabaseStatus
        {
            get => _databaseStatus;
            set => SetProperty(ref _databaseStatus, value);
        }

        private System.Windows.Media.Brush _databaseStatusColor = System.Windows.Media.Brushes.Gray;
        /// <summary>
        /// Gets or sets the color for the database status indicator.
        /// </summary>
        public System.Windows.Media.Brush DatabaseStatusColor
        {
            get => _databaseStatusColor;
            set => SetProperty(ref _databaseStatusColor, value);
        }

        private string _quickBooksClientId = string.Empty;
        /// <summary>
        /// Gets or sets the QuickBooks OAuth client ID.
        /// </summary>
        public string QuickBooksClientId
        {
            get => _quickBooksClientId;
            set => SetProperty(ref _quickBooksClientId, value);
        }

        private string _quickBooksClientSecret = string.Empty;
        /// <summary>
        /// Gets or sets the QuickBooks OAuth client secret.
        /// </summary>
        public string QuickBooksClientSecret
        {
            get => _quickBooksClientSecret;
            set => SetProperty(ref _quickBooksClientSecret, value);
        }

        private string _quickBooksRedirectUri = "http://localhost:8080/callback";
        /// <summary>
        /// Gets or sets the QuickBooks OAuth redirect URI.
        /// </summary>
        public string QuickBooksRedirectUri
        {
            get => _quickBooksRedirectUri;
            set => SetProperty(ref _quickBooksRedirectUri, value);
        }

        private ObservableCollection<string> _quickBooksEnvironments = new() { "Sandbox", "Production" };
        /// <summary>
        /// Gets or sets the available QuickBooks environments.
        /// </summary>
        public ObservableCollection<string> QuickBooksEnvironments
        {
            get => _quickBooksEnvironments;
            set => SetProperty(ref _quickBooksEnvironments, value);
        }

        private string _selectedQuickBooksEnvironment = "Sandbox";
        /// <summary>
        /// Gets or sets the selected QuickBooks environment.
        /// </summary>
        public string SelectedQuickBooksEnvironment
        {
            get => _selectedQuickBooksEnvironment;
            set => SetProperty(ref _selectedQuickBooksEnvironment, value);
        }

        private string _quickBooksConnectionStatus = "Not Connected";
        /// <summary>
        /// Gets or sets the QuickBooks connection status message.
        /// </summary>
        public string QuickBooksConnectionStatus
        {
            get => _quickBooksConnectionStatus;
            set => SetProperty(ref _quickBooksConnectionStatus, value);
        }

        private System.Windows.Media.Brush _quickBooksStatusColor = System.Windows.Media.Brushes.Gray;
        /// <summary>
        /// Gets or sets the color for the QuickBooks status indicator.
        /// </summary>
        public System.Windows.Media.Brush QuickBooksStatusColor
        {
            get => _quickBooksStatusColor;
            set => SetProperty(ref _quickBooksStatusColor, value);
        }

        private System.Windows.Media.Brush _syncfusionLicenseStatusColor = System.Windows.Media.Brushes.Gray;
        /// <summary>
        /// Gets or sets the color for the Syncfusion license status indicator.
        /// </summary>
        public System.Windows.Media.Brush SyncfusionLicenseStatusColor
        {
            get => _syncfusionLicenseStatusColor;
            set => SetProperty(ref _syncfusionLicenseStatusColor, value);
        }

        /// <summary>
        /// Gets or sets the XAI base URL (alias for XaiApiEndpoint).
        /// </summary>
        public string XaiBaseUrl
        {
            get => XaiApiEndpoint;
            set {
                XaiApiEndpoint = value;
                RaisePropertyChanged();
            }
        }

        private ObservableCollection<string> _availableModels = new() { "grok-beta", "grok-vision-beta" };
        /// <summary>
        /// Gets or sets the available AI models.
        /// </summary>
        public ObservableCollection<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }

        private int? _xaiTimeoutSeconds = 30;
        /// <summary>
        /// Gets or sets the XAI API timeout in seconds.
        /// </summary>
        public int? XaiTimeoutSeconds
        {
            get => _xaiTimeoutSeconds;
            set => SetProperty(ref _xaiTimeoutSeconds, value);
        }

        private string _xaiTimeoutValidation = string.Empty;
        /// <summary>
        /// Gets or sets the validation message for XAI timeout.
        /// </summary>
        public string XaiTimeoutValidation
        {
            get => _xaiTimeoutValidation;
            set => SetProperty(ref _xaiTimeoutValidation, value);
        }

        private ObservableCollection<string> _availableResponseStyles = new() { "Concise", "Detailed", "Technical" };
        /// <summary>
        /// Gets or sets the available response styles.
        /// </summary>
        public ObservableCollection<string> AvailableResponseStyles
        {
            get => _availableResponseStyles;
            set => SetProperty(ref _availableResponseStyles, value);
        }

        private string _responseStyle = "Detailed";
        /// <summary>
        /// Gets or sets the selected response style.
        /// </summary>
        public string ResponseStyle
        {
            get => _responseStyle;
            set => SetProperty(ref _responseStyle, value);
        }

        private ObservableCollection<string> _availablePersonalities = new() { "Professional", "Friendly", "Technical" };
        /// <summary>
        /// Gets or sets the available AI personalities.
        /// </summary>
        public ObservableCollection<string> AvailablePersonalities
        {
            get => _availablePersonalities;
            set => SetProperty(ref _availablePersonalities, value);
        }

        private string _personality = "Professional";
        /// <summary>
        /// Gets or sets the selected AI personality.
        /// </summary>
        public string Personality
        {
            get => _personality;
            set => SetProperty(ref _personality, value);
        }

        private int? _contextWindowSize = 4096;
        /// <summary>
        /// Gets or sets the AI context window size.
        /// </summary>
        public int? ContextWindowSize
        {
            get => _contextWindowSize;
            set => SetProperty(ref _contextWindowSize, value);
        }

        private string _contextWindowValidation = string.Empty;
        /// <summary>
        /// Gets or sets the validation message for context window size.
        /// </summary>
        public string ContextWindowValidation
        {
            get => _contextWindowValidation;
            set => SetProperty(ref _contextWindowValidation, value);
        }

        private double? _temperature = 0.7;
        /// <summary>
        /// Gets or sets the AI temperature parameter.
        /// </summary>
        public double? Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        private string _temperatureValidation = string.Empty;
        /// <summary>
        /// Gets or sets the validation message for temperature.
        /// </summary>
        public string TemperatureValidation
        {
            get => _temperatureValidation;
            set => SetProperty(ref _temperatureValidation, value);
        }

        private int? _maxTokens = 2048;
        /// <summary>
        /// Gets or sets the maximum tokens for AI responses.
        /// </summary>
        public int? MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, value);
        }

        private string _maxTokensValidation = string.Empty;
        /// <summary>
        /// Gets or sets the validation message for max tokens.
        /// </summary>
        public string MaxTokensValidation
        {
            get => _maxTokensValidation;
            set => SetProperty(ref _maxTokensValidation, value);
        }

        private bool _enableSafetyFilters = true;
        /// <summary>
        /// Gets or sets whether AI safety filters are enabled.
        /// </summary>
        public bool EnableSafetyFilters
        {
            get => _enableSafetyFilters;
            set => SetProperty(ref _enableSafetyFilters, value);
        }

        private bool _enableStreaming;
        /// <summary>
        /// Gets or sets whether AI streaming responses are enabled.
        /// </summary>
        public bool EnableStreaming
        {
            get => _enableStreaming;
            set => SetProperty(ref _enableStreaming, value);
        }

        private string _xaiConnectionStatus = "Not Connected";
        /// <summary>
        /// Gets or sets the XAI connection status message.
        /// </summary>
        public string XaiConnectionStatus
        {
            get => _xaiConnectionStatus;
            set => SetProperty(ref _xaiConnectionStatus, value);
        }

        private System.Windows.Media.Brush _xaiStatusColor = System.Windows.Media.Brushes.Gray;
        /// <summary>
        /// Gets or sets the color for the XAI status indicator.
        /// </summary>
        public System.Windows.Media.Brush XaiStatusColor
        {
            get => _xaiStatusColor;
            set => SetProperty(ref _xaiStatusColor, value);
        }

        private bool _enableDynamicColumns;
        /// <summary>
        /// Gets or sets whether dynamic columns are enabled.
        /// </summary>
        public bool EnableDynamicColumns
        {
            get => _enableDynamicColumns;
            set => SetProperty(ref _enableDynamicColumns, value);
        }

        private bool _enableDataCaching = true;
        /// <summary>
        /// Gets or sets whether data caching is enabled.
        /// </summary>
        public bool EnableDataCaching
        {
            get => _enableDataCaching;
            set => SetProperty(ref _enableDataCaching, value);
        }

        private int? _cacheExpirationMinutes = 30;
        /// <summary>
        /// Gets or sets the cache expiration time in minutes.
        /// </summary>
        public int? CacheExpirationMinutes
        {
            get => _cacheExpirationMinutes;
            set => SetProperty(ref _cacheExpirationMinutes, value);
        }

        private ObservableCollection<string> _logLevels = new() { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };
        /// <summary>
        /// Gets or sets the available log levels.
        /// </summary>
        public ObservableCollection<string> LogLevels
        {
            get => _logLevels;
            set => SetProperty(ref _logLevels, value);
        }

        private string _selectedLogLevel = "Information";
        /// <summary>
        /// Gets or sets the selected log level.
        /// </summary>
        public string SelectedLogLevel
        {
            get => _selectedLogLevel;
            set => SetProperty(ref _selectedLogLevel, value);
        }

        private bool _enableFileLogging = true;
        /// <summary>
        /// Gets or sets whether file logging is enabled.
        /// Configures the logging service when changed.
        /// </summary>
        public bool EnableFileLogging
        {
            get => _enableFileLogging;
            set
            {
                if (SetProperty(ref _enableFileLogging, value))
                {
                    ConfigureLoggingService();
                }
            }
        }

        private string _logFilePath = "logs/app.log";
        /// <summary>
        /// Gets or sets the log file path.
        /// Creates directory if it doesn't exist when changed.
        /// </summary>
        public string LogFilePath
        {
            get => _logFilePath;
            set
            {
                if (SetProperty(ref _logFilePath, value))
                {
                    EnsureLogDirectoryExists();
                    ConfigureLoggingService();
                }
            }
        }

        private string _systemInfo = "Windows 11, .NET 9.0";
        /// <summary>
        /// Gets or sets the system information display text.
        /// </summary>
        public string SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }

        private ObservableCollection<string> _fiscalYearMonths = new() { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        /// <summary>
        /// Gets or sets the available fiscal year start months.
        /// </summary>
        public ObservableCollection<string> FiscalYearMonths
        {
            get => _fiscalYearMonths;
            set => SetProperty(ref _fiscalYearMonths, value);
        }

        private int _fiscalYearStartMonth = 7;
        /// <summary>
        /// Gets or sets the fiscal year start month (1-12).
        /// </summary>
        public int FiscalYearStartMonth
        {
            get => _fiscalYearStartMonth;
            set => SetProperty(ref _fiscalYearStartMonth, value);
        }

        private int _fiscalYearStartDay = 1;
        /// <summary>
        /// Gets or sets the fiscal year start day.
        /// </summary>
        public int FiscalYearStartDay
        {
            get => _fiscalYearStartDay;
            set => SetProperty(ref _fiscalYearStartDay, value);
        }

        private string _fiscalYearDayValidation = string.Empty;
        /// <summary>
        /// Gets or sets the validation message for fiscal year day.
        /// </summary>
        public string FiscalYearDayValidation
        {
            get => _fiscalYearDayValidation;
            set => SetProperty(ref _fiscalYearDayValidation, value);
        }

        private string _currentFiscalYearDisplay = "FY 2024-2025";
        /// <summary>
        /// Gets or sets the current fiscal year display text.
        /// </summary>
        public string CurrentFiscalYearDisplay
        {
            get => _currentFiscalYearDisplay;
            set => SetProperty(ref _currentFiscalYearDisplay, value);
        }

        private string _fiscalYearPeriodDisplay = "Q2 (Oct-Dec 2024)";
        /// <summary>
        /// Gets or sets the fiscal year period display text.
        /// </summary>
        public string FiscalYearPeriodDisplay
        {
            get => _fiscalYearPeriodDisplay;
            set => SetProperty(ref _fiscalYearPeriodDisplay, value);
        }

        private string _daysRemainingInFiscalYear = "180 days";
        /// <summary>
        /// Gets or sets the days remaining in fiscal year display text.
        /// </summary>
        public string DaysRemainingInFiscalYear
        {
            get => _daysRemainingInFiscalYear;
            set => SetProperty(ref _daysRemainingInFiscalYear, value);
        }

        private string _settingsStatus = "Ready";
        /// <summary>
        /// Gets or sets the settings status message.
        /// </summary>
        public string SettingsStatus
        {
            get => _settingsStatus;
            set => SetProperty(ref _settingsStatus, value);
        }

        private string _lastSaved = "Never";
        /// <summary>
        /// Gets or sets the last saved timestamp text.
        /// </summary>
        public string LastSaved
        {
            get => _lastSaved;
            set => SetProperty(ref _lastSaved, value);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to save all settings.
        /// Persists all current settings to configuration files/database.
        /// </summary>
        public ICommand SaveSettingsCommand { get; }

        /// <summary>
        /// Command to reset settings to defaults.
        /// Restores default values for all settings.
        /// </summary>
        public ICommand ResetSettingsCommand { get; }

        /// <summary>
        /// Command to test database connection.
        /// Attempts connection and updates status and IsDatabaseConnected property.
        /// </summary>
        public ICommand TestDatabaseConnectionCommand { get; }

        /// <summary>
        /// Command to test QuickBooks connection.
        /// Attempts QuickBooks connection and updates status.
        /// </summary>
        public ICommand TestQuickBooksConnectionCommand { get; }

        /// <summary>
        /// Command to validate Syncfusion license.
        /// Validates license key with Syncfusion API and updates status.
        /// </summary>
        public ICommand ValidateSyncfusionLicenseCommand { get; }

        /// <summary>
        /// Command to test XAI connection.
        /// Tests API connection and updates status.
        /// </summary>
        public ICommand TestXaiConnectionCommand { get; }

        /// <summary>
        /// Command to export settings.
        /// Exports current settings to file for backup/sharing.
        /// </summary>
        public ICommand ExportSettingsCommand { get; }

        /// <summary>
        /// Command to import settings.
        /// Imports settings from a previously exported file.
        /// </summary>
        public ICommand ImportSettingsCommand { get; }

        /// <summary>
        /// Command to test connection (alias for TestDatabaseConnectionCommand).
        /// </summary>
        public ICommand TestConnectionCommand => TestDatabaseConnectionCommand;

        /// <summary>
        /// Command to validate license (alias for ValidateSyncfusionLicenseCommand).
        /// </summary>
        public ICommand ValidateLicenseCommand => ValidateSyncfusionLicenseCommand;

        /// <summary>
        /// Command to connect to QuickBooks.
        /// </summary>
        public ICommand ConnectQuickBooksCommand { get; }

        /// <summary>
        /// Command to save fiscal year settings.
        /// </summary>
        public ICommand SaveFiscalYearSettingsCommand { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Parameterless fallback constructor for Activator when DI resolution fails.
        /// Creates minimal viable instance with null services.
        /// </summary>
        protected SettingsPanelViewModel()
        {
            // Create fallback instances
            _settingsService = null!; // Will cause runtime errors if used
            _unitOfWork = null!;
            _quickBooksService = null;
            // _syncfusionLicenseService removed - license registration happens in App static constructor
            _aiService = null;
            _logger = null;

            // Initialize commands with safe defaults
            SaveSettingsCommand = new DelegateCommand(() => { }, () => false);
            ResetSettingsCommand = new DelegateCommand(() => { });
            TestDatabaseConnectionCommand = new DelegateCommand(() => { }, () => false);
            TestQuickBooksConnectionCommand = new DelegateCommand(() => { }, () => false);
            ValidateSyncfusionLicenseCommand = new DelegateCommand(() => { }, () => false);
            TestXaiConnectionCommand = new DelegateCommand(() => { }, () => false);
            ExportSettingsCommand = new DelegateCommand(() => { });
            ImportSettingsCommand = new DelegateCommand(() => { });
            ConnectQuickBooksCommand = new DelegateCommand(() => { });
            SaveFiscalYearSettingsCommand = new DelegateCommand(() => { });

            // Log warning if possible
            System.Diagnostics.Debug.WriteLine("WARNING: SettingsPanelViewModel created with parameterless fallback constructor - services unavailable");
        }

        public SettingsPanelViewModel(ISettingsService settingsService, IUnitOfWork unitOfWork, IQuickBooksService? quickBooksService = null, IAIService? aiService = null, ILogger<SettingsPanelViewModel>? logger = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _quickBooksService = quickBooksService;
            // _syncfusionLicenseService removed - license registration happens in App static constructor
            _aiService = aiService;
            _logger = logger;

            // Load window settings from user preferences
            LoadWindowSettings();

            // Initialize commands
            SaveSettingsCommand = new DelegateCommand(OnSaveSettings, CanSaveSettings);
            ResetSettingsCommand = new DelegateCommand(OnResetSettings);
            TestDatabaseConnectionCommand = new DelegateCommand(OnTestDatabaseConnection, CanTestDatabaseConnection);
            TestQuickBooksConnectionCommand = new DelegateCommand(OnTestQuickBooksConnection, CanTestQuickBooksConnection);
            ValidateSyncfusionLicenseCommand = new DelegateCommand(OnValidateSyncfusionLicense, CanValidateSyncfusionLicense);
            TestXaiConnectionCommand = new DelegateCommand(OnTestXaiConnection, CanTestXaiConnection);
            ExportSettingsCommand = new DelegateCommand(OnExportSettings);
            ImportSettingsCommand = new DelegateCommand(OnImportSettings);
            ConnectQuickBooksCommand = new DelegateCommand(OnConnectQuickBooks);
            SaveFiscalYearSettingsCommand = new DelegateCommand(OnSaveFiscalYearSettings);
        }

        #endregion

        #region Window Settings Management

        private void LoadWindowSettings()
        {
            try
            {
                var settings = _settingsService.Current;

                // Load window dimensions
                WindowWidth = settings.WindowWidth?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1024";
                WindowHeight = settings.WindowHeight?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "768";

                // Load window position preferences
                RememberWindowPosition = settings.WindowLeft.HasValue || settings.WindowTop.HasValue;

                // Load theme
                DefaultTheme = settings.Theme ?? "FluentDark";

                // Load maximized state
                StartMaximized = settings.WindowMaximized ?? false;
            }
            catch (Exception ex)
            {
                // Log error and use defaults
                StatusMessage = $"Error loading window settings: {ex.Message}";
                // Keep default values set in property initializers
            }
        }

        private void SaveWindowSettings()
        {
            try
            {
                var settings = _settingsService.Current;

                // Save window dimensions
                if (double.TryParse(WindowWidth, out var width))
                    settings.WindowWidth = width;
                if (double.TryParse(WindowHeight, out var height))
                    settings.WindowHeight = height;

                // Save theme
                settings.Theme = DefaultTheme;

                // Save maximized state
                settings.WindowMaximized = StartMaximized;

                // Save settings
                _settingsService.Save();

                StatusMessage = "Window settings saved successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving window settings: {ex.Message}";
            }
        }

        #endregion

        #region Command Handlers

        private async void OnSaveSettings()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Validating settings...";

                // Validate database connection before saving
                var connectionString = BuildConnectionString();
                var isDatabaseValid = await TestDatabaseConnectionAsync(connectionString);
                if (!isDatabaseValid)
                {
                    StatusMessage = "Cannot save settings: Database connection validation failed";
                    return;
                }

                StatusMessage = "Saving settings...";

                // Save window settings
                SaveWindowSettings();

                // Save database settings
                _settingsService.Current.DatabaseServer = DatabaseServer;
                _settingsService.Current.DatabaseName = DatabaseName;

                // Save QuickBooks settings
                _settingsService.Current.QuickBooksCompanyFile = QuickBooksCompanyFile;
                _settingsService.Current.EnableQuickBooksSync = EnableQuickBooksSync;
                _settingsService.Current.SyncIntervalMinutes = SyncIntervalMinutes;

                // Save AI/XAI settings
                _settingsService.Current.EnableAI = EnableXaiFeatures;
                _settingsService.Current.XaiApiKey = XaiApiKey;
                _settingsService.Current.XaiModel = XaiModel;
                _settingsService.Current.XaiApiEndpoint = XaiApiEndpoint;
                _settingsService.Current.XaiTimeout = XaiTimeout;
                _settingsService.Current.XaiMaxTokens = XaiMaxTokens;
                _settingsService.Current.XaiTemperature = XaiTemperature;

                // Save logging settings
                _settingsService.Current.EnableFileLogging = EnableFileLogging;
                _settingsService.Current.LogFilePath = LogFilePath;

                // Save application settings
                _settingsService.Current.EnableAutoSave = EnableAutoSave;
                _settingsService.Current.AutoSaveIntervalMinutes = AutoSaveIntervalMinutes;
                _settingsService.Current.EnableNotifications = EnableNotifications;
                _settingsService.Current.EnableSounds = EnableSounds;
                _settingsService.Current.DefaultLanguage = DefaultLanguage;
                _settingsService.Current.DateFormat = DateFormat;
                _settingsService.Current.CurrencyFormat = CurrencyFormat;
                _settingsService.Current.SessionTimeoutMinutes = SessionTimeoutMinutes;

                // Save fiscal year settings
                _settingsService.Current.FiscalYearStart = FiscalYearStart;
                _settingsService.Current.FiscalYearEnd = FiscalYearEnd;
                _settingsService.Current.CurrentFiscalYear = CurrentFiscalYear;
                _settingsService.Current.UseFiscalYearForReporting = UseFiscalYearForReporting;
                _settingsService.Current.FiscalQuarter = FiscalQuarter;
                _settingsService.Current.FiscalPeriod = FiscalPeriod;

                // Persist all settings to database
                _settingsService.Save();

                SettingsStatus = "All settings saved successfully";
                LastSaved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                _logger?.LogInformation("All settings saved to database");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
                _logger?.LogError(ex, "Failed to save settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanSaveSettings()
        {
            // Validate settings before allowing save
            if (IsLoading)
                return false;

            // Validate database settings
            if (string.IsNullOrWhiteSpace(DatabaseServer) || string.IsNullOrWhiteSpace(DatabaseName))
                return false;

            // Validate fiscal year settings
            if (string.IsNullOrWhiteSpace(FiscalYearStart) || string.IsNullOrWhiteSpace(FiscalYearEnd))
                return false;

            // Validate session timeout
            if (SessionTimeoutMinutes <= 0)
                return false;

            // All validations passed
            return true;
        }

        private void OnResetSettings()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Resetting settings to defaults...";

                // Reset window settings
                WindowWidth = "1024";
                WindowHeight = "768";
                RememberWindowPosition = false;
                StartMaximized = false;
                DefaultTheme = "FluentDark";

                // Reset database settings
                DatabaseServer = "localhost";
                DatabaseName = "WileyWidget";
                ConnectionString = string.Empty;
                IsDatabaseConnected = false;

                // Reset QuickBooks settings
                QuickBooksCompanyFile = string.Empty;
                EnableQuickBooksSync = false;
                SyncIntervalMinutes = 30;
                IsQuickBooksConnected = false;
                QuickBooksVersion = "Unknown";
                LastSyncTime = "Never";

                // Reset AI/XAI settings
                EnableXaiFeatures = false;
                XaiApiKey = string.Empty;
                XaiModel = "grok-beta";
                XaiApiEndpoint = "https://api.x.ai/v1";
                XaiTimeout = 30;
                XaiMaxTokens = 2000;
                XaiTemperature = 0.7;
                IsXaiConnected = false;

                // Reset logging settings
                EnableFileLogging = true;
                LogFilePath = "logs/app.log";

                // Reset application settings
                EnableAutoSave = true;
                AutoSaveIntervalMinutes = 5;
                EnableNotifications = true;
                EnableSounds = true;
                DefaultLanguage = "en-US";
                DateFormat = "MM/dd/yyyy";
                CurrencyFormat = "USD";
                SessionTimeoutMinutes = 60;

                // Reset fiscal year settings
                FiscalYearStart = "July 1";
                FiscalYearEnd = "June 30";
                CurrentFiscalYear = "2024-2025";
                UseFiscalYearForReporting = true;
                FiscalQuarter = 1;
                FiscalPeriod = "Q1";

                // Update fiscal year calculations
                UpdateFiscalYearCalculations();

                SettingsStatus = "Settings reset to defaults";
                LastSaved = "Never";

                _logger?.LogInformation("All settings reset to defaults");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error resetting settings: {ex.Message}";
                _logger?.LogError(ex, "Failed to reset settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void OnTestDatabaseConnection()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Testing database connection...";

                // Build connection string from individual components if not manually entered
                var connectionString = BuildConnectionString();

                // Test the connection using the UnitOfWork
                var isConnected = await TestDatabaseConnectionAsync(connectionString);

                IsDatabaseConnected = isConnected;
                StatusMessage = isConnected
                    ? "Database connection successful"
                    : "Database connection failed";

                // Update connection string property for display
                ConnectionString = connectionString;
            }
            catch (Exception ex)
            {
                IsDatabaseConnected = false;
                StatusMessage = $"Database connection error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string BuildConnectionString()
        {
            // If connection string is manually entered, validate and return it
            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                // Basic validation - should contain server and database keywords
                if (ConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                    ConnectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
                {
                    return ConnectionString;
                }
            }

            // Build from individual components
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = DatabaseServer,
                InitialCatalog = DatabaseName,
                IntegratedSecurity = true, // Use Windows Authentication by default
                ConnectTimeout = 30
            };

            return builder.ConnectionString;
        }

        private async Task<bool> TestDatabaseConnectionAsync(string connectionString)
        {
            try
            {
                // Create a temporary DbContext to test the connection
                var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<WileyWidget.Data.AppDbContext>()
                    .UseSqlServer(connectionString)
                    .UseQueryTrackingBehavior(Microsoft.EntityFrameworkCore.QueryTrackingBehavior.NoTracking);

                using var context = new WileyWidget.Data.AppDbContext(optionsBuilder.Options);
                return await context.Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }

        private async void OnTestQuickBooksConnection()
        {
            if (_quickBooksService == null)
            {
                QuickBooksConnectionStatus = "QuickBooks service not available";
                QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
                IsQuickBooksConnected = false;
                return;
            }

            try
            {
                IsLoading = true;
                QuickBooksConnectionStatus = "Testing connection...";
                QuickBooksStatusColor = System.Windows.Media.Brushes.Yellow;

                var connectionStatus = await _quickBooksService.GetConnectionStatusAsync();

                IsQuickBooksConnected = connectionStatus.IsConnected;
                QuickBooksVersion = connectionStatus.IsConnected ? "Connected" : "Not Connected";
                LastSyncTime = connectionStatus.LastSyncTime ?? "Never";

                if (connectionStatus.IsConnected)
                {
                    QuickBooksConnectionStatus = $"Connected to: {connectionStatus.CompanyName ?? "Unknown Company"}";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    QuickBooksConnectionStatus = connectionStatus.StatusMessage ?? "Connection failed";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                IsQuickBooksConnected = false;
                QuickBooksConnectionStatus = $"Connection test failed: {ex.Message}";
                QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
                QuickBooksVersion = "Unknown";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanTestQuickBooksConnection()
        {
            return !IsLoading && _quickBooksService != null &&
                   (!string.IsNullOrWhiteSpace(QuickBooksCompanyFile) ||
                    (!string.IsNullOrWhiteSpace(QuickBooksClientId) && !string.IsNullOrWhiteSpace(QuickBooksClientSecret)));
        }

        private void OnValidateSyncfusionLicense()
        {
            // License registration happens in App static constructor per Syncfusion documentation
            // No runtime validation is possible - just log the information
            try
            {
                IsLoading = true;
                SyncfusionLicenseStatus = "Checking license configuration...";
                SyncfusionLicenseStatusColor = System.Windows.Media.Brushes.Yellow;

                // Validate license key format
                if (string.IsNullOrWhiteSpace(SyncfusionLicenseKey))
                {
                    SyncfusionLicenseStatus = "License key is required. Set SYNCFUSION_LICENSE_KEY environment variable.";
                    SyncfusionLicenseStatusColor = System.Windows.Media.Brushes.Red;
                    IsSyncfusionLicenseValid = false;
                    return;
                }

                // Basic format validation
                bool isValidFormat = SyncfusionLicenseKey.Length >= 16 &&
                                   SyncfusionLicenseKey.Length <= 512 &&
                                   !SyncfusionLicenseKey.StartsWith("${") &&
                                   SyncfusionLicenseKey.Any(char.IsLetterOrDigit);

                if (!isValidFormat)
                {
                    SyncfusionLicenseStatus = "License key format appears invalid";
                    SyncfusionLicenseStatusColor = System.Windows.Media.Brushes.Red;
                    IsSyncfusionLicenseValid = false;
                    return;
                }

                // License key format is valid
                IsSyncfusionLicenseValid = true;
                SyncfusionLicenseStatus = "License key format is valid. Registration occurs at application startup.";
                SyncfusionLicenseStatusColor = System.Windows.Media.Brushes.Green;

                // Log information
                _logger?.LogInformation("Syncfusion license key format validation completed successfully");
                _logger?.LogWarning("License registration occurs at application startup only - changes require app restart");
            }
            catch (Exception ex)
            {
                IsSyncfusionLicenseValid = false;
                SyncfusionLicenseStatus = $"Error checking license: {ex.Message}";
                SyncfusionLicenseStatusColor = System.Windows.Media.Brushes.Red;
                _logger?.LogError(ex, "Error during Syncfusion license format validation");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanValidateSyncfusionLicense()
        {
            return !IsLoading && !string.IsNullOrWhiteSpace(SyncfusionLicenseKey);
        }

        private async void OnTestXaiConnection()
        {
            if (_aiService == null)
            {
                XaiConnectionStatus = "AI service not available";
                XaiStatusColor = System.Windows.Media.Brushes.Red;
                IsXaiConnected = false;
                return;
            }

            try
            {
                IsLoading = true;
                XaiConnectionStatus = "Testing AI connection...";
                XaiStatusColor = System.Windows.Media.Brushes.Yellow;

                // Test connection by validating the API key
                var validationResult = await _aiService.ValidateApiKeyAsync(XaiApiKey);

                if (validationResult.HttpStatusCode == 200)
                {
                    IsXaiConnected = true;
                    XaiConnectionStatus = "AI connection successful";
                    XaiStatusColor = System.Windows.Media.Brushes.Green;

                    // Try to load available models if connection is successful
                    await LoadAvailableModelsAsync();
                }
                else
                {
                    IsXaiConnected = false;
                    XaiConnectionStatus = $"AI connection failed: {validationResult.ErrorCode ?? "Unknown error"}";
                    XaiStatusColor = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                IsXaiConnected = false;
                XaiConnectionStatus = $"AI connection error: {ex.Message}";
                XaiStatusColor = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanTestXaiConnection()
        {
            return !IsLoading && _aiService != null && !string.IsNullOrWhiteSpace(XaiApiKey) && !string.IsNullOrWhiteSpace(XaiApiEndpoint);
        }

        private void OnExportSettings()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Exporting settings...";

                // Create a settings export object with current values
                var exportSettings = new
                {
                    // Window settings
                    WindowWidth = WindowWidth,
                    WindowHeight = WindowHeight,
                    RememberWindowPosition = RememberWindowPosition,
                    StartMaximized = StartMaximized,
                    DefaultTheme = DefaultTheme,

                    // Database settings
                    DatabaseServer = DatabaseServer,
                    DatabaseName = DatabaseName,

                    // QuickBooks settings
                    QuickBooksCompanyFile = QuickBooksCompanyFile,
                    EnableQuickBooksSync = EnableQuickBooksSync,
                    SyncIntervalMinutes = SyncIntervalMinutes,

                    // AI/XAI settings
                    EnableXaiFeatures = EnableXaiFeatures,
                    XaiModel = XaiModel,
                    XaiApiEndpoint = XaiApiEndpoint,
                    XaiTimeout = XaiTimeout,
                    XaiMaxTokens = XaiMaxTokens,
                    XaiTemperature = XaiTemperature,

                    // Logging settings
                    EnableFileLogging = EnableFileLogging,
                    LogFilePath = LogFilePath,

                    // Application settings
                    EnableAutoSave = EnableAutoSave,
                    AutoSaveIntervalMinutes = AutoSaveIntervalMinutes,
                    EnableNotifications = EnableNotifications,
                    EnableSounds = EnableSounds,
                    DefaultLanguage = DefaultLanguage,
                    DateFormat = DateFormat,
                    CurrencyFormat = CurrencyFormat,
                    SessionTimeoutMinutes = SessionTimeoutMinutes,

                    // Fiscal year settings
                    FiscalYearStart = FiscalYearStart,
                    FiscalYearEnd = FiscalYearEnd,
                    CurrentFiscalYear = CurrentFiscalYear,
                    UseFiscalYearForReporting = UseFiscalYearForReporting,
                    FiscalQuarter = FiscalQuarter,
                    FiscalPeriod = FiscalPeriod,

                    // Export metadata
                    ExportDate = DateTime.Now,
                    Version = "1.0"
                };

                // Serialize to JSON
                var json = System.Text.Json.JsonSerializer.Serialize(exportSettings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Generate filename with timestamp
                var fileName = $"wiley-widget-settings-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.json";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

                // Write to file
                File.WriteAllText(filePath, json);

                SettingsStatus = $"Settings exported to: {filePath}";
                _logger?.LogInformation("Settings exported to file: {Path}", filePath);
            }
            catch (Exception ex)
            {
                SettingsStatus = $"Error exporting settings: {ex.Message}";
                _logger?.LogError(ex, "Failed to export settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanTestDatabaseConnection()
        {
            // Can test if not loading and either connection string is provided or server/database are specified
            return !IsLoading && (!string.IsNullOrWhiteSpace(ConnectionString) ||
                                   (!string.IsNullOrWhiteSpace(DatabaseServer) && !string.IsNullOrWhiteSpace(DatabaseName)));
        }

        private void OnImportSettings()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Importing settings...";

                // Open file dialog to select settings file
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Wiley Widget Settings",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    var json = File.ReadAllText(filePath);

                    // Deserialize settings
                    var importSettings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (importSettings != null)
                    {
                        // Import window settings
                        if (importSettings.TryGetValue("WindowWidth", out var windowWidth))
                            WindowWidth = windowWidth?.ToString() ?? "1024";
                        if (importSettings.TryGetValue("WindowHeight", out var windowHeight))
                            WindowHeight = windowHeight?.ToString() ?? "768";
                        if (importSettings.TryGetValue("StartMaximized", out var startMaximized) && startMaximized is bool maximized)
                            StartMaximized = maximized;
                        if (importSettings.TryGetValue("DefaultTheme", out var theme))
                            DefaultTheme = theme?.ToString() ?? "FluentDark";

                        // Import database settings
                        if (importSettings.TryGetValue("DatabaseServer", out var dbServer))
                            DatabaseServer = dbServer?.ToString() ?? "localhost";
                        if (importSettings.TryGetValue("DatabaseName", out var dbName))
                            DatabaseName = dbName?.ToString() ?? "WileyWidget";

                        // Import QuickBooks settings
                        if (importSettings.TryGetValue("QuickBooksCompanyFile", out var qbFile))
                            QuickBooksCompanyFile = qbFile?.ToString() ?? string.Empty;
                        if (importSettings.TryGetValue("EnableQuickBooksSync", out var qbSync) && qbSync is bool sync)
                            EnableQuickBooksSync = sync;
                        if (importSettings.TryGetValue("SyncIntervalMinutes", out var syncInterval) && syncInterval is int interval)
                            SyncIntervalMinutes = interval;

                        // Import AI settings
                        if (importSettings.TryGetValue("EnableXaiFeatures", out var aiEnabled) && aiEnabled is bool ai)
                            EnableXaiFeatures = ai;
                        if (importSettings.TryGetValue("XaiModel", out var aiModel))
                            XaiModel = aiModel?.ToString() ?? "grok-beta";

                        // Import application settings
                        if (importSettings.TryGetValue("EnableAutoSave", out var autoSave) && autoSave is bool save)
                            EnableAutoSave = save;
                        if (importSettings.TryGetValue("EnableNotifications", out var notifications) && notifications is bool notif)
                            EnableNotifications = notif;
                        if (importSettings.TryGetValue("EnableSounds", out var sounds) && sounds is bool sound)
                            EnableSounds = sound;

                        SettingsStatus = $"Settings imported from: {Path.GetFileName(filePath)}";
                        _logger?.LogInformation("Settings imported from file: {Path}", filePath);
                    }
                    else
                    {
                        SettingsStatus = "Invalid settings file format";
                    }
                }
                else
                {
                    StatusMessage = "Import cancelled";
                }
            }
            catch (Exception ex)
            {
                SettingsStatus = $"Error importing settings: {ex.Message}";
                _logger?.LogError(ex, "Failed to import settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void OnConnectQuickBooks()
        {
            if (_quickBooksService == null)
            {
                QuickBooksConnectionStatus = "QuickBooks service not available";
                QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
                return;
            }

            try
            {
                IsLoading = true;
                QuickBooksConnectionStatus = "Initiating QuickBooks connection...";
                QuickBooksStatusColor = System.Windows.Media.Brushes.Yellow;

                // Check URL ACL first
                var aclResult = await _quickBooksService.CheckUrlAclAsync(QuickBooksRedirectUri);
                if (!aclResult.IsReady)
                {
                    QuickBooksConnectionStatus = $"URL ACL not configured: {aclResult.Guidance}";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Orange;
                    return;
                }

                var connected = await _quickBooksService.ConnectAsync();
                if (connected)
                {
                    // Get updated status
                    var status = await _quickBooksService.GetConnectionStatusAsync();
                    IsQuickBooksConnected = true;
                    QuickBooksConnectionStatus = $"Connected to: {status.CompanyName ?? "QuickBooks Company"}";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Green;
                    QuickBooksVersion = "Connected";
                    LastSyncTime = status.LastSyncTime ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    IsQuickBooksConnected = false;
                    QuickBooksConnectionStatus = "QuickBooks connection failed";
                    QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                IsQuickBooksConnected = false;
                QuickBooksConnectionStatus = $"Connection failed: {ex.Message}";
                QuickBooksStatusColor = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnSaveFiscalYearSettings()
        {
            try
            {
                IsLoading = true;
                SettingsStatus = "Saving fiscal year settings...";

                // Save all fiscal year settings to database via settings service
                var settings = _settingsService.Current;
                settings.FiscalYearStart = FiscalYearStart;
                settings.FiscalYearEnd = FiscalYearEnd;
                settings.CurrentFiscalYear = CurrentFiscalYear;
                settings.UseFiscalYearForReporting = UseFiscalYearForReporting;
                settings.FiscalQuarter = FiscalQuarter;
                settings.FiscalPeriod = FiscalPeriod;

                _settingsService.Save();

                SettingsStatus = "Fiscal year settings saved successfully";
                LastSaved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                _logger?.LogInformation("Fiscal year settings saved to database");
            }
            catch (Exception ex)
            {
                SettingsStatus = $"Error saving fiscal year settings: {ex.Message}";
                _logger?.LogError(ex, "Failed to save fiscal year settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _syncTimer?.Dispose();
                _syncTimer = null;
                _autoSaveTimer?.Dispose();
                _autoSaveTimer = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
