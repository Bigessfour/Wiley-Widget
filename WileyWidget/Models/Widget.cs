namespace WileyWidget.Models;

/// <summary>
/// Simple domain model representing a product-like widget displayed in the main grid.
/// Acts as a placeholder until real domain entities are introduced.
/// </summary>
public class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Price { get; set; }
}
