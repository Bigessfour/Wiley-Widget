using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Options;

namespace WileyWidget.Models;

/// <summary>
/// Custom validation for ConnectionStringsOptions
/// </summary>
public class ConnectionStringsOptionsValidator : IValidateOptions<ConnectionStringsOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectionStringsOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DefaultConnection))
        {
            failures.Add("DefaultConnection must be configured");
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Custom validation for QuickBooksOptions
/// </summary>
public class QuickBooksOptionsValidator : IValidateOptions<QuickBooksOptions>
{
    public ValidateOptionsResult Validate(string? name, QuickBooksOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var failures = new List<string>();

        // If any QuickBooks setting is configured, require all required settings
        var hasAnyQuickBooksSetting = !string.IsNullOrWhiteSpace(options.ClientId) ||
                                     !string.IsNullOrWhiteSpace(options.ClientSecret) ||
                                     !string.IsNullOrWhiteSpace(options.RedirectUri);

        if (hasAnyQuickBooksSetting)
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
                failures.Add("ClientId is required when QuickBooks integration is configured");

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
                failures.Add("ClientSecret is required when QuickBooks integration is configured");

            if (string.IsNullOrWhiteSpace(options.RedirectUri))
                failures.Add("RedirectUri is required when QuickBooks integration is configured");

            // Validate redirect URI is HTTPS in production
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                             "Development";

            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
                !options.RedirectUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("RedirectUri must use HTTPS in production environment");
            }
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Custom validation for SyncfusionOptions
/// </summary>
public class SyncfusionOptionsValidator : IValidateOptions<SyncfusionOptions>
{
    public ValidateOptionsResult Validate(string? name, SyncfusionOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var failures = new List<string>();

        // License key is always required for Syncfusion controls
        if (string.IsNullOrWhiteSpace(options.LicenseKey))
        {
            failures.Add("LicenseKey is required for Syncfusion controls. Configure via appsettings or environment variable SYNCFUSION_LICENSE_KEY");
        }
        else
        {
            // Basic validation that license key looks like a Syncfusion license
            if (!options.LicenseKey.Contains("@", StringComparison.Ordinal) || options.LicenseKey.Length < 50)
            {
                failures.Add("LicenseKey appears to be invalid. Please verify it's a valid Syncfusion license key");
            }
        }

        return failures.Any()
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
