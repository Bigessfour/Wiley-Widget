using System;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.IntegrationTests.TestDoubles;

/// <summary>
/// Null implementation of ITelemetryService for testing.
/// Prevents telemetry calls from failing during integration tests.
/// </summary>
public class NullTelemetryService : ITelemetryService
{
    public void RecordException(Exception exception, params (string key, object? value)[] additionalTags)
    {
        // No-op for test implementation
    }
}
