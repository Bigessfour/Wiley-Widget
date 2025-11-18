using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a system activity item for display in the dashboard.
/// Tracks user actions, system events, and important operations.
/// </summary>
public class ActivityItem : INotifyPropertyChanged
{
    private DateTime _timestamp;
    private string _activity = string.Empty;
    private string _user = string.Empty;
    private string _icon = string.Empty;
    private string _category = string.Empty;
    private string _details = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the activity occurred.
    /// </summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set
        {
            if (_timestamp != value)
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the activity description.
    /// </summary>
    public string Activity
    {
        get => _activity;
        set
        {
            if (_activity != value)
            {
                _activity = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the user who performed the activity.
    /// </summary>
    public string User
    {
        get => _user;
        set
        {
            if (_user != value)
            {
                _user = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the icon identifier for displaying in the UI.
    /// Should correspond to Syncfusion icon names or custom icon paths.
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
    /// Gets or sets the category of the activity (e.g., "Budget", "Enterprise", "System").
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
    /// Gets or sets additional details about the activity.
    /// </summary>
    public string Details
    {
        get => _details;
        set
        {
            if (_details != value)
            {
                _details = value;
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
