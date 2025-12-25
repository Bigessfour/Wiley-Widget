using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Integration.Tests.Shared;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Services;
using WileyWidget.Services.Excel;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WileyWidget.Integration.Tests.Services
{
    public class BudgetImporterTests : IntegrationTestBase
    {
        public BudgetImporterTests() : base()
        {
        }

        [Fact]
        public async Task ValidateImportFileAsync_ReturnsError_ForUnsupportedExtension()
        {
            // Arrange
            var importer = new BudgetImporter(Mock.Of<IExcelReaderService>(), NullLogger<BudgetImporter>.Instance, GetRequiredService<IBudgetRepository>());
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
            File.WriteAllText(tempFile, "dummy");

            try
            {
                // Act
                var errors = await importer.ValidateImportFileAsync(tempFile);

                // Assert
                errors.Should().Contain(e => e.Contains("Unsupported file extension"));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportBudgetAsync_ValidExcel_ReturnsEnrichedEntries_AndPersistable()
        {
            // Arrange
            await ResetDatabaseAsync();
            var dbRepo = GetRequiredService<IBudgetRepository>();

            var mockExcel = new Mock<IExcelReaderService>();
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xlsx");
            File.WriteAllText(filePath, "dummy");

            var entry = new BudgetEntry { AccountNumber = "500", Description = "Imported", FiscalYear = 2099, BudgetedAmount = 100m };
            mockExcel.Setup(x => x.ValidateExcelStructureAsync(filePath)).ReturnsAsync(true);
            mockExcel.Setup(x => x.ReadBudgetDataAsync(filePath)).ReturnsAsync(new[] { entry });

            var importer = new BudgetImporter(mockExcel.Object, NullLogger<BudgetImporter>.Instance, dbRepo);

            try
            {
                // Act
                var result = (await importer.ImportBudgetAsync(filePath)).ToList();

                // Assert
                result.Should().ContainSingle();
                var returned = result.Single();
                returned.StartPeriod.Year.Should().Be(2099);
                returned.EndPeriod.Year.Should().Be(2099);
                returned.SourceFilePath.Should().Be(filePath);
                returned.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

                // Persist - ensure a department and fund exist for FK constraints
                var dept = new Department { Name = "Importer Dept" };
                var fundRec = new Fund { FundCode = "900-IMP", Name = "Importer Fund", Type = FundType.GeneralFund };
                using (var scope = CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    ctx.Departments.Add(dept);
                    ctx.Funds.Add(fundRec);
                    await ctx.SaveChangesAsync();
                }

                foreach (var e in result)
                {
                    e.DepartmentId = dept.Id;
                    e.FundId = fundRec.Id;
                    await dbRepo.AddAsync(e);
                }

                var persisted = (await dbRepo.GetByFiscalYearAsync(2099)).ToList();
                persisted.Should().ContainSingle(b => b.AccountNumber == "500");
            }
            finally
            {
                File.Delete(filePath);
            }
        }
    }
}
