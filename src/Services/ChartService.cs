making using AutoMapper;
using Microsoft.Extensions.Logging;
using WileyWidget.Data.Repositories;
using WileyWidget.Services;

namespace WileyWidget.Services
{
    public class ChartService : IChartService
    {
        private readonly IBudgetRepository _repo;
        private readonly IMapper _mapper;
        private readonly ILogger<ChartService> _logger;

        public ChartService(IBudgetRepository repo, IMapper mapper, ILogger<ChartService> logger)
        {
            _repo = repo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<ChartSeriesDto>> GetChartDataAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            using (new PerformanceLogger(_logger, "GetChartData", 150))
            {
                if (from > to) throw new ArgumentException("From date must be before to date.", nameof(from)); // Validation

                var budgets = await _repo.GetAllAsync(ct); // Stub repo
                var filtered = budgets.Where(b => (!from.HasValue || b.Date >= from) && (!to.HasValue || b.Date <= to)); // LINQ filter

                var series = _mapper.Map<IEnumerable<ChartSeriesDto>>(filtered);
                _logger.LogDebug("Chart data filtered: {Count} series from {From} to {To}",
                    series.Count(), from?.ToShortDateString() ?? "start", to?.ToShortDateString() ?? "end");

                return series; // Stub data: 3 series
            }
        }
    }
}
