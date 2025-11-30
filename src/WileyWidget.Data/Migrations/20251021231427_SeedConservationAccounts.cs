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

            // Ensure a BudgetPeriod row exists (Id=1) so the MunicipalAccounts seed's FK references succeed.
            // Use raw SQL and check both plural/singular table names (BudgetPeriods/BudgetPeriod) so this works
            // regardless of previous naming in historical databases.
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[BudgetPeriods]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [BudgetPeriods] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [BudgetPeriods] ON;
        INSERT INTO [BudgetPeriods] ([Id], [Year], [Name], [CreatedDate], [Status], [StartDate], [EndDate], [IsActive])
        VALUES (1, 2026, N'2026', '2025-10-21T23:14:27.0000000Z', 2, '2026-01-01T00:00:00.0000000', '2026-12-31T00:00:00.0000000', CAST(1 AS bit));
        SET IDENTITY_INSERT [BudgetPeriods] OFF;
    END
END
ELSE IF OBJECT_ID(N'[BudgetPeriod]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [BudgetPeriod] WHERE [Id] = 1)
    BEGIN
        SET IDENTITY_INSERT [BudgetPeriod] ON;
        INSERT INTO [BudgetPeriod] ([Id], [Year], [Name], [CreatedDate], [Status], [StartDate], [EndDate], [IsActive])
        VALUES (1, 2026, N'2026', '2025-10-21T23:14:27.0000000Z', 2, '2026-01-01T00:00:00.0000000', '2026-12-31T00:00:00.0000000', CAST(1 AS bit));
        SET IDENTITY_INSERT [BudgetPeriod] OFF;
    END
END");

            migrationBuilder.InsertData(
                table: "MunicipalAccounts",
                // MunicipalAccount uses an owned AccountNumber mapped to a computed / backing column.
                // The model now uses `AccountNumber_Value` for the persisted/computed value column —
                // update the seed to insert into the correct backing column so migrations don't fail
                // when the 'AccountNumber' column isn't present in the target schema.
                columns: new[] { "Id", "AccountNumber_Value", "Balance", "BudgetAmount", "BudgetPeriodId", "DepartmentId", "Fund", "FundClass", "IsActive", "LastSyncDate", "Name", "Notes", "ParentAccountId", "QuickBooksId", "Type" },
                values: new object[,]
                {
                    // FundClass is non-nullable at this point in the migration history (AddBackendEnhancements)
                    // supply the computed fund class integer so the seed can insert successfully.
                    // Most of these are ConservationTrust (Fund=8) which maps to FundClass = Fiduciary (2).
                    { 1, "110", 0m, 0m, 1, 1, 8, 2, true, null, "CASH IN BANK", null, null, null, 0 },
                    { 2, "110.1", 0m, 0m, 1, 1, 8, 2, true, null, "CASH-BASEBALL FIELD PROJECT", null, null, null, 0 },
                    { 3, "120", 0m, 0m, 1, 1, 8, 2, true, null, "INVESTMENTS", null, null, null, 1 },
                    { 4, "130", 0m, 0m, 1, 1, 8, 2, true, null, "INTERGOVERNMENTAL RECEIVABLE", null, null, null, 2 },
                    { 5, "140", 0m, 0m, 1, 1, 8, 2, true, null, "GRANT RECEIVABLE", null, null, null, 2 },
                    { 6, "210", 0m, 0m, 1, 1, 8, 2, true, null, "ACCOUNTS PAYABLE", null, null, null, 6 },
                    { 7, "211", 0m, 0m, 1, 1, 8, 2, true, null, "BASEBALL FIELD PROJECT LOAN", null, null, null, 7 },
                    { 8, "212", 0m, 0m, 1, 1, 8, 2, true, null, "WALKING TRAIL LOAN", null, null, null, 7 },
                    { 9, "230", 0m, 0m, 1, 1, 8, 2, true, null, "DUE TO/FROM TOW GENERAL FUND", null, null, null, 8 },
                    { 10, "240", 0m, 0m, 1, 1, 8, 2, true, null, "DUE TO/FROM TOW UTILITY FUND", null, null, null, 8 },
                    { 11, "290", 0m, 0m, 1, 1, 8, 2, true, null, "FUND BALANCE", null, null, null, 10 },
                    { 12, "3000", 0m, 0m, 1, 1, 8, 2, true, null, "Opening Bal Equity", null, null, null, 9 },
                    { 13, "33000", 0m, 0m, 1, 1, 8, 2, true, null, "Retained Earnings", null, null, null, 9 },
                    { 14, "310", 0m, 0m, 1, 1, 8, 2, true, null, "STATE APPORTIONMENT", null, null, null, 16 },
                    { 15, "314", 0m, 0m, 1, 1, 8, 2, true, null, "WALKING TRAIL DONATION", null, null, null, 13 },
                    { 16, "315", 0m, 0m, 1, 1, 8, 2, true, null, "BASEBALL FIELD DONATIONS", null, null, null, 13 },
                    { 17, "320", 0m, 0m, 1, 1, 8, 2, true, null, "GRANT REVENUES", null, null, null, 13 },
                    { 18, "323", 0m, 0m, 1, 1, 8, 2, true, null, "MISC REVENUE", null, null, null, 16 },
                    { 19, "325", 0m, 0m, 1, 1, 8, 2, true, null, "WALKING TRAIL REVENUE", null, null, null, 16 },
                    { 20, "360", 0m, 0m, 1, 1, 8, 2, true, null, "INTEREST ON INVESTMENTS", null, null, null, 14 },
                    { 21, "370", 0m, 0m, 1, 1, 8, 2, true, null, "TRANSFER FROM REC FUND", null, null, null, 30 },
                    { 22, "2111", 0m, 0m, 1, 1, 8, 2, true, null, "BALLFIELD ACCRUED INTEREST", null, null, null, 24 },
                    { 23, "2112", 0m, 0m, 1, 1, 8, 2, true, null, "WALKING TRAIL ACCRUED INTEREST", null, null, null, 24 },
                    { 24, "410", 0m, 0m, 1, 1, 8, 2, true, null, "CAPITAL IMP - BALL COMPLEX", null, null, null, 29 },
                    { 25, "420", 0m, 0m, 1, 1, 8, 2, true, null, "PARKS - DEVELOPMENT", null, null, null, 29 }
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

            // (BudgetPeriod removal will occur after removing the MunicipalAccounts seeding below)

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

            // Remove seeded BudgetPeriod when rolling back this migration (do this after deleting accounts)
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[BudgetPeriods]', N'U') IS NOT NULL
BEGIN
    DELETE FROM [BudgetPeriods] WHERE [Id] = 1;
END
ELSE IF OBJECT_ID(N'[BudgetPeriod]', N'U') IS NOT NULL
BEGIN
    DELETE FROM [BudgetPeriod] WHERE [Id] = 1;
END");
        }
    }
}
