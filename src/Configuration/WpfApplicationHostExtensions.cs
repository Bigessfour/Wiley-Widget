using System;
using Microsoft.Extensions.Hosting;
using WileyWidget.Configuration;

namespace WileyWidget;

/// <summary>
/// Backwards-compatible extension wrapper that forwards to the consolidated configuration in <see cref="Configuration.WpfHostingExtensions"/>.
/// Deprecated: prefer calling <see cref="Configuration.WpfHostingExtensions.ConfigureWpfApplication"/> directly.
/// </summary>
[Obsolete("Use Configuration.WpfHostingExtensions.ConfigureWpfApplication(builder) instead. This shim will be removed in a future release.")]
public static class WpfApplicationHostExtensions
{
    public static IHostApplicationBuilder ConfigureWpfApplication(this IHostApplicationBuilder builder)
    {
        return Configuration.WpfHostingExtensions.ConfigureWpfApplication(builder);
    }
}
