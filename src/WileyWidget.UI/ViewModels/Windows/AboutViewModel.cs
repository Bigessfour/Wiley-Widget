#nullable enable

using System;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;

namespace WileyWidget.ViewModels.Windows;

/// <summary>
/// ViewModel for the About window
/// </summary>
public class AboutViewModel : AsyncViewModelBase
{
    private string? _applicationName;
    private string? _version;
    private string? _description;
    private string? _copyright;

    /// <summary>
    /// Gets or sets the application name
    /// </summary>
    public string? ApplicationName
    {
        get => _applicationName;
        set => SetProperty(ref _applicationName, value);
    }

    /// <summary>
    /// Gets or sets the application version
    /// </summary>
    public string? Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    /// <summary>
    /// Gets or sets the application description
    /// </summary>
    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Gets or sets the copyright information
    /// </summary>
    public string? Copyright
    {
        get => _copyright;
        set => SetProperty(ref _copyright, value);
    }

    /// <summary>
    /// Gets the command to close the about window
    /// </summary>
    public Prism.Commands.DelegateCommand CloseCommand { get; }

    /// <summary>
    /// Gets the command to open URLs
    /// </summary>
    public Prism.Commands.DelegateCommand<string> OpenUrlCommand { get; }

    /// <summary>
    /// Gets or sets the action to close the window
    /// </summary>
    public Action? CloseAction { get; set; }

    /// <summary>
    /// Initializes a new instance of the AboutViewModel class
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations</param>
    /// <param name="logger">The logger instance</param>
    public AboutViewModel(IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<AboutViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        CloseCommand = new Prism.Commands.DelegateCommand(Close);
        OpenUrlCommand = new Prism.Commands.DelegateCommand<string>(OpenUrl);

        // Initialize with assembly information
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyName = assembly.GetName();

        ApplicationName = assemblyName.Name ?? "Wiley Widget";
        Version = assemblyName.Version?.ToString() ?? "1.0.0.0";
        Description = "Municipal budget management and analysis application";
        Copyright = "© 2025 Wiley Widget. All rights reserved.";
    }

    private void Close()
    {
        CloseAction?.Invoke();
    }

    private void OpenUrl(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to open URL: {Url}", url);
            }
        }
    }
}
