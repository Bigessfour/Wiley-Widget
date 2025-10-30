using System.Threading.Tasks;
using Moq;
using Xunit;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using WileyWidget.Configuration;

namespace WileyWidget.Tests
{
    public class SettingsViewModelTests
    {
        private SettingsViewModel CreateViewModel(
            out Mock<IAIService> aiMock,
            out Mock<ISecretVaultService> vaultMock,
            out Mock<IAuditService> auditMock)
        {
            aiMock = new Mock<IAIService>();
            vaultMock = new Mock<ISecretVaultService>();
            auditMock = new Mock<IAuditService>();

            var logger = new NullLogger<SettingsViewModel>();
            var appOptions = Options.Create(new AppOptions());
            var appOptionsMonitor = new Mock<IOptionsMonitor<AppOptions>>();
            appOptionsMonitor.Setup(m => m.CurrentValue).Returns(appOptions.Value);

            var unitOfWorkMock = new Mock<WileyWidget.Business.Interfaces.IUnitOfWork>();
            var dbMock = new Mock<WileyWidget.Data.AppDbContext>();
            var quickBooksMock = new Mock<WileyWidget.Services.IQuickBooksService>();
            var syncfusionMock = new Mock<WileyWidget.Services.ISyncfusionLicenseService>();
            var settingsServiceMock = new Mock<WileyWidget.Services.ISettingsService>();
            var dialogServiceMock = new Mock<Prism.Dialogs.IDialogService>();

            var vm = new SettingsViewModel(
                logger,
                appOptions,
                appOptionsMonitor.Object,
                unitOfWorkMock.Object,
                dbMock.Object,
                vaultMock.Object,
                quickBooksMock.Object,
                syncfusionMock.Object,
                aiMock.Object,
                auditMock.Object,
                settingsServiceMock.Object,
                dialogServiceMock.Object
            );

            return vm;
        }

        [Fact]
        public async Task ValidateAndSave_Calls_Audit_On_Success()
        {
            var vm = CreateViewModel(out var aiMock, out var vaultMock, out var auditMock);

            var testKey = "TEST_XAI_VALID_KEY";
            aiMock.Setup(x => x.ValidateApiKeyAsync(testKey)).ReturnsAsync(new WileyWidget.Services.AIResponseResult(200, "OK", null));
            vaultMock.Setup(v => v.RotateSecretAsync("XAI-ApiKey", testKey)).Returns(Task.CompletedTask);
            aiMock.Setup(x => x.UpdateApiKeyAsync(testKey)).Returns(Task.CompletedTask);
            auditMock.Setup(a => a.AuditAsync(It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask).Verifiable();

            await vm.ValidateAndSaveXaiKeyAsyncPublic(testKey);

            // Assert VM state
            Assert.True(vm.IsXaiKeyValidated);
            Assert.Equal(string.Empty, vm.XaiApiKey);

            auditMock.Verify(a => a.AuditAsync(It.Is<string>(s => s.Contains("XAI.KeyRotated") || s.Contains("XAI.KeyRotated")), It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Validate_Failure_Audited()
        {
            var vm = CreateViewModel(out var aiMock, out var vaultMock, out var auditMock);
            var testKey = "TEST_XAI_BAD_KEY";
            aiMock.Setup(x => x.ValidateApiKeyAsync(testKey)).ReturnsAsync(new WileyWidget.Services.AIResponseResult(401, "Unauthorized", "AuthFailure"));
            auditMock.Setup(a => a.AuditAsync(It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask).Verifiable();

            await vm.ValidateXaiKeyAsyncPublic(testKey);

            Assert.False(vm.IsXaiKeyValidated);
            Assert.Contains("invalid", vm.GetErrors(nameof(vm.XaiApiKey)).Cast<string>().FirstOrDefault() ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);

            auditMock.Verify(a => a.AuditAsync(It.Is<string>(s => s.Contains("XAI.KeyValidationFailed") || s.Contains("XAI.KeyValidationFailed")), It.IsAny<object>()), Times.AtLeastOnce);
        }
    }
}
