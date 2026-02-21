using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace WileyWidget.WinForms.Tests.Infrastructure;

/// <summary>
/// Base class for tests that hit the real xAI Grok API.
/// Automatically skips tests if XAI__ApiKey environment variable is not set.
/// Tracks token usage to prevent excessive costs.
/// </summary>
public abstract class RealApiTestBase : IDisposable
{
    protected ITestOutputHelper Output { get; }
    protected IConfiguration Configuration { get; }
    protected string? ApiKey { get; }
    protected bool IsRealApiAvailable { get; }

    private static int _totalTokensUsed;
    private const int MaxTokenBudget = 50000; // Maximum tokens for entire test suite
    private bool _disposed;

    protected RealApiTestBase(ITestOutputHelper output)
    {
        Output = output;

        // Build configuration from environment variables and user secrets
        Configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<RealApiTestBase>()
            .Build();

        // Check for API key (double underscore takes precedence per Microsoft conventions)
        ApiKey = Configuration["XAI:ApiKey"]
                 ?? Environment.GetEnvironmentVariable("XAI__ApiKey")
                 ?? Environment.GetEnvironmentVariable("XAI_API_KEY");

        IsRealApiAvailable = !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.Contains("YOUR_");

        if (!IsRealApiAvailable)
        {
            Output.WriteLine("⚠️ Real API tests skipped - No valid XAI__ApiKey found in environment or user secrets.");
            Output.WriteLine("   To enable: dotnet user-secrets set \"XAI:ApiKey\" \"YOUR_KEY_HERE\"");
            Output.WriteLine("   Or: setx XAI__ApiKey \"YOUR_KEY_HERE\"");
        }
        else
        {
            Output.WriteLine($"✅ Real API key detected: {MaskApiKey(ApiKey)}");
            Output.WriteLine($"📊 Current token usage: {_totalTokensUsed}/{MaxTokenBudget}");
        }
    }

    /// <summary>
    /// Skip test if real API is not available (no API key configured).
    /// </summary>
    protected void SkipIfRealApiNotAvailable()
    {
        if (!IsRealApiAvailable)
        {
            Output.WriteLine("SKIP: Real xAI API key not configured. Set XAI__ApiKey environment variable or user secret.");
            Assert.Fail("SKIPPED: Real xAI API key not configured");
        }
    }

    /// <summary>
    /// Skip test if token budget has been exceeded.
    /// </summary>
    protected void SkipIfBudgetExceeded()
    {
        if (_totalTokensUsed >= MaxTokenBudget)
        {
            Output.WriteLine($"SKIP: Token budget exceeded: {_totalTokensUsed}/{MaxTokenBudget} tokens used");
            Assert.Fail($"SKIPPED: Token budget exceeded: {_totalTokensUsed}/{MaxTokenBudget} tokens used");
        }
    }

    /// <summary>
    /// Records token usage from a test (thread-safe).
    /// </summary>
    protected void RecordTokenUsage(int tokens)
    {
        Interlocked.Add(ref _totalTokensUsed, tokens);
        Output.WriteLine($"💰 Tokens used in this test: {tokens} (Total: {_totalTokensUsed}/{MaxTokenBudget})");

        if (_totalTokensUsed > MaxTokenBudget * 0.8)
        {
            Output.WriteLine($"⚠️ WARNING: 80% of token budget consumed ({_totalTokensUsed}/{MaxTokenBudget})");
        }
    }

    /// <summary>
    /// Masks an API key for logging (shows first 4 and last 4 chars).
    /// </summary>
    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 12)
        {
            return "***";
        }

        return $"{apiKey[..4]}***{apiKey[^4..]}";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing && Configuration is IDisposable disposableConfiguration)
        {
            disposableConfiguration.Dispose();
        }

        _disposed = true;
    }
}
