using System;

namespace WileyWidget.Models;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class GridDisplayAttribute : Attribute
{
    public int DisplayOrder { get; set; }
    public int Width { get; set; }
    public bool Visible { get; set; } = true;
    public int DecimalDigits { get; set; } = -1;
    public string? Format { get; set; }
    
    public GridDisplayAttribute(int displayOrder, int width)
    {
        DisplayOrder = displayOrder;
        Width = width;
    }
}