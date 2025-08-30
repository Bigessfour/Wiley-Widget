using System;

namespace WileyWidget.Models;

/// <summary>
/// Persisted user-facing settings. Contains only values that must survive restarts.
/// QBO (QuickBooks Online) tokens are stored to allow silent refresh on next launch.
/// Legacy QuickBooks* properties retained temporarily for migration; new canonical names use Qbo* prefix.
/// </summary>
public class AppSettings
{
    // Theme + window geometry
    public string Theme { get; set; } = "FluentDark";
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool? WindowMaximized { get; set; }

    // Legacy QuickBooks token/property names (kept for one migration cycle)
    public string QuickBooksAccessToken { get; set; }
    public string QuickBooksRefreshToken { get; set; }
    public string QuickBooksRealmId { get; set; }
    public string QuickBooksEnvironment { get; set; } = "sandbox"; // or "production"
    public DateTime? QuickBooksTokenExpiresUtc { get; set; }

    // Canonical QBO properties going forward
    public string QboAccessToken { get; set; }
    public string QboRefreshToken { get; set; }
    public DateTime QboTokenExpiry { get; set; } // UTC absolute expiry of access token

    // xAI API Configuration
    public string XaiApiKey { get; set; }
    public string XaiModel { get; set; } = "grok-4-0709";
    public int XaiMaxRetries { get; set; } = 3;
    public int XaiTimeoutSeconds { get; set; } = 30;
    public int XaiCacheTtlMinutes { get; set; } = 30;
    public decimal XaiDailyBudget { get; set; } = 10.00M;
    public decimal XaiMonthlyBudget { get; set; } = 300.00M;
    public bool XaiEnabled { get; set; } = true;
}
