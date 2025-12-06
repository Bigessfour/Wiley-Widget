using AutoMapper;
using Microsoft.Extensions.Logging;
using WileyWidget.Data.Repositories; // Assume repo
using WileyWidget.Services;

namespace WileyWidget.Services
{
    public class AppService : IAppService
    {
        private readonly IWidgetRepository _repo;
        private readonly IMapper _mapper;
        private readonly ILogger<AppService> _logger;

        public AppService(IWidgetRepository repo, IMapper mapper, ILogger<AppService> logger)
        {
            _repo = repo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<AppDataDto> LoadAsync(CancellationToken ct = default)
        {
            using (new PerformanceLogger(_logger, "AppService.LoadAsync", 200))
            {
                _logger.LogInformation("Loading app data – widgets and config incoming.");
                var entities = await _repo.GetWidgetsAsync(ct); // Stub repo call
                var widgets = _mapper.Map<List<WidgetDto>>(entities); // Mapping

                var config = new UserConfigDto("FluentDark", true); // Stub

                _logger.LogInformation("Loaded {WidgetCount} widgets successfully", widgets.Count);

                return new AppDataDto(widgets, config); // Record
            }
        }
    }
}
