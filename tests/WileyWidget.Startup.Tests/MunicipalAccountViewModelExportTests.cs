using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Xunit;

namespace WileyWidget.Startup.Tests
{
    public class MunicipalAccountViewModelExportTests
    {
        private class FakeAccountRepository : IMunicipalAccountRepository
        {
            public Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber) => Task.FromResult<MunicipalAccount?>(null);
            public Task<IEnumerable<MunicipalAccount>> GetAllAsync() => Task.FromResult<IEnumerable<MunicipalAccount>>(Enumerable.Empty<MunicipalAccount>());
            public Task<MunicipalAccount?> GetByIdAsync(int id) => Task.FromResult<MunicipalAccount?>(null);
            public Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId) => Task.FromResult<IEnumerable<MunicipalAccount>>(Enumerable.Empty<MunicipalAccount>());
            public Task<MunicipalAccount> AddAsync(MunicipalAccount account) => Task.FromResult(account);
            public Task<MunicipalAccount> UpdateAsync(MunicipalAccount account) => Task.FromResult(account);
            public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
            public Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts) => Task.CompletedTask;
            public Task<object> GetBudgetAnalysisAsync(int periodId) => Task.FromResult<object>(new object());
            public Task<IEnumerable<MunicipalAccount>> GetByFundAsync(MunicipalFundType fund) => Task.FromResult<IEnumerable<MunicipalAccount>>(Enumerable.Empty<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type) => Task.FromResult<IEnumerable<MunicipalAccount>>(Enumerable.Empty<MunicipalAccount>());
        }

        private class FakeReportExportService : IReportExportService
        {
            public bool ExportCalled { get; private set; }
            public object? ExportedData { get; private set; }
            public string? ExportedPath { get; private set; }

            public Task ExportToPdfAsync(object data, string filePath) => Task.CompletedTask;
            public Task ExportToExcelAsync(object data, string filePath)
            {
                ExportCalled = true;
                ExportedData = data;
                ExportedPath = filePath;
                // Create the file to simulate real export
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
                File.WriteAllText(filePath, "export");
                return Task.CompletedTask;
            }
            public Task ExportToCsvAsync(IEnumerable<object> data, string filePath) => Task.CompletedTask;
            public Task ExportComplianceReportToPdfAsync(WileyWidget.Models.ComplianceReport report, string filePath) => Task.CompletedTask;
            public Task ExportComplianceReportToExcelAsync(WileyWidget.Models.ComplianceReport report, string filePath) => Task.CompletedTask;
            public IEnumerable<string> GetSupportedFormats() => new[] { "pdf", "xlsx" };
        }

        private class TestableMunicipalAccountViewModel : MunicipalAccountViewModel
        {
            private readonly string _pathToReturn;

            public TestableMunicipalAccountViewModel(IMunicipalAccountRepository repo, IReportExportService reportExportService, string pathToReturn)
                : base(repo, null, null, null, null, null, null, null, null, reportExportService, null)
            {
                _pathToReturn = pathToReturn;
            }

            protected override Task<string?> ShowSaveFileDialogAsync(string title, string filter, string defaultExt, string defaultFileName)
            {
                // Return the injected path to avoid UI interaction
                return Task.FromResult<string?>(_pathToReturn);
            }
        }

        [Fact]
        public async Task ExportToExcel_Fallback_UsesReportExportService_WhenGridNotAvailable()
        {
            // Arrange
            var tmp = Path.Combine(Path.GetTempPath(), $"municipal_export_test_{System.Guid.NewGuid():N}.xlsx");
            var repo = new FakeAccountRepository();
            var fakeExport = new FakeReportExportService();

            var vm = new TestableMunicipalAccountViewModel(repo, fakeExport, tmp);

            // Add a sample account into PagedAccounts so fallback has data
            vm.PagedAccounts.Add(new MunicipalAccount { Name = "Test", AccountNumber = new WileyWidget.Models.AccountNumber("100.1"), Balance = 123.45m });

            // Act
            await vm.ExportToExcel();

            // Assert
            Assert.True(fakeExport.ExportCalled, "Expected ExportToExcelAsync to be called on report export service");
            Assert.Equal(tmp, fakeExport.ExportedPath);
            Assert.NotNull(fakeExport.ExportedData);
            // Clean up
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
