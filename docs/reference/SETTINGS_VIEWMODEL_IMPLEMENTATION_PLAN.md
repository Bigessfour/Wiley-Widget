# SettingsViewModel Implementation Plan

## Overview

The `SettingsViewModel` is severely incomplete, missing ~80 properties and commands that `SettingsView.xaml` expects. This document outlines what needs to be implemented in each file to resolve all runtime binding errors.

---

## 1. Core File: `WileyWidget.UI/ViewModels/Main/SettingsViewModel.cs`

### Current State

- **Implemented**: 3 properties (`Theme`, `SaveCommand`, `IsXaiKeyValidated`)
- **Missing**: 80+ properties, 9 commands
- **Status**: 📕 **INCOMPLETE** - Only ~4% implemented

### Required Implementation

#### A. Commands (10 total)

```csharp
public ICommand SaveSettingsCommand { get; private set; }      // Currently named SaveCommand
public ICommand ResetSettingsCommand { get; private set; }
public ICommand TestConnectionCommand { get; private set; }
public ICommand TestQuickBooksConnectionCommand { get; private set; }
public ICommand ConnectQuickBooksCommand { get; private set; }
public ICommand CheckQuickBooksUrlAclCommand { get; private set; }
public ICommand ValidateLicenseCommand { get; private set; }
public ICommand TestXaiConnectionCommand { get; private set; }
public ICommand SaveFiscalYearSettingsCommand { get; private set; }
```

**Implementation Pattern**:

```csharp
private void InitializeCommands()
{
    SaveSettingsCommand = new DelegateCommand(ExecuteSaveSettings, CanSaveSettings);
    ResetSettingsCommand = new DelegateCommand(ExecuteResetSettings);
    TestConnectionCommand = new DelegateCommand(async () => await ExecuteTestConnectionAsync());
    // ... etc
}
```

#### B. UI/General Settings (9 properties)

```csharp
private string _searchText = string.Empty;
public string SearchText
{
    get => _searchText;
    set => SetProperty(ref _searchText, value);
}

private ObservableCollection<string> _availableThemes = new() { "FluentLight", "FluentDark" };
public ObservableCollection<string> AvailableThemes
{
    get => _availableThemes;
    set => SetProperty(ref _availableThemes, value);
}

private string _selectedTheme = "FluentDark";
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
public string WindowWidthValidation
{
    get => _windowWidthValidation;
    set => SetProperty(ref _windowWidthValidation, value);
}

private int _windowHeight = 720;
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
public string WindowHeightValidation
{
    get => _windowHeightValidation;
    set => SetProperty(ref _windowHeightValidation, value);
}

private bool _maximizeOnStartup = false;
public bool MaximizeOnStartup
{
    get => _maximizeOnStartup;
    set => SetProperty(ref _maximizeOnStartup, value);
}

private bool _showSplashScreen = true;
public bool ShowSplashScreen
{
    get => _showSplashScreen;
    set => SetProperty(ref _showSplashScreen, value);
}
```

#### C. Database Settings (3 properties)

```csharp
private string _databaseConnectionString = string.Empty;
public string DatabaseConnectionString
{
    get => _databaseConnectionString;
    set => SetProperty(ref _databaseConnectionString, value);
}

private string _databaseStatus = "Not Connected";
public string DatabaseStatus
{
    get => _databaseStatus;
    set => SetProperty(ref _databaseStatus, value);
}

private Brush _databaseStatusColor = Brushes.Gray;
public Brush DatabaseStatusColor
{
    get => _databaseStatusColor;
    set => SetProperty(ref _databaseStatusColor, value);
}
```

**Note**: Requires `using System.Windows.Media;` for `Brush` type.

#### D. QuickBooks Settings (13 properties)

```csharp
private string _quickBooksClientId = string.Empty;
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
public string QuickBooksClientIdValidation
{
    get => _quickBooksClientIdValidation;
    set => SetProperty(ref _quickBooksClientIdValidation, value);
}

private string _quickBooksClientSecret = string.Empty;
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
public string QuickBooksClientSecretValidation
{
    get => _quickBooksClientSecretValidation;
    set => SetProperty(ref _quickBooksClientSecretValidation, value);
}

private string _quickBooksRedirectUri = "http://localhost:8080/callback";
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
public string QuickBooksRedirectUriValidation
{
    get => _quickBooksRedirectUriValidation;
    set => SetProperty(ref _quickBooksRedirectUriValidation, value);
}

private ObservableCollection<string> _quickBooksEnvironments = new() { "Sandbox", "Production" };
public ObservableCollection<string> QuickBooksEnvironments
{
    get => _quickBooksEnvironments;
    set => SetProperty(ref _quickBooksEnvironments, value);
}

private string _selectedQuickBooksEnvironment = "Sandbox";
public string SelectedQuickBooksEnvironment
{
    get => _selectedQuickBooksEnvironment;
    set => SetProperty(ref _selectedQuickBooksEnvironment, value);
}

private string _quickBooksConnectionStatus = "Not Connected";
public string QuickBooksConnectionStatus
{
    get => _quickBooksConnectionStatus;
    set => SetProperty(ref _quickBooksConnectionStatus, value);
}

private Brush _quickBooksStatusColor = Brushes.Gray;
public Brush QuickBooksStatusColor
{
    get => _quickBooksStatusColor;
    set => SetProperty(ref _quickBooksStatusColor, value);
}

private string _quickBooksUrlAclStatus = "Not Configured";
public string QuickBooksUrlAclStatus
{
    get => _quickBooksUrlAclStatus;
    set => SetProperty(ref _quickBooksUrlAclStatus, value);
}

private Brush _quickBooksUrlAclStatusColor = Brushes.Gray;
public Brush QuickBooksUrlAclStatusColor
{
    get => _quickBooksUrlAclStatusColor;
    set => SetProperty(ref _quickBooksUrlAclStatusColor, value);
}
```

#### E. Syncfusion License Settings (4 properties)

```csharp
private string _syncfusionLicenseKey = string.Empty;
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
public string SyncfusionLicenseKeyValidation
{
    get => _syncfusionLicenseKeyValidation;
    set => SetProperty(ref _syncfusionLicenseKeyValidation, value);
}

private string _syncfusionLicenseStatus = "Not Validated";
public string SyncfusionLicenseStatus
{
    get => _syncfusionLicenseStatus;
    set => SetProperty(ref _syncfusionLicenseStatus, value);
}

private Brush _syncfusionLicenseStatusColor = Brushes.Gray;
public Brush SyncfusionLicenseStatusColor
{
    get => _syncfusionLicenseStatusColor;
    set => SetProperty(ref _syncfusionLicenseStatusColor, value);
}
```

#### F. X.AI Settings (21 properties)

```csharp
private string _xaiApiKey = string.Empty;
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
public string XaiApiKeyValidation
{
    get => _xaiApiKeyValidation;
    set => SetProperty(ref _xaiApiKeyValidation, value);
}

private string _xaiBaseUrl = "https://api.x.ai/v1";
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
public ObservableCollection<string> AvailableModels
{
    get => _availableModels;
    set => SetProperty(ref _availableModels, value);
}

private string _xaiModel = "grok-beta";
public string XaiModel
{
    get => _xaiModel;
    set => SetProperty(ref _xaiModel, value);
}

private int _xaiTimeoutSeconds = 30;
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
public ObservableCollection<string> AvailableResponseStyles
{
    get => _availableResponseStyles;
    set => SetProperty(ref _availableResponseStyles, value);
}

private string _responseStyle = "Detailed";
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
public ObservableCollection<string> AvailablePersonalities
{
    get => _availablePersonalities;
    set => SetProperty(ref _availablePersonalities, value);
}

private string _personality = "Assistant";
public string Personality
{
    get => _personality;
    set => SetProperty(ref _personality, value);
}

private int _contextWindowSize = 8192;
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
public string ContextWindowValidation
{
    get => _contextWindowValidation;
    set => SetProperty(ref _contextWindowValidation, value);
}

private bool _enableSafetyFilters = true;
public bool EnableSafetyFilters
{
    get => _enableSafetyFilters;
    set => SetProperty(ref _enableSafetyFilters, value);
}

private double _temperature = 0.7;
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
public string TemperatureValidation
{
    get => _temperatureValidation;
    set => SetProperty(ref _temperatureValidation, value);
}

private int _maxTokens = 2048;
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
public string MaxTokensValidation
{
    get => _maxTokensValidation;
    set => SetProperty(ref _maxTokensValidation, value);
}

private bool _enableStreaming = false;
public bool EnableStreaming
{
    get => _enableStreaming;
    set => SetProperty(ref _enableStreaming, value);
}

private string _xaiConnectionStatus = "Not Connected";
public string XaiConnectionStatus
{
    get => _xaiConnectionStatus;
    set => SetProperty(ref _xaiConnectionStatus, value);
}

private Brush _xaiStatusColor = Brushes.Gray;
public Brush XaiStatusColor
{
    get => _xaiStatusColor;
    set => SetProperty(ref _xaiStatusColor, value);
}
```

#### G. Fiscal Year Settings (9 properties)

```csharp
private ObservableCollection<MonthItem> _fiscalYearMonths = new();
public ObservableCollection<MonthItem> FiscalYearMonths
{
    get => _fiscalYearMonths;
    set => SetProperty(ref _fiscalYearMonths, value);
}

private int _fiscalYearStartMonth = 1; // January
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
public string FiscalYearDayValidation
{
    get => _fiscalYearDayValidation;
    set => SetProperty(ref _fiscalYearDayValidation, value);
}

private string _currentFiscalYearDisplay = string.Empty;
public string CurrentFiscalYearDisplay
{
    get => _currentFiscalYearDisplay;
    set => SetProperty(ref _currentFiscalYearDisplay, value);
}

private string _fiscalYearPeriodDisplay = string.Empty;
public string FiscalYearPeriodDisplay
{
    get => _fiscalYearPeriodDisplay;
    set => SetProperty(ref _fiscalYearPeriodDisplay, value);
}

private string _daysRemainingInFiscalYear = string.Empty;
public string DaysRemainingInFiscalYear
{
    get => _daysRemainingInFiscalYear;
    set => SetProperty(ref _daysRemainingInFiscalYear, value);
}

private ObservableCollection<FiscalYearInfo> _availableFiscalYears = new();
public ObservableCollection<FiscalYearInfo> AvailableFiscalYears
{
    get => _availableFiscalYears;
    set => SetProperty(ref _availableFiscalYears, value);
}
```

**Note**: Requires helper classes `MonthItem` and `FiscalYearInfo`.

#### H. System/Advanced Settings (9 properties)

```csharp
private bool _enableDynamicColumns = true;
public bool EnableDynamicColumns
{
    get => _enableDynamicColumns;
    set => SetProperty(ref _enableDynamicColumns, value);
}

private bool _enableDataCaching = true;
public bool EnableDataCaching
{
    get => _enableDataCaching;
    set => SetProperty(ref _enableDataCaching, value);
}

private int _cacheExpirationMinutes = 30;
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
public ObservableCollection<string> LogLevels
{
    get => _logLevels;
    set => SetProperty(ref _logLevels, value);
}

private string _selectedLogLevel = "Information";
public string SelectedLogLevel
{
    get => _selectedLogLevel;
    set => SetProperty(ref _selectedLogLevel, value);
}

private bool _enableFileLogging = true;
public bool EnableFileLogging
{
    get => _enableFileLogging;
    set => SetProperty(ref _enableFileLogging, value);
}

private string _logFilePath = "logs/app.log";
public string LogFilePath
{
    get => _logFilePath;
    set => SetProperty(ref _logFilePath, value);
}

private string _systemInfo = string.Empty;
public string SystemInfo
{
    get => _systemInfo;
    set => SetProperty(ref _systemInfo, value);
}
```

#### I. Status/Busy Indicators (4 properties)

```csharp
private string _settingsStatus = "Ready";
public string SettingsStatus
{
    get => _settingsStatus;
    set => SetProperty(ref _settingsStatus, value);
}

private string _lastSaved = "Never";
public string LastSaved
{
    get => _lastSaved;
    set => SetProperty(ref _lastSaved, value);
}

private bool _isBusy = false;
public bool IsBusy
{
    get => _isBusy;
    set => SetProperty(ref _isBusy, value);
}

private string _busyMessage = string.Empty;
public string BusyMessage
{
    get => _busyMessage;
    set => SetProperty(ref _busyMessage, value);
}
```

---

## 2. Supporting Classes (New Files Needed)

### `WileyWidget.UI/Models/MonthItem.cs`

```csharp
namespace WileyWidget.UI.Models
{
    public class MonthItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
```

### `WileyWidget.UI/Models/FiscalYearInfo.cs`

```csharp
namespace WileyWidget.UI.Models
{
    public class FiscalYearInfo
    {
        public int Year { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DisplayName => $"FY {Year}";
    }
}
```

---

## 3. Validation Methods (Add to SettingsViewModel)

```csharp
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

private void ValidateXaiApiKey()
{
    if (string.IsNullOrWhiteSpace(XaiApiKey))
    {
        XaiApiKeyValidation = "API Key is required";
    }
    else if (!XaiApiKey.StartsWith("xai-"))
    {
        XaiApiKeyValidation = "X.AI keys typically start with 'xai-'";
    }
    else
    {
        XaiApiKeyValidation = string.Empty;
    }
}

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
```

---

## 4. Command Implementations (Add to SettingsViewModel)

```csharp
private void ExecuteSaveSettings()
{
    try
    {
        IsBusy = true;
        BusyMessage = "Saving settings...";

        _settingsService.Value.Save();

        LastSaved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

private bool CanSaveSettings()
{
    return !IsBusy
        && string.IsNullOrEmpty(WindowWidthValidation)
        && string.IsNullOrEmpty(WindowHeightValidation)
        && string.IsNullOrEmpty(QuickBooksClientIdValidation)
        && string.IsNullOrEmpty(QuickBooksClientSecretValidation);
}

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

// Helper methods
private async Task<bool> TestDatabaseConnectionAsync()
{
    // TODO: Implement actual database connection test
    await Task.Delay(1000); // Simulate async operation
    return true;
}

private async Task<bool> CheckUrlAclAsync(string uri)
{
    // TODO: Implement actual URL ACL check
    await Task.Delay(500);
    return true;
}

private async Task<bool> ValidateSyncfusionLicenseAsync(string key)
{
    // TODO: Implement actual license validation
    await Task.Delay(500);
    return !string.IsNullOrWhiteSpace(key);
}

private async Task<bool> TestXaiConnectionAsync()
{
    // TODO: Implement actual X.AI connection test
    await Task.Delay(1000);
    return !string.IsNullOrWhiteSpace(XaiApiKey);
}

private void UpdateFiscalYearDisplay()
{
    var startDate = new DateTime(DateTime.Now.Year, FiscalYearStartMonth, FiscalYearStartDay);
    var endDate = startDate.AddYears(1).AddDays(-1);

    CurrentFiscalYearDisplay = $"FY {startDate.Year}";
    FiscalYearPeriodDisplay = $"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}";

    var daysRemaining = (endDate - DateTime.Now).Days;
    DaysRemainingInFiscalYear = daysRemaining > 0 ? $"{daysRemaining} days remaining" : "Fiscal year ended";
}

private void ApplyTheme(string themeName)
{
    // TODO: Implement theme application logic
    _logger.LogInformation("Theme changed to: {ThemeName}", themeName);
}

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

private void LoadSystemInfo()
{
    var osVersion = Environment.OSVersion;
    var clrVersion = Environment.Version;
    var processorCount = Environment.ProcessorCount;

    SystemInfo = $"OS: {osVersion}\n.NET: {clrVersion}\nProcessors: {processorCount}";
}
```

---

## 5. Constructor Updates

**Update the constructors to initialize all data:**

```csharp
protected SettingsViewModel()
{
    using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    _logger = loggerFactory.CreateLogger<SettingsViewModel>();
    _quickBooksService = new Lazy<IQuickBooksService>(() => throw new InvalidOperationException("DI required"));
    _settingsService = new Lazy<ISettingsService>(() => throw new InvalidOperationException("DI required"));

    LogWarning(_logger, "SettingsViewModel created with fallback ctor");

    InitializeCommands();
    InitializeFiscalYearData();
    LoadSystemInfo();
}

public SettingsViewModel(ILogger<SettingsViewModel> logger, Lazy<IQuickBooksService> quickBooksService, Lazy<ISettingsService> settingsService)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
    _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

    LogInformation(_logger, "SettingsViewModel initialized with full DI");

    InitializeCommands();
    InitializeFiscalYearData();
    LoadSystemInfo();
}
```

---

## 6. Required Using Directives

**Add to top of SettingsViewModel.cs:**

```csharp
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using WileyWidget.Services;
using WileyWidget.UI.Models;
```

---

## 7. Service Interface Updates (if needed)

### `ISettingsService` additions (if missing)

```csharp
public interface ISettingsService
{
    void Save();
    void SaveFiscalYearSettings(int month, int day);
    Task LoadAsync();
}
```

### `IQuickBooksService` additions (if missing)

```csharp
public interface IQuickBooksService
{
    Task<bool> TestConnectionAsync();
    Task ConnectAsync();
}
```

---

## 8. Implementation Checklist

### Phase 1: Basic Properties (2-3 hours)

- [ ] Add all backing fields
- [ ] Implement UI/General properties (9)
- [ ] Implement Database properties (3)
- [ ] Update constructors with initialization
- [ ] Test basic binding

### Phase 2: Commands (2-3 hours)

- [ ] Implement all 9 command properties
- [ ] Add command execution methods
- [ ] Add async command handlers
- [ ] Wire up in `InitializeCommands()`

### Phase 3: QuickBooks & Syncfusion (2-3 hours)

- [ ] Implement QuickBooks properties (13)
- [ ] Implement Syncfusion properties (4)
- [ ] Add validation methods
- [ ] Test connection commands

### Phase 4: X.AI Integration (3-4 hours)

- [ ] Implement X.AI properties (21)
- [ ] Add validation methods
- [ ] Implement test connection logic
- [ ] Test API key validation

### Phase 5: Fiscal Year & Advanced (2-3 hours)

- [ ] Create `MonthItem` and `FiscalYearInfo` classes
- [ ] Implement Fiscal Year properties (9)
- [ ] Implement System/Advanced properties (9)
- [ ] Add fiscal year calculation logic

### Phase 6: Status & Polish (1-2 hours)

- [ ] Implement status properties (4)
- [ ] Add busy indicators
- [ ] Test all validation
- [ ] Final integration testing

---

## 9. Testing Strategy

1. **Build Verification**: `dotnet build` should succeed
2. **Runtime Verification**: No binding errors in Output window
3. **UI Testing**: All controls should display and update properly
4. **Command Testing**: All buttons/ribbons should execute commands
5. **Validation Testing**: All validation messages should appear correctly

---

## 10. Estimated Effort

- **Total Properties**: 80+
- **Total Commands**: 9
- **Validation Methods**: 12
- **Helper Classes**: 2
- **Total Effort**: 15-20 hours for complete implementation

---

## 11. Priority Order

1. **Critical** (Blocks UI): Commands, basic UI properties
2. **High** (User-facing): Database, QuickBooks, X.AI settings
3. **Medium** (Advanced): Fiscal Year, System settings
4. **Low** (Polish): Busy indicators, status messages

---

## Notes

- All properties use `SetProperty()` from `BindableBase` for automatic `PropertyChanged` notifications
- Commands use `DelegateCommand` from Prism
- Async commands wrap with `async () => await` pattern
- Validation happens on property setters
- Color properties use `System.Windows.Media.Brush` type
- Collections use `ObservableCollection<T>` for automatic UI updates
