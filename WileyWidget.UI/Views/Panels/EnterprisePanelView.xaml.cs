using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;
using BusinessInterfaces = WileyWidget.Business.Interfaces;

namespace WileyWidget.Views.Panels;

/// <summary>
/// Enterprise Management panel view for embedding in docking layout
/// Prism auto-wires EnterpriseViewModel via ViewModelLocator.AutoWireViewModel
/// </summary>
public partial class EnterprisePanelView : UserControl
{
    private bool _loadedOnce;
    public EnterprisePanelView()
    {
        InitializeComponent();
        EnsureNamedElementsAreDiscoverable();

        // Prism ViewModelLocator automatically wires the ViewModel

        // Load enterprises when control loads
        Loaded += async (s, e) =>
        {
            if (_loadedOnce) return;
            _loadedOnce = true;
            if (DataContext is EnterpriseViewModel vm)
            {
                await vm.LoadEnterprisesAsync();
            }
        };
    }

    private T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T frameworkElement && frameworkElement.Name == name)
            {
                return frameworkElement;
            }

            var result = FindVisualChildByName<T>(child, name);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void EnsureNamedElementsAreDiscoverable()
    {
        RegisterNameIfMissing("EnterpriseTreeGrid", base.FindName("EnterpriseTreeGrid") as FrameworkElement);
        RegisterNameIfMissing("SearchTextBox", base.FindName("SearchTextBox") as FrameworkElement);
        RegisterNameIfMissing("StatusFilterCombo", base.FindName("StatusFilterCombo") as FrameworkElement);
        RegisterNameIfMissing("dataPager", base.FindName("dataPager") as FrameworkElement);
    }

    private void RegisterNameIfMissing(string name, FrameworkElement? element)
    {
        if (element is null || base.FindName(name) is not null)
        {
            return;
        }

        if (NameScope.GetNameScope(this) is not NameScope scope)
        {
            scope = new NameScope();
            NameScope.SetNameScope(this, scope);
        }

        if (scope.FindName(name) is null)
        {
            scope.RegisterName(name, element);
        }
    }

    public new object? FindName(string name)
    {
        return base.FindName(name) ?? TryResolveField(name) ?? TryFindInVisualTree(name);
    }

    private object? TryResolveField(string name)
    {
        var field = GetType().GetField(
            name,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.IgnoreCase);

        return field?.GetValue(this);
    }

    private FrameworkElement? TryFindInVisualTree(string name)
    {
        return FindVisualChildByName<FrameworkElement>(this, name);
    }
}
