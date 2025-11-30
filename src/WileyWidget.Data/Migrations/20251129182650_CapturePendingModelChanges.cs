using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class CapturePendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "UtilityCustomers",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                // rowversion (timestamp) doesn't accept explicit defaults; leave default off so DB can generate values
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "MunicipalAccounts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                // rowversion (timestamp) doesn't accept explicit defaults; leave default off so DB can generate values
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<int>(
                name: "MunicipalAccountId",
                table: "BudgetEntries",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoSaveIntervalMinutes",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyFormat",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrentFiscalYear",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DatabaseName",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DatabaseServer",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DateFormat",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultLanguage",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EnableAI",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAutoSave",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableNotifications",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableQuickBooksSync",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSounds",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FiscalPeriod",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FiscalQuarter",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FiscalYearEnd",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FiscalYearStart",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearStartDay",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearStartMonth",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QuickBooksCompanyFile",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuickBooksRedirectUri",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionTimeoutMinutes",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SyncIntervalMinutes",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UseFiscalYearForReporting",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "XaiApiEndpoint",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "XaiApiKey",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "XaiMaxTokens",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "XaiModel",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "XaiTemperature",
                table: "AppSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "XaiTimeout",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TaxRevenueSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PriorYearLevy = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PriorYearAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrentYearLevy = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrentYearAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BudgetYearLevy = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BudgetYearAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IncDecLevy = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IncDecAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRevenueSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UtilityBills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    BillNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BillDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WaterCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false, defaultValue: 0m),
                    SewerCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false, defaultValue: 0m),
                    GarbageCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false, defaultValue: 0m),
                    StormwaterCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false, defaultValue: 0m),
                    LateFees = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false, defaultValue: 0m),
                    OtherCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false, defaultValue: 0m),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false, defaultValue: 0m),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WaterUsageGallons = table.Column<int>(type: "int", nullable: false),
                    PreviousMeterReading = table.Column<int>(type: "int", nullable: false),
                    CurrentMeterReading = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityBills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtilityBills_UtilityCustomers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "UtilityCustomers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillId = table.Column<int>(type: "int", nullable: false),
                    ChargeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 19, scale: 4, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 19, scale: 4, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UtilityBillId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Charges_UtilityBills_BillId",
                        column: x => x.BillId,
                        principalTable: "UtilityBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_UtilityBills_UtilityBillId",
                        column: x => x.UtilityBillId,
                        principalTable: "UtilityBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Ensure baseline AppSettings row exists before updating (idempotent).
            // This uses only columns that exist BEFORE this migration adds its new columns.
            // The columns must match those from AddAppSettingsEntity + AddAdvancedSettingsToAppSettings + AddQboClientColumnsToAppSettings
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [AppSettings] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [AppSettings] ON;
    INSERT INTO [AppSettings] (
        [Id], [Theme], [UseDynamicColumns], [QuickBooksEnvironment],
        [QboAccessToken], [QboRefreshToken], [QboTokenExpiry],
        [EnableFileLogging], [EnableDataCaching], [LogFilePath], [CacheExpirationMinutes], [SelectedLogLevel],
        [LastSelectedEnterpriseId], [IncludeChartsInReports]
    )
    VALUES (
        1, N'Light', 0, N'Production',
        NULL, NULL, '0001-01-01T00:00:00.0000000',
        1, 1, N'logs/wiley-widget.log', 30, N'Information',
        0, 0
    );
    SET IDENTITY_INSERT [AppSettings] OFF;
END
");

            // AppSettings row with Id=1 should now exist.
            // Here we UPDATE the newly-added columns from this migration to their intended defaults.
            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] {
                    "AutoSaveIntervalMinutes",
                    "CurrencyFormat",
                    "CurrentFiscalYear",
                    "DatabaseName",
                    "DatabaseServer",
                    "DateFormat",
                    "DefaultLanguage",
                    "EnableAI",
                    "EnableAutoSave",
                    "EnableNotifications",
                    "EnableQuickBooksSync",
                    "EnableSounds",
                    "FiscalPeriod",
                    "FiscalQuarter",
                    "FiscalYearEnd",
                    "FiscalYearStart",
                    "FiscalYearStartDay",
                    "FiscalYearStartMonth",
                    "SessionTimeoutMinutes",
                    "SyncIntervalMinutes",
                    "UseFiscalYearForReporting",
                    "XaiApiEndpoint",
                    "XaiMaxTokens",
                    "XaiModel",
                    "XaiTemperature",
                    "XaiTimeout"
                },
                values: new object[] {
                    5,
                    "USD",
                    "2024-2025",
                    "WileyWidget",
                    "localhost",
                    "MM/dd/yyyy",
                    "en-US",
                    false,
                    true,
                    true,
                    false,
                    true,
                    "Q1",
                    1,
                    "June 30",
                    "July 1",
                    1,
                    7,
                    60,
                    30,
                    true,
                    "https://api.x.ai/v1",
                    2000,
                    "grok-4-0709",
                    0.69999999999999996,
                    30
                });

            // Ensure baseline BudgetPeriods exist (idempotent - skip if already present).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [BudgetPeriod] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [BudgetPeriod] ON;
    INSERT INTO [BudgetPeriod] ([Id], [CreatedDate], [EndDate], [IsActive], [Name], [StartDate], [Status], [Year])
    VALUES (1, '2024-10-28T00:00:00.0000000Z', '2025-12-31T23:59:59.0000000Z', 1, N'2025 Current', '2025-01-01T00:00:00.0000000Z', 1, 2025);
    SET IDENTITY_INSERT [BudgetPeriod] OFF;
END
IF NOT EXISTS (SELECT 1 FROM [BudgetPeriod] WHERE [Id] = 2)
BEGIN
    SET IDENTITY_INSERT [BudgetPeriod] ON;
    INSERT INTO [BudgetPeriod] ([Id], [CreatedDate], [EndDate], [IsActive], [Name], [StartDate], [Status], [Year])
    VALUES (2, '2025-10-28T00:00:00.0000000Z', '2026-12-31T23:59:59.0000000Z', 0, N'2026 Proposed', '2026-01-01T00:00:00.0000000Z', 1, 2026);
    SET IDENTITY_INSERT [BudgetPeriod] OFF;
END
");

            // Idempotent seed for Departments (skip Id 1,2 if already seeded by AddBackendEnhancements)
            migrationBuilder.Sql(@"
-- Update existing departments if present, or insert new ones
IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Departments] ON;
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (1, N'ADMIN', N'Administration', NULL);
    SET IDENTITY_INSERT [Departments] OFF;
END
ELSE
BEGIN
    UPDATE [Departments] SET [DepartmentCode] = N'ADMIN', [Name] = N'Administration' WHERE [Id] = 1;
END

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 2)
BEGIN
    SET IDENTITY_INSERT [Departments] ON;
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (2, N'DPW', N'Public Works', NULL);
    SET IDENTITY_INSERT [Departments] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 3)
BEGIN
    SET IDENTITY_INSERT [Departments] ON;
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (3, N'CULT', N'Culture and Recreation', NULL);
    SET IDENTITY_INSERT [Departments] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 5)
BEGIN
    SET IDENTITY_INSERT [Departments] ON;
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (5, N'UTIL', N'Utilities', NULL);
    SET IDENTITY_INSERT [Departments] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 6)
BEGIN
    SET IDENTITY_INSERT [Departments] ON;
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (6, N'COMM', N'Community Center', NULL);
    SET IDENTITY_INSERT [Departments] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 7)
BEGIN
    SET IDENTITY_INSERT [Departments] ON;
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (7, N'CONS', N'Conservation', NULL);
    SET IDENTITY_INSERT [Departments] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 8)
BEGIN
    SET IDENTITY_INSERT [Departments] ON;
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (8, N'REC', N'Recreation', NULL);
    SET IDENTITY_INSERT [Departments] OFF;
END
");

            // Idempotent seed for Funds (update FundCode if already present from AddBackendEnhancements)
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Funds] ON;
    INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (1, N'100-GEN', N'General Fund', 1);
    SET IDENTITY_INSERT [Funds] OFF;
END
ELSE
BEGIN
    UPDATE [Funds] SET [FundCode] = N'100-GEN', [Name] = N'General Fund' WHERE [Id] = 1;
END

IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 2)
BEGIN
    SET IDENTITY_INSERT [Funds] ON;
    INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (2, N'200-ENT', N'Enterprise Fund', 2);
    SET IDENTITY_INSERT [Funds] OFF;
END
ELSE
BEGIN
    UPDATE [Funds] SET [FundCode] = N'200-ENT', [Name] = N'Enterprise Fund' WHERE [Id] = 2;
END

IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 3)
BEGIN
    SET IDENTITY_INSERT [Funds] ON;
    INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (3, N'300-UTIL', N'Utility Fund', 2);
    SET IDENTITY_INSERT [Funds] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 4)
BEGIN
    SET IDENTITY_INSERT [Funds] ON;
    INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (4, N'400-COMM', N'Community Center Fund', 3);
    SET IDENTITY_INSERT [Funds] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 5)
BEGIN
    SET IDENTITY_INSERT [Funds] ON;
    INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (5, N'500-CONS', N'Conservation Trust Fund', 6);
    SET IDENTITY_INSERT [Funds] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 6)
BEGIN
    SET IDENTITY_INSERT [Funds] ON;
    INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type])
    VALUES (6, N'600-REC', N'Recreation Fund', 3);
    SET IDENTITY_INSERT [Funds] OFF;
END
");

            // TaxRevenueSummaries can use InsertData since it's a new table
            migrationBuilder.InsertData(
                table: "TaxRevenueSummaries",
                columns: new[] { "Id", "BudgetYearAmount", "BudgetYearLevy", "CurrentYearAmount", "CurrentYearLevy", "Description", "IncDecAmount", "IncDecLevy", "PriorYearAmount", "PriorYearLevy" },
                values: new object[,]
                {
                    { 1, 1880448m, 1880448m, 1072691m, 1072691m, "ASSESSED VALUATION-COUNTY FUND", 807757m, 807757m, 1069780m, 1069780m },
                    { 2, 85692m, 45.570m, 48883m, 45.570m, "GENERAL", 36809m, 0m, 48750m, 45.570m },
                    { 3, 0m, 0m, 0m, 0m, "UTILITY", 0m, 0m, 0m, 0m },
                    { 4, 0m, 0m, 0m, 0m, "COMMUNITY CENTER", 0m, 0m, 0m, 0m },
                    { 5, 0m, 0m, 0m, 0m, "CONSERVATION TRUST FUND", 0m, 0m, 0m, 0m },
                    { 6, 0m, 0m, 0m, 0m, "TEMPORARY MILL LEVY CREDIT", 0m, 0m, 0m, 0m },
                    { 7, 85692m, 45.570m, 48883m, 45.570m, "TOTAL", 36810m, 0m, 48750m, 45.570m }
                });

            // Idempotent seed for Vendors
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Vendor] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [Vendor] ON;
    INSERT INTO [Vendor] ([Id], [ContactInfo], [IsActive], [Name])
    VALUES (1, N'contact@acmesupplies.example.com', 1, N'Acme Supplies');
    SET IDENTITY_INSERT [Vendor] OFF;
END
IF NOT EXISTS (SELECT 1 FROM [Vendor] WHERE [Id] = 2)
BEGIN
    SET IDENTITY_INSERT [Vendor] ON;
    INSERT INTO [Vendor] ([Id], [ContactInfo], [IsActive], [Name])
    VALUES (2, N'info@muniservices.example.com', 1, N'Municipal Services Co.');
    SET IDENTITY_INSERT [Vendor] OFF;
END
IF NOT EXISTS (SELECT 1 FROM [Vendor] WHERE [Id] = 3)
BEGIN
    SET IDENTITY_INSERT [Vendor] ON;
    INSERT INTO [Vendor] ([Id], [ContactInfo], [IsActive], [Name])
    VALUES (3, N'projects@trailbuilders.example.com', 1, N'Trail Builders LLC');
    SET IDENTITY_INSERT [Vendor] OFF;
END
");

            // Idempotent seed for BudgetEntries - update if exists with different AccountNumber, or insert if not present
            migrationBuilder.Sql(@"
-- BudgetEntries Id 1-2 may exist from AddBackendEnhancements - update or skip
IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (1, N'332.1', NULL, 0, 360, '2025-10-28', 1, N'Federal: Mineral Lease', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 2)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (2, N'333.00', NULL, 0, 240, '2025-10-28', 1, N'State: Cigarette Taxes', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

-- BudgetEntries Id 3-20 are new
IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 3)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (3, N'334.31', NULL, 0, 18153, '2025-10-28', 1, N'Highways Users', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 4)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (4, N'313.00', NULL, 0, 1775, '2025-10-28', 1, N'Additional MV', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 5)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (5, N'337.17', NULL, 0, 1460, '2025-10-28', 1, N'County Road & Bridge', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 6)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (6, N'311.20', NULL, 0, 1500, '2025-10-28', 1, N'Senior Homestead Exemption', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 7)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (7, N'312.00', NULL, 0, 5100, '2025-10-28', 1, N'Specific Ownership Taxes', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 8)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (8, N'314.00', NULL, 0, 2500, '2025-10-28', 1, N'Tax A', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 9)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (9, N'319.00', NULL, 0, 35, '2025-10-28', 1, N'Penalties & Interest on Delinquent Taxes', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 10)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (10, N'336.00', NULL, 0, 120000, '2025-10-28', 1, N'Sales Tax', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 11)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (11, N'318.20', NULL, 0, 7058, '2025-10-28', 1, N'Franchise Fee', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 12)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (12, N'322.70', NULL, 0, 50, '2025-10-28', 1, N'Animal Licenses', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 13)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (13, N'310.00', NULL, 0, 6000, '2025-10-28', 1, N'Charges for Services: WSD Collection Fee', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 14)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (14, N'370.00', NULL, 0, 12000, '2025-10-28', 1, N'Housing Authority Mgt Fee', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 15)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (15, N'373.00', NULL, 0, 2400, '2025-10-28', 1, N'Pickup Usage Fee', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 16)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (16, N'361.00', NULL, 0, 325, '2025-10-28', 1, N'Miscellaneous Receipts: Interest Earnings', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 17)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (17, N'365.00', NULL, 0, 100, '2025-10-28', 1, N'Dividends', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 18)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (18, N'363.00', NULL, 0, 1100, '2025-10-28', 1, N'Lease', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 19)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (19, N'350.00', NULL, 0, 10000, '2025-10-28', 1, N'Wiley Hay Days Donations', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 20)
BEGIN
    SET IDENTITY_INSERT [BudgetEntries] ON;
    INSERT INTO [BudgetEntries] ([Id], [AccountNumber], [ActivityCode], [ActualAmount], [BudgetedAmount], [CreatedAt], [DepartmentId], [Description], [EncumbranceAmount], [EndPeriod], [FiscalYear], [FundId], [FundType], [IsGASBCompliant], [MunicipalAccountId], [ParentId], [SourceFilePath], [SourceRowNumber], [StartPeriod], [UpdatedAt], [Variance])
    VALUES (20, N'362.00', NULL, 0, 2500, '2025-10-28', 1, N'Donations', 0, '0001-01-01', 2026, 1, 1, 1, 1, NULL, NULL, NULL, '0001-01-01', '2025-10-28', 0);
    SET IDENTITY_INSERT [BudgetEntries] OFF;
END

-- Department Id=4 (child of 2)
IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 4)
BEGIN
    SET IDENTITY_INSERT [Departments] ON;
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId])
    VALUES (4, N'SAN', N'Sanitation', 2);
    SET IDENTITY_INSERT [Departments] OFF;
END
");

            // Idempotent seed for MunicipalAccounts
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [MunicipalAccounts] ON;
    INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
    VALUES (1, N'100', 0, 0, 1, 1, 1, N'General Fund', 1, NULL, N'GENERAL ACCOUNT', NULL, NULL, NULL, 1, N'Asset', 0);
    SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 26)
BEGIN
    SET IDENTITY_INSERT [MunicipalAccounts] ON;
    INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
    VALUES (26, N'425', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'MISC EXPENSE', NULL, NULL, NULL, 24, N'Asset', 0);
    SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 27)
BEGIN
    SET IDENTITY_INSERT [MunicipalAccounts] ON;
    INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
    VALUES (27, N'430', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'TRAIL MAINTENANCE', NULL, NULL, NULL, 24, N'Asset', 0);
    SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 28)
BEGIN
    SET IDENTITY_INSERT [MunicipalAccounts] ON;
    INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
    VALUES (28, N'435', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'PARK IMPROVEMENTS', NULL, NULL, NULL, 29, N'Asset', 0);
    SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 29)
BEGIN
    SET IDENTITY_INSERT [MunicipalAccounts] ON;
    INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
    VALUES (29, N'440', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'EQUIPMENT PURCHASES', NULL, NULL, NULL, 29, N'Asset', 0);
    SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 30)
BEGIN
    SET IDENTITY_INSERT [MunicipalAccounts] ON;
    INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
    VALUES (30, N'445', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'PROJECTS - SMALL', NULL, NULL, NULL, 24, N'Asset', 0);
    SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END

IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 31)
BEGIN
    SET IDENTITY_INSERT [MunicipalAccounts] ON;
    INSERT INTO [MunicipalAccounts] ([Id], [AccountNumber_Value], [Balance], [BudgetAmount], [BudgetPeriodId], [DepartmentId], [Fund], [FundDescription], [IsActive], [LastSyncDate], [Name], [Notes], [ParentAccountId], [QuickBooksId], [Type], [TypeDescription], [FundClass])
    VALUES (31, N'450', 0, 0, 1, 1, 8, N'Conservation Trust Fund', 1, NULL, N'RESERVES ALLOCATION', NULL, NULL, NULL, 30, N'Asset', 0);
    SET IDENTITY_INSERT [MunicipalAccounts] OFF;
END
");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_BillId",
                table: "Charges",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ChargeType",
                table: "Charges",
                column: "ChargeType");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_UtilityBillId",
                table: "Charges",
                column: "UtilityBillId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBills_BillDate",
                table: "UtilityBills",
                column: "BillDate");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBills_BillNumber",
                table: "UtilityBills",
                column: "BillNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBills_CustomerId",
                table: "UtilityBills",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBills_DueDate",
                table: "UtilityBills",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBills_Status",
                table: "UtilityBills",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Charges");

            migrationBuilder.DropTable(
                name: "TaxRevenueSummaries");

            migrationBuilder.DropTable(
                name: "UtilityBills");

            migrationBuilder.DeleteData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 20);

            // Use raw SQL to delete BudgetPeriod to avoid model validation
            migrationBuilder.Sql("DELETE FROM [BudgetPeriod] WHERE [Id] IN (1, 2);");

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Vendor",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Vendor",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Vendor",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "AutoSaveIntervalMinutes",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CurrencyFormat",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CurrentFiscalYear",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DatabaseName",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DatabaseServer",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DateFormat",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultLanguage",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EnableAI",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EnableAutoSave",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EnableNotifications",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EnableQuickBooksSync",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EnableSounds",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FiscalPeriod",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FiscalQuarter",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FiscalYearEnd",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FiscalYearStart",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FiscalYearStartDay",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FiscalYearStartMonth",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "QuickBooksCompanyFile",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "QuickBooksRedirectUri",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SessionTimeoutMinutes",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SyncIntervalMinutes",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UseFiscalYearForReporting",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "XaiApiEndpoint",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "XaiApiKey",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "XaiMaxTokens",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "XaiModel",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "XaiTemperature",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "XaiTimeout",
                table: "AppSettings");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "UtilityCustomers",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "MunicipalAccounts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<int>(
                name: "MunicipalAccountId",
                table: "BudgetEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
