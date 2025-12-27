using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedConservationAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // BudgetPeriod with Id=1 is expected to already exist in most environments.
            // If not, create it separately or switch to UseSeeding for runtime-safe insertion.

            // Ensure a default BudgetPeriod exists for fresh databases
            // Use raw SQL here because table naming/renames are applied earlier in the migration chain
            // and InsertData requires an entity mapping in the model at generation time. We also need
            // to enable IDENTITY_INSERT so we can set the Id explicitly when inserting.
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM [BudgetPeriod] WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [BudgetPeriod] ON;
    INSERT INTO [BudgetPeriod] (Id, [Year], [Name], CreatedDate, [Status], StartDate, EndDate, IsActive)
    VALUES (1, 2025, N'2025', '2025-01-01 00:00:00', 0, '2025-01-01', '2025-12-31', 1);
    SET IDENTITY_INSERT [BudgetPeriod] OFF;
END");

            migrationBuilder.InsertData(
                table: "MunicipalAccounts",
                columns: new[] { "Id", "AccountNumber_Value", "Balance", "BudgetAmount", "BudgetPeriodId", "DepartmentId", "Fund", "FundClass", "IsActive", "LastSyncDate", "Name", "Notes", "ParentAccountId", "QuickBooksId", "Type" },
                values: new object[,]
                {
                    { 1, "110", 0m, 0m, 1, 1, 8, 0, true, null, "CASH IN BANK", null, null, null, 0 },
                    { 2, "110.1", 0m, 0m, 1, 1, 8, 0, true, null, "CASH-BASEBALL FIELD PROJECT", null, null, null, 0 },
                    { 3, "120", 0m, 0m, 1, 1, 8, 0, true, null, "INVESTMENTS", null, null, null, 1 },
                    { 4, "130", 0m, 0m, 1, 1, 8, 0, true, null, "INTERGOVERNMENTAL RECEIVABLE", null, null, null, 2 },
                    { 5, "140", 0m, 0m, 1, 1, 8, 0, true, null, "GRANT RECEIVABLE", null, null, null, 2 },
                    { 6, "210", 0m, 0m, 1, 1, 8, 0, true, null, "ACCOUNTS PAYABLE", null, null, null, 6 },
                    { 7, "211", 0m, 0m, 1, 1, 8, 0, true, null, "BASEBALL FIELD PROJECT LOAN", null, null, null, 7 },
                    { 8, "212", 0m, 0m, 1, 1, 8, 0, true, null, "WALKING TRAIL LOAN", null, null, null, 7 },
                    { 9, "230", 0m, 0m, 1, 1, 8, 0, true, null, "DUE TO/FROM TOW GENERAL FUND", null, null, null, 8 },
                    { 10, "240", 0m, 0m, 1, 1, 8, 0, true, null, "DUE TO/FROM TOW UTILITY FUND", null, null, null, 8 },
                    { 11, "290", 0m, 0m, 1, 1, 8, 0, true, null, "FUND BALANCE", null, null, null, 10 },
                    { 12, "3000", 0m, 0m, 1, 1, 8, 0, true, null, "Opening Bal Equity", null, null, null, 9 },
                    { 13, "33000", 0m, 0m, 1, 1, 8, 0, true, null, "Retained Earnings", null, null, null, 9 },
                    { 14, "310", 0m, 0m, 1, 1, 8, 0, true, null, "STATE APPORTIONMENT", null, null, null, 16 },
                    { 15, "314", 0m, 0m, 1, 1, 8, 0, true, null, "WALKING TRAIL DONATION", null, null, null, 13 },
                    { 16, "315", 0m, 0m, 1, 1, 8, 0, true, null, "BASEBALL FIELD DONATIONS", null, null, null, 13 },
                    { 17, "320", 0m, 0m, 1, 1, 8, 0, true, null, "GRANT REVENUES", null, null, null, 13 },
                    { 18, "323", 0m, 0m, 1, 1, 8, 0, true, null, "MISC REVENUE", null, null, null, 16 },
                    { 19, "325", 0m, 0m, 1, 1, 8, 0, true, null, "WALKING TRAIL REVENUE", null, null, null, 16 },
                    { 20, "360", 0m, 0m, 1, 1, 8, 0, true, null, "INTEREST ON INVESTMENTS", null, null, null, 14 },
                    { 21, "370", 0m, 0m, 1, 1, 8, 0, true, null, "TRANSFER FROM REC FUND", null, null, null, 30 },
                    { 22, "2111", 0m, 0m, 1, 1, 8, 0, true, null, "BALLFIELD ACCRUED INTEREST", null, null, null, 24 },
                    { 23, "2112", 0m, 0m, 1, 1, 8, 0, true, null, "WALKING TRAIL ACCRUED INTEREST", null, null, null, 24 },
                    { 24, "410", 0m, 0m, 1, 1, 8, 0, true, null, "CAPITAL IMP - BALL COMPLEX", null, null, null, 29 },
                    { 25, "420", 0m, 0m, 1, 1, 8, 0, true, null, "PARKS - DEVELOPMENT", null, null, null, 29 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 25);
        }
    }
}
