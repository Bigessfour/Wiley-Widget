using System.Net.Http;
using Polly;

namespace WileyWidget.Services.Resilience
{
    /// <summary>
    /// Factory for creating configured ResiliencePipeline instances for HTTP calls.
    /// Centralizes retry/circuit-breaker/timeout/rate-limit configuration so callers
    /// don't duplicate policies.
    /// </summary>
    public interface IResiliencePipelineFactory
    {
        ResiliencePipeline<HttpResponseMessage> CreateDefaultHttpPipeline(string clientName);
    }
}
