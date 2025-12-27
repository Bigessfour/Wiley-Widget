using System;

namespace WileyWidget.Models;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
/// <summary>
/// Represents a class for griddisplayattribute.
/// </summary>
public class GridDisplayAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the displayorder.
    /// </summary>
    public int DisplayOrder { get; set; }
    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public int Width { get; set; }
    /// <summary>
    /// Gets or sets the visible.
    /// </summary>
    public bool Visible { get; set; } = true;
    /// <summary>
    /// Gets or sets the decimaldigits.
    /// </summary>
    public int DecimalDigits { get; set; } = -1;
    public string? Format { get; set; }

    public GridDisplayAttribute(int displayOrder, int width)
    {
        DisplayOrder = displayOrder;
        Width = width;
    }
}
