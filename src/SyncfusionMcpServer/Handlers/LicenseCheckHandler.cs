using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;
using SyncfusionMcpServer.Services;

namespace SyncfusionMcpServer.Handlers;

public class LicenseCheckHandler
{
    private readonly LicenseService _service;
    private readonly ILogger<LicenseCheckHandler> _logger;

    public LicenseCheckHandler(LicenseService service, ILogger<LicenseCheckHandler> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<LicenseValidationResult> HandleAsync(JsonElement arguments)
    {
        string? licenseKey = null;
        string? expectedVersion = null;

        if (arguments.TryGetProperty("licenseKey", out var lk))
        {
            licenseKey = lk.GetString();
        }

        if (arguments.TryGetProperty("expectedVersion", out var ev))
        {
            expectedVersion = ev.GetString();
        }

        _logger.LogInformation("Checking Syncfusion license");
        return await _service.ValidateLicenseAsync(licenseKey, expectedVersion);
    }
}
