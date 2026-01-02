using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Abstract base class for navigation panels that extend ScopedPanelBase.
/// Provides accessibility support for UI automation testing.
/// Theme is inherited automatically from parent form via SfSkinManager cascade.
/// </summary>
/// <typeparam name="TViewModel">The ViewModel type to resolve from the scoped service provider.</typeparam>
public abstract class NavigationPanelBase<TViewModel> : ScopedPanelBase<TViewModel>
    where TViewModel : class
{
    protected NavigationPanelBase(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<TViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();
        // Theme inherited from parent form - no manual theme application needed
        this.Load += NavigationPanelBase_Load;
    }

    /// <summary>
    /// Override to provide panel-specific AccessibleName for UI automation.
    /// </summary>
    protected abstract string AccessiblePanelName { get; }

    /// <summary>
    /// Required method for Designer support - must be provided by derived classes.
    /// </summary>
    protected abstract void InitializeComponent();

    private void NavigationPanelBase_Load(object? sender, EventArgs e)
    {
        this.AccessibleName = AccessiblePanelName;
        this.AccessibleRole = AccessibleRole.Pane;
    }
}
