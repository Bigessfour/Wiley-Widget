using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace WileyWidget.Configuration;

/// <summary>
/// Health check for external API services (xAI Grok API)
/// </summary>
public class ExternalApiHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ExternalApiHealthCheck(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = _configuration["xAI:ApiKey"];
            var baseUrl = _configuration["xAI:BaseUrl"] ?? "https://api.x.ai/v1";

            if (string.IsNullOrEmpty(apiKey))
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("xAI API key not configured");
            }

            // Simple health check - try to reach the API endpoint
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("xAI API is responding");
            }
            else
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded($"xAI API returned {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("xAI API health check failed", ex);
        }
    }
}
