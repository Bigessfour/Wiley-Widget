using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Syncfusion.SfSkinManager;

namespace WileyWidget.Controls;

/// <summary>
/// PolishHost - A modular ContentControl wrapper for hosting Syncfusion-themed content.
/// Provides centralized theme management and resource freezing for consistent mayoral dashboards.
/// Avoids Microsoft's "modular chunks" pitfalls by using a single, unified content host.
/// </summary>
public class PolishHost : ContentControl
{
    /// <summary>
    /// Identifies the Theme dependency property.
    /// </summary>
    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(
            nameof(Theme),
            typeof(string),
            typeof(PolishHost),
            new PropertyMetadata("FluentDark", OnThemeChanged));

    /// <summary>
    /// Gets or sets the Syncfusion theme to apply to this host and its content.
    /// </summary>
    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>
    /// Identifies the IsFrozen dependency property.
    /// </summary>
    public static readonly DependencyProperty IsFrozenProperty =
        DependencyProperty.Register(
            nameof(IsFrozen),
            typeof(bool),
            typeof(PolishHost),
            new PropertyMetadata(false, OnIsFrozenChanged));

    /// <summary>
    /// Identifies the CornerRadius dependency property.
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(PolishHost),
            new PropertyMetadata(new CornerRadius(0)));

    /// <summary>
    /// Gets or sets the corner radius for the host border.
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets whether resources should be frozen for performance.
    /// </summary>
    public bool IsFrozen
    {
        get => (bool)GetValue(IsFrozenProperty);
        set => SetValue(IsFrozenProperty, value);
    }

    static PolishHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PolishHost), new FrameworkPropertyMetadata(typeof(PolishHost)));
    }

    public PolishHost()
    {
        // Initialize with default theme
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup if needed
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PolishHost host)
        {
            host.ApplyTheme();
        }
    }

    private static void OnIsFrozenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PolishHost host && (bool)e.NewValue)
        {
            host.FreezeResources();
        }
    }

    /// <summary>
    /// Applies the current theme to this control and freezes resources if requested.
    /// </summary>
    private void ApplyTheme()
    {
        if (!string.IsNullOrEmpty(Theme))
        {
            try
            {
                // Theme is managed globally via SfSkinManager.ApplicationTheme
                // Individual control theming is discouraged - use global theme instead
                // Keeping this for backward compatibility but it should inherit from ApplicationTheme
                Serilog.Log.Debug("PolishHost theme property set to {Theme}, but global ApplicationTheme is preferred", Theme);

                // Freeze resources for performance if requested
                if (IsFrozen)
                {
                    FreezeResources();
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash - theme application is non-critical
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme '{Theme}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Freezes theme resources for improved performance.
    /// </summary>
    private void FreezeResources()
    {
        try
        {
            // Freeze the control's resources to prevent dynamic changes
            if (Resources != null)
            {
                foreach (var resourceKey in Resources.Keys)
                {
                    if (Resources[resourceKey] is Freezable freezable && !freezable.IsFrozen)
                    {
                        freezable.Freeze();
                    }
                }
            }

            // Also freeze template resources if they exist
            if (Template != null && Template.Resources != null)
            {
                foreach (var resourceKey in Template.Resources.Keys)
                {
                    if (Template.Resources[resourceKey] is Freezable freezable && !freezable.IsFrozen)
                    {
                        freezable.Freeze();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Freezing is optional - log but continue
            System.Diagnostics.Debug.WriteLine($"Failed to freeze resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Switches the content while maintaining theme consistency.
    /// </summary>
    public void SwitchContent(object newContent)
    {
        Content = newContent;
        ApplyTheme(); // Re-apply theme to new content
    }
}
