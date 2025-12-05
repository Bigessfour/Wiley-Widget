using WileyWidget.Models.Dtos;

namespace WileyWidget.Services
{
    public interface IAppService
    {
        Task<AppDataDto> LoadAsync(CancellationToken ct = default);
    }

    public record AppDataDto(List<WidgetDto> Widgets, UserConfigDto Config);

    public record WidgetDto(string Id, string Name);
    public record UserConfigDto(string Theme, bool AutoSave);
}
