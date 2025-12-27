namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Represents a interface for iusercontext.
    /// </summary>
    public interface IUserContext
    {
        string? UserId { get; }
        string? DisplayName { get; }
    }
}
