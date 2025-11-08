using System;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.ViewModels.Main;
using WileyWidget.Services;
namespace WileyWidget.Tests
{
    public class SettingsViewModelTests
    {
        private SettingsViewModel CreateViewModel()
        {
            var logger = new NullLogger<SettingsViewModel>();
            var quickBooksMock = new Mock<IQuickBooksService>();
            var settingsServiceMock = new Mock<ISettingsService>();

            var vm = new SettingsViewModel(
                logger,
                new Lazy<IQuickBooksService>(() => quickBooksMock.Object),
                new Lazy<ISettingsService>(() => settingsServiceMock.Object)
            );

            return vm;
        }

    [Fact]
    public async Task ValidateAndSave_SetsValidated_On_Success()
    {
        var vm = CreateViewModel();
        var testKey = "TEST_XAI_VALID_KEY";
        await vm.ValidateAndSaveXaiKeyAsyncPublic(testKey);
        // Debug: check the XaiApiKey value
        Assert.Equal("TEST_XAI_VALID_KEY", vm.XaiApiKey);
        Assert.True(vm.IsXaiKeyValidated);
    }        [Fact]
        public async Task Validate_Failure_Sets_NotValidated()
        {
            var vm = CreateViewModel();
            var testKey = ""; // simulate invalid key
            await vm.ValidateXaiKeyAsyncPublic(testKey);
            Assert.False(vm.IsXaiKeyValidated);
        }
    }
}
