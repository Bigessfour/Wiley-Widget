using System;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Services
{
    internal interface IStartupProbeService
    {
        void RecordResolution();
    }

    internal sealed class StartupProbeService : IStartupProbeService
    {
        private static readonly ActivitySource ActivitySource = new("WileyWidget.Startup");
        private readonly ILogger<StartupProbeService> _logger;
        private readonly IHostEnvironment _environment;
        private readonly Guid _instanceId = Guid.NewGuid();

        public StartupProbeService(ILogger<StartupProbeService> logger, IHostEnvironment environment)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        public void RecordResolution()
        {
            using var activity = ActivitySource.StartActivity("DI.Resolve.StartupProbe");
            activity?.SetTag("service", nameof(StartupProbeService));
            activity?.SetTag("environment", _environment.EnvironmentName);
            activity?.SetTag("instanceId", _instanceId);

            _logger.LogInformation(
                "Startup probe resolved: {Service} in {Environment} (InstanceId={InstanceId})",
                nameof(StartupProbeService),
                _environment.EnvironmentName,
                _instanceId);
        }
    }
}
