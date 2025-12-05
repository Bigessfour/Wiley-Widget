using WileyWidget.Models.Dtos;

namespace WileyWidget.Services
{
    public interface IChartService
    {
        Task<IEnumerable<ChartSeriesDto>> GetChartDataAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    }

    public record ChartSeriesDto(DateTime Date, decimal Value);
}
