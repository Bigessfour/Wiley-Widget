using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging;
using WileyWidget.Data.Repositories;
using WileyWidget.Services;

namespace WileyWidget.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ISettingsRepository _repo;
        private readonly IMapper _mapper;
        private readonly IValidator<SettingsDto> _validator;
        private readonly ILogger<SettingsService> _logger;

        public SettingsService(ISettingsRepository repo, IMapper mapper, IValidator<SettingsDto> validator, ILogger<SettingsService> logger)
        {
            _repo = repo;
            _mapper = mapper;
            _validator = validator;
            _logger = logger;
        }

        public async Task<SettingsDto> LoadAsync(CancellationToken ct = default)
        {
            var entity = await _repo.GetAsync(ct); // Stub
            return _mapper.Map<SettingsDto>(entity ?? new SettingsEntity { Theme = "FluentDark" });
        }

        public async Task SaveAsync(SettingsDto settings, CancellationToken ct = default)
        {
            var result = await _validator.ValidateAsync(settings, ct);
            if (!result.IsValid)
            {
                var errors = result.Errors.Select(e => e.ErrorMessage).ToList(); // LINQ
                _logger.LogWarning("Validation failed: {Errors}", errors);
                throw new ValidationException(result.Errors);
            }

            var entity = _mapper.Map<SettingsEntity>(settings);
            await _repo.SaveAsync(entity, ct);
            _logger.LogInformation("Settings saved – {Theme} mode activated.", settings.Theme);
        }
    }
}
