using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace WileyWidget.WinForms.Configuration;

internal sealed class AuthenticationOptionsPostConfigure : IPostConfigureOptions<AuthenticationOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public AuthenticationOptionsPostConfigure(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
    }

    public void PostConfigure(string? name, AuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var environmentOptions = options.Environment ?? new AuthenticationEnvironmentOptions();
        var forceDevelopmentBypass = IsTruthy(_configuration[environmentOptions.ForceDevelopmentBypassFlag]);
        var forceLocalIdentity = IsTruthy(_configuration[environmentOptions.ForceLocalIdentityFlag]);

        if (forceDevelopmentBypass)
        {
            options.Mode = environmentOptions.DevelopmentMode;
        }
        else if (forceLocalIdentity)
        {
            options.Mode = environmentOptions.NonDevelopmentMode;
        }
        else if (environmentOptions.EnableModeOverride)
        {
            options.Mode = _hostEnvironment.IsDevelopment()
                ? environmentOptions.DevelopmentMode
                : environmentOptions.NonDevelopmentMode;
        }

        if (options.LocalIdentity.RememberMeDurationDays < 1)
        {
            options.LocalIdentity.RememberMeDurationDays = 1;
        }
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }
}