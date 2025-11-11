using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using WileyWidget.Services;

namespace WileyWidget.Tests.Data
{
    public class EncryptedLocalSecretVaultServiceTests
    {
        [Fact]
        public async Task MigrateSecretsFromEnvironment_CreatesVaultAndPassesDiagnostics()
        {
            var logger = NullLoggerFactory.Instance.CreateLogger<EncryptedLocalSecretVaultService>();
            using var svc = new EncryptedLocalSecretVaultService(logger);

            // Ensure we can call migration without exceptions
            await svc.MigrateSecretsFromEnvironmentAsync();

            var diag = await svc.GetDiagnosticsAsync();
            Assert.Contains("Vault Directory:", diag);

            // TestConnectionAsync will attempt an atomic store/read; guard for permission issues
            var canConnect = await svc.TestConnectionAsync();
            Assert.True(canConnect, "Secret vault should allow write/read in test environment");
        }
    }
}
