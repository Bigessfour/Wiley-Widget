using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;

namespace WileyWidget.ViewModels;

/// <summary>
/// Demonstration view model providing an in-memory list of widgets and a command to cycle selection.
/// Serves as a template for future data-bound collections / CRUD patterns.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<Widget> Widgets { get; } = new()
    {
        new Widget { Id = 1, Name = "Alpha", Category = "Core", Price = 19.99 },
        new Widget { Id = 2, Name = "Beta", Category = "Core", Price = 24.50 },
        new Widget { Id = 3, Name = "Gamma", Category = "Extended", Price = 42.00 }
    };

    /// <summary>Currently selected widget in the grid (null when none selected).</summary>
    [ObservableProperty]
    private Widget selectedWidget;

    [RelayCommand]
    /// <summary>
    /// Cycles to the next widget (wrap-around). If none selected, selects the first. Safe for empty list.
    /// </summary>
    private void SelectNext()
    {
        if (Widgets.Count == 0)
            return;
        if (SelectedWidget == null)
        {
            SelectedWidget = Widgets[0];
            return;
        }
        var idx = Widgets.IndexOf(SelectedWidget);
        idx = (idx + 1) % Widgets.Count;
        SelectedWidget = Widgets[idx];
    }
}
