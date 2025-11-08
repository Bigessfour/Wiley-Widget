using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a dashboard panel item for tile-based layouts.
/// Used with Syncfusion SfTileView for dashboard panels.
/// </summary>
public class PanelItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _content = string.Empty;
    private string _icon = string.Empty;
    private string _backgroundColor = "#2196F3";
    private int _rowSpan = 1;
    private int _columnSpan = 1;
    private bool _isMaximized;
    private object? _viewModel;
    private Type? _viewType;

    /// <summary>
    /// Gets or sets the panel title.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the panel content text.
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the icon identifier for the panel.
    /// </summary>
    public string Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the background color for the panel.
    /// </summary>
    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor != value)
            {
                _backgroundColor = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the row span for grid layout.
    /// </summary>
    public int RowSpan
    {
        get => _rowSpan;
        set
        {
            if (_rowSpan != value)
            {
                _rowSpan = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the column span for grid layout.
    /// </summary>
    public int ColumnSpan
    {
        get => _columnSpan;
        set
        {
            if (_columnSpan != value)
            {
                _columnSpan = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the panel is maximized.
    /// </summary>
    public bool IsMaximized
    {
        get => _isMaximized;
        set
        {
            if (_isMaximized != value)
            {
                _isMaximized = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the ViewModel associated with this panel.
    /// </summary>
    public object? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel != value)
            {
                _viewModel = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the View type to display in this panel.
    /// </summary>
    public Type? ViewType
    {
        get => _viewType;
        set
        {
            if (_viewType != value)
            {
                _viewType = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
