using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Tests.Infrastructure;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    public class ChatPanelViewModelIntegrationTests
    {
        private static ServiceProvider BuildProvider(Dictionary<string, string?>? overrides = null)
        {
            var services = new ServiceCollection();

            var defaultConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:IsUiTestHarness"] = "false",
                    ["UI:UseSyncfusionDocking"] = "false",
                    ["UI:ShowRibbon"] = "true",
                    ["UI:ShowStatusBar"] = "true"
                })
                .Build();

            var configuration = overrides == null
                ? defaultConfig
                : new ConfigurationBuilder().AddInMemoryCollection(overrides).Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(builder => builder.AddDebug());

            // Register minimal services
            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");
            themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Callback<string>(theme =>
            {
                TestThemeHelper.EnsureOffice2019Colorful();
            });
            services.AddSingleton<IThemeService>(themeMock.Object);

            // Register ChatBridgeService and GrokAgentService
            services.AddSingleton<IChatBridgeService, ChatBridgeService>();
            services.AddSingleton<GrokAgentService>();
            services.AddSingleton<IGrokApiKeyProvider>(Mock.Of<IGrokApiKeyProvider>());

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        [StaFact]
        public async Task JARVISChat_PromptSubmission_ReceivesResponse()
        {
            // Arrange
            var provider = BuildProvider();
            var chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IChatBridgeService>(provider);
            var grokService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<GrokAgentService>(provider);

            // Mock or use real API key if configured
            var apiKeyProvider = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IGrokApiKeyProvider>(provider);
            await apiKeyProvider.ValidateAsync();  // Ensure valid key

            // Act
            await grokService.InitializeAsync();
            var response = await grokService.GetSimpleResponse("Are you connected?", ct: CancellationToken.None);

            // Assert
            Assert.NotNull(response);
            Assert.DoesNotContain("failed", response.ToLower(CultureInfo.InvariantCulture));  // Basic check for success
        }
    }
}
