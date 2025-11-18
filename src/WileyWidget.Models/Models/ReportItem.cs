#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a report item for the reporting system
/// </summary>
public class ReportItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private string _name = string.Empty;
    private string _path = string.Empty;
    private string _description = string.Empty;
    private string _category = string.Empty;
    private bool _isSelected;

    /// <summary>
    /// Gets or sets the display name of the report
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the file path to the report definition (.rdl file)
    /// </summary>
    public string Path
    {
        get => _path;
        set
        {
            if (_path != value)
            {
                _path = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the description of the report
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the category of the report (e.g., "Budget", "Audit", "Financial")
    /// </summary>
    public string Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this report is currently selected
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the file name without extension
    /// </summary>
    public string FileName => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>
    /// Returns a string representation of the report item
    /// </summary>
    public override string ToString() => Name;
}