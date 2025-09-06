using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Interface for application settings management.
/// </summary>
public interface ISettingsService
{
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
    T GetSetting<T>(string key);
    void SetSetting<T>(string key, T value);
}

/// <summary>
/// Interface for theme management.
/// </summary>
public interface IThemeService
{
    Task InitializeAsync();
    Task ApplyThemeAsync(string themeName);
    string CurrentTheme { get; }
}

/// <summary>
/// Interface for configuration management.
/// </summary>
public interface IConfigurationService
{
    Task LoadConfigurationAsync();
    T GetConfiguration<T>(string sectionName);
}

/// <summary>
/// Interface for business logic operations.
/// </summary>
public interface IBusinessLogicService
{
    Task ProcessDataAsync(object data);
    Task ValidateDataAsync(object data);
}

/// <summary>
/// Interface for data access operations.
/// </summary>
public interface IDataService
{
    Task<object> GetDataAsync(string query);
    Task SaveDataAsync(object data);
}

/// <summary>
/// Interface for data validation.
/// </summary>
public interface IValidationService
{
    Task<bool> ValidateAsync(object data);
    Task<string> GetValidationErrorsAsync(object data);
}
