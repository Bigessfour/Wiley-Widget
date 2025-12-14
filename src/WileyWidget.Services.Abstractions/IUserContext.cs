namespace WileyWidget.Services.Abstractions
{
    public interface IUserContext
    {
        string? UserId { get; }
        string? DisplayName { get; }
    }
}
