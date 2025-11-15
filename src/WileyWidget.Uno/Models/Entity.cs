namespace WileyWidget.Uno.Models;

/// <summary>
/// Entity data model for navigation examples.
/// </summary>
public partial record Entity(string Name)
{
    /// <summary>
    /// Entity identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Entity description.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
