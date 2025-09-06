namespace WileyWidget.Configuration;

/// <summary>
/// Configuration settings for QuickBooks integration.
/// </summary>
public class QuickBooksSettings
{
    /// <summary>
    /// Gets or sets the batch size for sync operations.
    /// </summary>
    public int SyncBatchSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed operations.
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the cache duration in minutes.
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the sliding expiration in minutes.
    /// </summary>
    public int SlidingExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the Grok integration settings.
    /// </summary>
    public GrokIntegrationSettings GrokIntegration { get; set; } = new();

    /// <summary>
    /// Gets or sets whether caching is enabled.
    /// </summary>
    public bool EnableCaching { get; set; } = true;
}

/// <summary>
/// Configuration settings for Grok integration.
/// </summary>
public class GrokIntegrationSettings
{
    /// <summary>
    /// Gets or sets whether Grok integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the analysis frequency.
    /// </summary>
    public string AnalysisFrequency { get; set; } = "RealTime";

    /// <summary>
    /// Gets or sets the prediction horizon in months.
    /// </summary>
    public int PredictionHorizonMonths { get; set; } = 12;

    /// <summary>
    /// Gets or sets the confidence threshold for predictions.
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.75;
}
