using System;
using Prism.Mvvm;

namespace WileyWidget.ViewModels.Windows;

/// <summary>
/// View model backing the splash screen window visuals and progress indicators.
/// Provides strongly-typed properties consumed by the XAML bindings.
/// </summary>
public partial class SplashScreenWindowViewModel : BindableBase
{
    private string title = "Wiley Widget";
    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    private string subtitle = "Enterprise Business Solutions";
    public string Subtitle
    {
        get => subtitle;
        set => SetProperty(ref subtitle, value);
    }

    private bool isLoading = true;
    public bool IsLoading
    {
        get => isLoading;
        set => SetProperty(ref isLoading, value);
    }

    private bool isIndeterminate = true;
    public bool IsIndeterminate
    {
        get => isIndeterminate;
        set => SetProperty(ref isIndeterminate, value);
    }

    private string statusText = "Starting Wiley Widget...";
    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    private double progress;
    public double Progress
    {
        get => progress;
        set => SetProperty(ref progress, value);
    }

    private string versionInfo = "Version 1.0.0 • Build 2024.09.20";
    public string VersionInfo
    {
        get => versionInfo;
        set => SetProperty(ref versionInfo, value);
    }

    private string systemInfo = BuildSystemInfo();
    public string SystemInfo
    {
        get => systemInfo;
        set => SetProperty(ref systemInfo, value);
    }

    private string copyrightText = "© 2024 Wiley Widget Corporation. All rights reserved.";
    public string CopyrightText
    {
        get => copyrightText;
        set => SetProperty(ref copyrightText, value);
    }

    /// <summary>
    /// Refreshes the system information banner with the most recent runtime details.
    /// </summary>
    public void RefreshSystemInfo()
    {
        SystemInfo = BuildSystemInfo();
    }

    private static string BuildSystemInfo()
    {
        try
        {
            var osVersion = Environment.OSVersion;
            var framework = Environment.Version;
            var architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
            return $".NET {framework} • Windows {osVersion.Version.Major}.{osVersion.Version.Minor} • {architecture}";
        }
        catch
        {
            return ".NET 9.0 • Windows 11 • Enterprise Edition";
        }
    }
}
