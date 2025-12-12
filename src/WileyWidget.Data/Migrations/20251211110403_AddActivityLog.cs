using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLog : Migration
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
                nullable: false,
                defaultValue: 0,
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
                name: "ActivityLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Activity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    User = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLog", x => x.Id);
                });

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

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "AutoSaveIntervalMinutes", "CacheExpirationMinutes", "CurrencyFormat", "CurrentFiscalYear", "DatabaseName", "DatabaseServer", "DateFormat", "DefaultLanguage", "EnableAI", "EnableAutoSave", "EnableDataCaching", "EnableFileLogging", "EnableNotifications", "EnableQuickBooksSync", "EnableSounds", "FiscalPeriod", "FiscalQuarter", "FiscalYearEnd", "FiscalYearStart", "FiscalYearStartDay", "FiscalYearStartMonth", "IncludeChartsInReports", "LastReportEndDate", "LastReportStartDate", "LastSelectedEnterpriseId", "LastSelectedFormat", "LastSelectedReportType", "LogFilePath", "QboAccessToken", "QboClientId", "QboClientSecret", "QboRefreshToken", "QboTokenExpiry", "QuickBooksAccessToken", "QuickBooksCompanyFile", "QuickBooksEnvironment", "QuickBooksRealmId", "QuickBooksRedirectUri", "QuickBooksRefreshToken", "QuickBooksTokenExpiresUtc", "SelectedLogLevel", "SessionTimeoutMinutes", "SyncIntervalMinutes", "Theme", "UseDynamicColumns", "UseFiscalYearForReporting", "WindowHeight", "WindowLeft", "WindowMaximized", "WindowTop", "WindowWidth", "XaiApiEndpoint", "XaiApiKey", "XaiMaxTokens", "XaiModel", "XaiTemperature", "XaiTimeout" },
                values: new object[] { 1, 5, 30, "USD", "2024-2025", "WileyWidget", "localhost", "MM/dd/yyyy", "en-US", false, true, true, true, true, false, true, "Q1", 1, "June 30", "July 1", 1, 7, true, null, null, 1, null, null, "logs/wiley-widget.log", null, null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "sandbox", null, null, null, null, "Information", 60, 30, "FluentDark", false, true, null, null, null, null, null, "https://api.x.ai/v1", null, 2000, "grok-4-0709", 0.69999999999999996, 30 });

            migrationBuilder.InsertData(
                table: "BudgetPeriods",
                columns: new[] { "Id", "CreatedDate", "EndDate", "IsActive", "Name", "StartDate", "Status", "Year" },
                values: new object[] { 2, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 31, 23, 59, 59, 0, DateTimeKind.Utc), false, "2026 Proposed", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 2026 });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "DepartmentCode", "Name", "ParentId" },
                values: new object[,]
                {
                    { 1, "ADMIN", "Administration", null },
                    { 2, "DPW", "Public Works", null },
                    { 3, "CULT", "Culture and Recreation", null },
                    { 5, "UTIL", "Utilities", null },
                    { 6, "COMM", "Community Center", null },
                    { 7, "CONS", "Conservation", null },
                    { 8, "REC", "Recreation", null }
                });

            migrationBuilder.InsertData(
                table: "Funds",
                columns: new[] { "Id", "FundCode", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "100-GEN", "General Fund", 1 },
                    { 2, "200-ENT", "Enterprise Fund", 2 },
                    { 3, "300-UTIL", "Utility Fund", 2 },
                    { 4, "400-COMM", "Community Center Fund", 3 },
                    { 5, "500-CONS", "Conservation Trust Fund", 6 },
                    { 6, "600-REC", "Recreation Fund", 3 }
                });

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

            migrationBuilder.InsertData(
                table: "Vendor",
                columns: new[] { "Id", "ContactInfo", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "contact@acmesupplies.example.com", true, "Acme Supplies" },
                    { 2, "info@muniservices.example.com", true, "Municipal Services Co." },
                    { 3, "projects@trailbuilders.example.com", true, "Trail Builders LLC" }
                });

            migrationBuilder.InsertData(
                table: "BudgetEntries",
                columns: new[] { "Id", "AccountNumber", "ActivityCode", "ActualAmount", "BudgetedAmount", "CreatedAt", "DepartmentId", "Description", "EncumbranceAmount", "EndPeriod", "FiscalYear", "FundId", "FundType", "IsGASBCompliant", "MunicipalAccountId", "ParentId", "SourceFilePath", "SourceRowNumber", "StartPeriod", "UpdatedAt", "Variance" },
                values: new object[,]
                {
                    { 1, "332.1", null, 0m, 360m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Federal: Mineral Lease", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 2, "333.00", null, 0m, 240m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "State: Cigarette Taxes", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 3, "334.31", null, 0m, 18153m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Highways Users", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 4, "313.00", null, 0m, 1775m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Additional MV", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 5, "337.17", null, 0m, 1460m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "County Road & Bridge", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 6, "311.20", null, 0m, 1500m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Senior Homestead Exemption", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 7, "312.00", null, 0m, 5100m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Specific Ownership Taxes", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 8, "314.00", null, 0m, 2500m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Tax A", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 9, "319.00", null, 0m, 35m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Penalties & Interest on Delinquent Taxes", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 10, "336.00", null, 0m, 120000m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Sales Tax", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 11, "318.20", null, 0m, 7058m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Franchise Fee", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 12, "322.70", null, 0m, 50m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Animal Licenses", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 13, "310.00", null, 0m, 6000m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Charges for Services: WSD Collection Fee", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 14, "370.00", null, 0m, 12000m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Housing Authority Mgt Fee", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 15, "373.00", null, 0m, 2400m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Pickup Usage Fee", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 16, "361.00", null, 0m, 325m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Miscellaneous Receipts: Interest Earnings", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 17, "365.00", null, 0m, 100m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Dividends", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 18, "363.00", null, 0m, 1100m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Lease", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 19, "350.00", null, 0m, 10000m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Wiley Hay Days Donations", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 20, "362.00", null, 0m, 2500m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Donations", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, 0, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m }
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "DepartmentCode", "Name", "ParentId" },
                values: new object[] { 4, "SAN", "Sanitation", 2 });

            migrationBuilder.InsertData(
                table: "MunicipalAccounts",
                columns: new[] { "Id", "AccountNumber", "Balance", "BudgetAmount", "BudgetPeriodId", "DepartmentId", "Fund", "FundDescription", "IsActive", "LastSyncDate", "Name", "Notes", "ParentAccountId", "QuickBooksId", "Type", "TypeDescription" },
                values: new object[,]
                {
                    { 26, "425", 0m, 0m, 1, 1, 8, "Conservation Trust Fund", true, null, "MISC EXPENSE", null, null, null, 24, "Asset" },
                    { 27, "430", 0m, 0m, 1, 1, 8, "Conservation Trust Fund", true, null, "TRAIL MAINTENANCE", null, null, null, 24, "Asset" },
                    { 28, "435", 0m, 0m, 1, 1, 8, "Conservation Trust Fund", true, null, "PARK IMPROVEMENTS", null, null, null, 29, "Asset" },
                    { 29, "440", 0m, 0m, 1, 1, 8, "Conservation Trust Fund", true, null, "EQUIPMENT PURCHASES", null, null, null, 29, "Asset" },
                    { 30, "445", 0m, 0m, 1, 1, 8, "Conservation Trust Fund", true, null, "PROJECTS - SMALL", null, null, null, 24, "Asset" },
                    { 31, "450", 0m, 0m, 1, 1, 8, "Conservation Trust Fund", true, null, "RESERVES ALLOCATION", null, null, null, 30, "Asset" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_Timestamp",
                table: "ActivityLog",
                column: "Timestamp");

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
                name: "ActivityLog");

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

            migrationBuilder.DeleteData(
                table: "BudgetPeriods",
                keyColumn: "Id",
                keyValue: 2);

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
