using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Forms
{
    public class MainFormDIResolutionTests
    {
        [Fact]
        public void MainFormAndAccountsForm_Resolve_WithoutShowing()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var sp = WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(config);

            // Ensure error dialogs are suppressed for headless tests
            var reporter = sp.GetService(typeof(WileyWidget.Services.ErrorReportingService)) as WileyWidget.Services.ErrorReportingService;
            if (reporter != null) reporter.SuppressUserDialogs = true;

            var mainForm = sp.GetService(typeof(WileyWidget.WinForms.Forms.MainForm));
            var accountsForm = sp.GetService(typeof(WileyWidget.WinForms.Forms.AccountsForm));

            Assert.NotNull(mainForm);
            Assert.NotNull(accountsForm);
        }
    }
}
