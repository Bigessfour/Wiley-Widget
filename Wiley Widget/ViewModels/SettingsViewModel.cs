using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WileyWidget.Configuration;
using WileyWidget.Services;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel encapsulating application settings editing &amp; persistence, API key lifecycle
/// operations, and theme/model selection. Reduces code-behind complexity in MainWindow.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly IApiKeyFacade _apiKeyFacade;
    private readonly IThemeCoordinator _themeCoordinator;

    [ObservableProperty] private int timeoutSeconds;
    [ObservableProperty] private int cacheTtlMinutes;
    [ObservableProperty] private decimal dailyBudget;
    [ObservableProperty] private decimal monthlyBudget;
    [ObservableProperty] private string apiKey;
    [ObservableProperty] private string selectedTheme;
    [ObservableProperty] private string selectedModel;
    [ObservableProperty] private string statusMessage = "Ready to configure xAI API key. Click 'Load Settings' to see current configuration.";

    public SettingsViewModel(SettingsService settingsService, IApiKeyFacade apiKeyFacade, IThemeCoordinator themeCoordinator)
    {
        _settingsService = settingsService;
        _apiKeyFacade = apiKeyFacade;
        _themeCoordinator = themeCoordinator;
        LoadFromService();
    }

    private void LoadFromService()
    {
        var s = _settingsService.Current;
        TimeoutSeconds = s.XaiTimeoutSeconds <= 0 ? 30 : s.XaiTimeoutSeconds;
        CacheTtlMinutes = s.XaiCacheTtlMinutes <= 0 ? 30 : s.XaiCacheTtlMinutes;
        DailyBudget = s.XaiDailyBudget <= 0 ? 10m : s.XaiDailyBudget;
        MonthlyBudget = s.XaiMonthlyBudget <= 0 ? 300m : s.XaiMonthlyBudget;
        SelectedTheme = s.Theme;
        SelectedModel = s.XaiModel;
    }

    [RelayCommand]
    private void Load()
    {
        try
        {
            LoadFromService();
            var info = _apiKeyFacade.Info();
            StatusMessage = info.IsValid ? "✅ API key is securely stored" : "❌ No API key configured";
            Log.Information("Settings loaded into SettingsViewModel");
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Failed to load settings: {ex.Message}";
            Log.Error(ex, "Load settings failed");
        }
    }

    [RelayCommand]
    private async Task TestApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "❌ Please enter an API key first";
            return;
        }
        StatusMessage = "🔄 Testing xAI API key...";
        try
        {
            var valid = await _apiKeyFacade.TestAsync(ApiKey);
            if (valid)
            {
                StatusMessage = "✅ API key is valid! Saving...";
                if (_apiKeyFacade.SaveSecure(ApiKey))
                {
                    ApiKey = string.Empty; // clear entry field
                    StatusMessage = "✅ API key saved securely!";
                }
                else
                {
                    StatusMessage = "⚠️ Key valid but failed to save securely";
                }
            }
            else
            {
                StatusMessage = "❌ API test failed - check your key";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ API test failed: {ex.Message}";
            Log.Error(ex, "API key test failed");
        }
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            var s = _settingsService.Current;
            s.XaiTimeoutSeconds = TimeoutSeconds;
            s.XaiCacheTtlMinutes = CacheTtlMinutes;
            s.XaiDailyBudget = DailyBudget;
            s.XaiMonthlyBudget = MonthlyBudget;
            if (!string.IsNullOrWhiteSpace(SelectedTheme))
            {
                s.Theme = _themeCoordinator.Current = SelectedTheme; // coordinator persists theme
            }
            if (!string.IsNullOrWhiteSpace(SelectedModel))
            {
                s.XaiModel = SelectedModel;
            }
            _settingsService.Save();
            StatusMessage = "✅ Settings saved successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Failed to save settings: {ex.Message}";
            Log.Error(ex, "Save settings failed");
        }
    }

    [RelayCommand]
    private void RemoveApiKey()
    {
        try
        {
            if (_apiKeyFacade.Remove())
            {
                StatusMessage = "✅ API key removed";
            }
            else
            {
                StatusMessage = "❌ Failed to remove API key";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Remove failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ShowApiInfo()
    {
        try
        {
            var info = _apiKeyFacade.Info();
            Log.Information("API_KEY_INFO Env={Env} UserSecrets={Usr} Encrypted={Enc} Valid={Valid}", info.HasEnvironmentVariable, info.HasUserSecrets, info.HasEncryptedStorage, info.IsValid);
            StatusMessage = "ℹ️ API key info logged";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Info retrieval failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenGuide()
    {
        try
        {
            var guidePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "xAI-API-Key-Setup-Guide.md");
            if (System.IO.File.Exists(guidePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = guidePath, UseShellExecute = true });
            }
            else
            {
                StatusMessage = "❌ Guide not found";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Failed to open guide: {ex.Message}";
        }
    }
}
