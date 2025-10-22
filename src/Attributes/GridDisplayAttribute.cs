using System;

namespace WileyWidget.Attributes;

/// <summary>
/// Attribute to specify display metadata for properties in data grids
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class GridDisplayAttribute : Attribute
{
    /// <summary>
    /// Display order (lower numbers appear first)
    /// </summary>
    public int Order { get; set; } = 50;

    /// <summary>
    /// Column width in pixels
    /// </summary>
    public double Width { get; set; } = 120;

    /// <summary>
    /// Custom header text (if null, property name will be converted from camelCase)
    /// </summary>
    public string? HeaderText { get; set; }

    /// <summary>
    /// Whether this property should be displayed in the grid
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Number of decimal places for numeric columns
    /// </summary>
    public int DecimalDigits { get; set; } = -1; // -1 means use default based on type

    /// <summary>
    /// Optional: Converter type to apply to the column's binding when generating dynamic columns.
    /// The type must implement IValueConverter and have a parameterless constructor.
    /// </summary>
    public Type? ConverterType { get; set; }

    /// <summary>
    /// Optional: Resource key for a converter declared in XAML resources. If provided, this will be
    /// resolved via FindResource and used when generating the column binding.
    /// </summary>
    public string? ConverterResourceKey { get; set; }

    public GridDisplayAttribute(int order = 50, double width = 120)
    {
        Order = order;
        Width = width;
    }
}
