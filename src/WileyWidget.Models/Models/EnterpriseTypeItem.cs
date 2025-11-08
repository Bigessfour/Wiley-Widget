using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents enterprise type statistics for dashboard visualization.
/// Used in DashboardViewModel for displaying enterprise distribution by type.
/// </summary>
public class EnterpriseTypeItem : INotifyPropertyChanged
{
    private string _type = string.Empty;
    private int _count;
    private decimal _totalBudget;
    private decimal _totalRevenue;
    private decimal _averageRate;
    private string _color = string.Empty;

    /// <summary>
    /// Gets or sets the enterprise type (e.g., "Water", "Sewer", "Electric", "Sanitation").
    /// </summary>
    public string Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the count of enterprises of this type.
    /// </summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Percentage));
            }
        }
    }

    /// <summary>
    /// Gets or sets the total budget for all enterprises of this type.
    /// </summary>
    public decimal TotalBudget
    {
        get => _totalBudget;
        set
        {
            if (_totalBudget != value)
            {
                _totalBudget = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the total revenue for all enterprises of this type.
    /// </summary>
    public decimal TotalRevenue
    {
        get => _totalRevenue;
        set
        {
            if (_totalRevenue != value)
            {
                _totalRevenue = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the average rate for enterprises of this type.
    /// </summary>
    public decimal AverageRate
    {
        get => _averageRate;
        set
        {
            if (_averageRate != value)
            {
                _averageRate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the color for chart visualization.
    /// </summary>
    public string Color
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the percentage of total enterprises (calculated property).
    /// This requires the total count to be set externally.
    /// </summary>
    public double Percentage { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
