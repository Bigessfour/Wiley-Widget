using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a system alert for display in the dashboard.
/// Used for notifications, warnings, and error messages.
/// </summary>
public class AlertItem : INotifyPropertyChanged
{
    private string _severity = "Info";
    private string _message = string.Empty;
    private DateTime _timestamp;
    private bool _isDismissed;
    private string _source = string.Empty;
    private string _actionUrl = string.Empty;
    private int _id;

    /// <summary>
    /// Gets or sets the unique identifier for this alert.
    /// </summary>
    public int Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the severity level of the alert.
    /// Valid values: "Info", "Warning", "Error", "Critical".
    /// </summary>
    public string Severity
    {
        get => _severity;
        set
        {
            if (_severity != value)
            {
                _severity = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the alert message.
    /// </summary>
    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the timestamp when the alert was generated.
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
    /// Gets or sets whether the alert has been dismissed by the user.
    /// </summary>
    public bool IsDismissed
    {
        get => _isDismissed;
        set
        {
            if (_isDismissed != value)
            {
                _isDismissed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the source of the alert (e.g., "Budget System", "Database", "QuickBooks").
    /// </summary>
    public string Source
    {
        get => _source;
        set
        {
            if (_source != value)
            {
                _source = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets an optional action URL for navigation when the alert is clicked.
    /// </summary>
    public string ActionUrl
    {
        get => _actionUrl;
        set
        {
            if (_actionUrl != value)
            {
                _actionUrl = value;
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
