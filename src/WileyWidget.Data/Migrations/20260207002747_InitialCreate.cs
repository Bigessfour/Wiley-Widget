using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Theme = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WindowWidth = table.Column<double>(type: "float", nullable: true),
                    WindowHeight = table.Column<double>(type: "float", nullable: true),
                    WindowLeft = table.Column<double>(type: "float", nullable: true),
                    WindowTop = table.Column<double>(type: "float", nullable: true),
                    WindowMaximized = table.Column<bool>(type: "bit", nullable: true),
                    DatabaseServer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DatabaseName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuickBooksCompanyFile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnableQuickBooksSync = table.Column<bool>(type: "bit", nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    QuickBooksRedirectUri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnableAutoSave = table.Column<bool>(type: "bit", nullable: false),
                    AutoSaveIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    ApplicationFont = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UseDynamicColumns = table.Column<bool>(type: "bit", nullable: false),
                    EnableDataCaching = table.Column<bool>(type: "bit", nullable: false),
                    CacheExpirationMinutes = table.Column<int>(type: "int", nullable: false),
                    SelectedLogLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnableFileLogging = table.Column<bool>(type: "bit", nullable: false),
                    LogFilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuickBooksAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuickBooksRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuickBooksRealmId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuickBooksEnvironment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuickBooksTokenExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QboAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QboRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QboTokenExpiry = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QboClientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QboClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnableAI = table.Column<bool>(type: "bit", nullable: false),
                    XaiApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    XaiModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    XaiApiEndpoint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    XaiTimeout = table.Column<int>(type: "int", nullable: false),
                    XaiMaxTokens = table.Column<int>(type: "int", nullable: false),
                    XaiTemperature = table.Column<double>(type: "float", nullable: false),
                    EnableNotifications = table.Column<bool>(type: "bit", nullable: false),
                    EnableSounds = table.Column<bool>(type: "bit", nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateFormat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrencyFormat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SessionTimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                    FiscalYearStart = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FiscalYearStartMonth = table.Column<int>(type: "int", nullable: false),
                    FiscalYearStartDay = table.Column<int>(type: "int", nullable: false),
                    FiscalYearEnd = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentFiscalYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UseFiscalYearForReporting = table.Column<bool>(type: "bit", nullable: false),
                    FiscalQuarter = table.Column<int>(type: "int", nullable: false),
                    FiscalPeriod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSelectedReportType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSelectedFormat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastReportStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReportEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IncludeChartsInReports = table.Column<bool>(type: "bit", nullable: false),
                    LastSelectedEnterpriseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationHistories",
                columns: table => new
                {
                    ConversationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationHistories", x => x.ConversationId);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentCurrentCharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Department = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentCharge = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    CustomerCount = table.Column<int>(type: "int", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentCurrentCharges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Department = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdjustmentFactor = table.Column<decimal>(type: "decimal(18,4)", precision: 19, scale: 4, nullable: false, defaultValue: 1.0m),
                    TargetProfitMarginPercent = table.Column<decimal>(type: "decimal(18,4)", precision: 19, scale: 4, nullable: false),
                    RecommendationText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentGoals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    DepartmentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Departments_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enterprises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentRate = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    MonthlyExpenses = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    CitizenCount = table.Column<int>(type: "int", nullable: false),
                    TotalBudget = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MeterReading = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: true),
                    MeterReadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousMeterReading = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: true),
                    PreviousMeterReadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enterprises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYearSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalYearStartMonth = table.Column<int>(type: "int", nullable: false),
                    FiscalYearStartDay = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Funds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FundCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Funds", x => x.Id);
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
                name: "TelemetryLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TownOfWileyBudgetData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceFile = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FundOrDepartment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PriorYearActual = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    SevenMonthActual = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    EstimateCurrentYr = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    BudgetYear = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ActualYTD = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Remaining = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PercentOfBudget = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MappedDepartment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TownOfWileyBudgetData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UtilityCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false, defaultValue: new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerType = table.Column<int>(type: "int", nullable: false),
                    ServiceAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServiceCity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ServiceState = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ServiceZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MailingAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MailingCity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MailingState = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    MailingZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MeterNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ServiceLocation = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AccountOpenDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountCloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BusinessLicenseNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConnectDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisconnectDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    LastPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityCustomers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactInfo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MailingAddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MailingAddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MailingAddressCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MailingAddressState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MailingAddressPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MailingAddressCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QuickBooksId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MunicipalAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ParentAccountId = table.Column<int>(type: "int", nullable: true),
                    BudgetPeriodId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    TypeDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FundDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fund = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    QuickBooksId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastSyncDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccountNumber_Value = table.Column<string>(type: "nvarchar(max)", nullable: true, computedColumnSql: "[AccountNumber]"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false, defaultValue: new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 })
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MunicipalAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId",
                        column: x => x.BudgetPeriodId,
                        principalTable: "BudgetPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MunicipalAccounts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "MunicipalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetInteraction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrimaryEnterpriseId = table.Column<int>(type: "int", nullable: false),
                    SecondaryEnterpriseId = table.Column<int>(type: "int", nullable: true),
                    InteractionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    InteractionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCost = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EnterpriseId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetInteraction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                        column: x => x.EnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                        column: x => x.PrimaryEnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                        column: x => x.SecondaryEnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false, defaultValue: new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }),
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
                name: "BudgetEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BudgetedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false, defaultValue: 0m),
                    ActualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    StartPeriod = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndPeriod = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FundType = table.Column<int>(type: "int", nullable: false),
                    EncumbranceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    IsGASBCompliant = table.Column<bool>(type: "bit", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    FundId = table.Column<int>(type: "int", nullable: true),
                    MunicipalAccountId = table.Column<int>(type: "int", nullable: true),
                    SourceFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceRowNumber = table.Column<int>(type: "int", nullable: true),
                    ActivityCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetEntries", x => x.Id);
                    table.CheckConstraint("CK_Budget_Positive", "[BudgetedAmount] > 0");
                    table.ForeignKey(
                        name: "FK_BudgetEntries_BudgetEntries_ParentId",
                        column: x => x.ParentId,
                        principalTable: "BudgetEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetEntries_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetEntries_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                        column: x => x.MunicipalAccountId,
                        principalTable: "MunicipalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    MunicipalAccountId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                        column: x => x.MunicipalAccountId,
                        principalTable: "MunicipalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
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

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BudgetEntryId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MunicipalAccountId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transaction_NonZero", "[Amount] != 0");
                    table.ForeignKey(
                        name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                        column: x => x.BudgetEntryId,
                        principalTable: "BudgetEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                        column: x => x.MunicipalAccountId,
                        principalTable: "MunicipalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Payee = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MunicipalAccountId = table.Column<int>(type: "int", nullable: true),
                    VendorId = table.Column<int>(type: "int", nullable: true),
                    InvoiceId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Cleared"),
                    IsCleared = table.Column<bool>(type: "bit", nullable: false),
                    Memo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_MunicipalAccounts_MunicipalAccountId",
                        column: x => x.MunicipalAccountId,
                        principalTable: "MunicipalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "ApplicationFont", "AutoSaveIntervalMinutes", "CacheExpirationMinutes", "CurrencyFormat", "CurrentFiscalYear", "DatabaseName", "DatabaseServer", "DateFormat", "DefaultLanguage", "EnableAI", "EnableAutoSave", "EnableDataCaching", "EnableFileLogging", "EnableNotifications", "EnableQuickBooksSync", "EnableSounds", "FiscalPeriod", "FiscalQuarter", "FiscalYearEnd", "FiscalYearStart", "FiscalYearStartDay", "FiscalYearStartMonth", "IncludeChartsInReports", "LastReportEndDate", "LastReportStartDate", "LastSelectedEnterpriseId", "LastSelectedFormat", "LastSelectedReportType", "LogFilePath", "QboAccessToken", "QboClientId", "QboClientSecret", "QboRefreshToken", "QboTokenExpiry", "QuickBooksAccessToken", "QuickBooksCompanyFile", "QuickBooksEnvironment", "QuickBooksRealmId", "QuickBooksRedirectUri", "QuickBooksRefreshToken", "QuickBooksTokenExpiresUtc", "SelectedLogLevel", "SessionTimeoutMinutes", "SyncIntervalMinutes", "Theme", "UseDynamicColumns", "UseFiscalYearForReporting", "WindowHeight", "WindowLeft", "WindowMaximized", "WindowTop", "WindowWidth", "XaiApiEndpoint", "XaiApiKey", "XaiMaxTokens", "XaiModel", "XaiTemperature", "XaiTimeout" },
                values: new object[] { 1, "Segoe UI, 9pt", 5, 30, "USD", "2024-2025", "WileyWidget", "localhost", "MM/dd/yyyy", "en-US", false, true, true, true, true, false, true, "Q1", 1, "June 30", "July 1", 1, 7, true, null, null, 1, null, null, "logs/wiley-widget.log", null, null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "sandbox", null, null, null, null, "Information", 60, 30, "FluentDark", false, true, null, null, null, null, null, "https://api.x.ai/v1", null, 2000, "grok-4.1", 0.69999999999999996, 30 });

            migrationBuilder.InsertData(
                table: "BudgetPeriods",
                columns: new[] { "Id", "CreatedDate", "EndDate", "IsActive", "Name", "StartDate", "Status", "Year" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 31, 23, 59, 59, 0, DateTimeKind.Utc), true, "2025 Adopted", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, 2025 },
                    { 2, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 31, 23, 59, 59, 0, DateTimeKind.Utc), false, "2026 Proposed", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 2026 }
                });

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
                    { 1, "332.1", null, 0m, 360m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Federal: Mineral Lease", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m },
                    { 2, "333.00", null, 0m, 240m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "State: Cigarette Taxes", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 3, "334.31", null, 0m, 18153m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Highways Users", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 4, "313.00", null, 0m, 1775m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Additional MV", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 5, "337.17", null, 0m, 1460m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "County Road & Bridge", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 6, "311.20", null, 0m, 1500m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Senior Homestead Exemption", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 7, "312.00", null, 0m, 5100m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Specific Ownership Taxes", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 8, "314.00", null, 0m, 2500m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Tax A", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 9, "319.00", null, 0m, 35m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Penalties & Interest on Delinquent Taxes", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 10, "336.00", null, 0m, 120000m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Sales Tax", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 11, "318.20", null, 0m, 7058m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Franchise Fee", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 12, "322.70", null, 0m, 50m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Animal Licenses", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 13, "310.00", null, 0m, 6000m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Charges for Services: WSD Collection Fee", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 14, "370.00", null, 0m, 12000m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Housing Authority Mgt Fee", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 15, "373.00", null, 0m, 2400m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Pickup Usage Fee", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 16, "361.00", null, 0m, 325m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Miscellaneous Receipts: Interest Earnings", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 17, "365.00", null, 0m, 100m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Dividends", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 18, "363.00", null, 0m, 1100m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Lease", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 19, "350.00", null, 0m, 10000m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Wiley Hay Days Donations", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 20, "362.00", null, 0m, 2500m, new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Donations", 0m, new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 1, true, null, null, null, null, new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m }
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
                    { 1, "110", 0m, 0m, 1, 1, 8, "General Fund", true, null, "CASH IN BANK", null, null, null, 0, "Asset" },
                    { 2, "110.1", 0m, 0m, 1, 1, 8, "General Fund", true, null, "CASH-BASEBALL FIELD PROJECT", null, null, null, 0, "Asset" },
                    { 3, "120", 0m, 0m, 1, 1, 8, "General Fund", true, null, "INVESTMENTS", null, null, null, 1, "Asset" },
                    { 4, "130", 0m, 0m, 1, 1, 8, "General Fund", true, null, "INTERGOVERNMENTAL RECEIVABLE", null, null, null, 2, "Asset" },
                    { 5, "140", 0m, 0m, 1, 1, 8, "General Fund", true, null, "GRANT RECEIVABLE", null, null, null, 2, "Asset" },
                    { 6, "210", 0m, 0m, 1, 1, 8, "General Fund", true, null, "ACCOUNTS PAYABLE", null, null, null, 6, "Asset" },
                    { 7, "211", 0m, 0m, 1, 1, 8, "General Fund", true, null, "BASEBALL FIELD PROJECT LOAN", null, null, null, 7, "Asset" },
                    { 8, "212", 0m, 0m, 1, 1, 8, "General Fund", true, null, "WALKING TRAIL LOAN", null, null, null, 7, "Asset" },
                    { 9, "230", 0m, 0m, 1, 1, 8, "General Fund", true, null, "DUE TO/FROM TOW GENERAL FUND", null, null, null, 8, "Asset" },
                    { 10, "240", 0m, 0m, 1, 1, 8, "General Fund", true, null, "DUE TO/FROM TOW UTILITY FUND", null, null, null, 8, "Asset" },
                    { 11, "290", 0m, 0m, 1, 1, 8, "General Fund", true, null, "FUND BALANCE", null, null, null, 10, "Asset" },
                    { 12, "3000", 0m, 0m, 1, 1, 8, "General Fund", true, null, "Opening Bal Equity", null, null, null, 9, "Asset" },
                    { 13, "33000", 0m, 0m, 1, 1, 8, "General Fund", true, null, "Retained Earnings", null, null, null, 9, "Asset" },
                    { 14, "310", 0m, 0m, 1, 1, 8, "General Fund", true, null, "STATE APPORTIONMENT", null, null, null, 16, "Asset" },
                    { 15, "314", 0m, 0m, 1, 1, 8, "General Fund", true, null, "WALKING TRAIL DONATION", null, null, null, 13, "Asset" },
                    { 16, "315", 0m, 0m, 1, 1, 8, "General Fund", true, null, "BASEBALL FIELD DONATIONS", null, null, null, 13, "Asset" },
                    { 17, "320", 0m, 0m, 1, 1, 8, "General Fund", true, null, "GRANT REVENUES", null, null, null, 13, "Asset" },
                    { 18, "323", 0m, 0m, 1, 1, 8, "General Fund", true, null, "MISC REVENUE", null, null, null, 16, "Asset" },
                    { 19, "325", 0m, 0m, 1, 1, 8, "General Fund", true, null, "WALKING TRAIL REVENUE", null, null, null, 16, "Asset" },
                    { 20, "360", 0m, 0m, 1, 1, 8, "General Fund", true, null, "INTEREST ON INVESTMENTS", null, null, null, 14, "Asset" },
                    { 21, "370", 0m, 0m, 1, 1, 8, "General Fund", true, null, "TRANSFER FROM REC FUND", null, null, null, 30, "Asset" },
                    { 22, "2111", 0m, 0m, 1, 1, 8, "General Fund", true, null, "BALLFIELD ACCRUED INTEREST", null, null, null, 24, "Asset" },
                    { 23, "2112", 0m, 0m, 1, 1, 8, "General Fund", true, null, "WALKING TRAIL ACCRUED INTEREST", null, null, null, 24, "Asset" },
                    { 24, "410", 0m, 0m, 1, 1, 8, "General Fund", true, null, "CAPITAL IMP - BALL COMPLEX", null, null, null, 29, "Asset" },
                    { 25, "420", 0m, 0m, 1, 1, 8, "General Fund", true, null, "PARKS - DEVELOPMENT", null, null, null, 29, "Asset" },
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
                name: "IX_BudgetEntries_AccountNumber_FiscalYear",
                table: "BudgetEntries",
                columns: new[] { "AccountNumber", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_ActivityCode",
                table: "BudgetEntries",
                column: "ActivityCode");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_DepartmentId",
                table: "BudgetEntries",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_FiscalYear",
                table: "BudgetEntries",
                column: "FiscalYear");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_FundId",
                table: "BudgetEntries",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_MunicipalAccountId",
                table: "BudgetEntries",
                column: "MunicipalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_ParentId",
                table: "BudgetEntries",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_SourceRowNumber",
                table: "BudgetEntries",
                column: "SourceRowNumber");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteraction_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteraction_PrimaryEnterpriseId",
                table: "BudgetInteraction",
                column: "PrimaryEnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteraction_SecondaryEnterpriseId",
                table: "BudgetInteraction",
                column: "SecondaryEnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_IsActive",
                table: "BudgetPeriods",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Year",
                table: "BudgetPeriods",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Year_Status",
                table: "BudgetPeriods",
                columns: new[] { "Year", "Status" });

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
                name: "IX_ConversationHistories_UpdatedAt",
                table: "ConversationHistories",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentCurrentCharges_Department",
                table: "DepartmentCurrentCharges",
                column: "Department",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentCurrentCharges_IsActive",
                table: "DepartmentCurrentCharges",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentGoals_Department_IsActive",
                table: "DepartmentGoals",
                columns: new[] { "Department", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DepartmentCode",
                table: "Departments",
                column: "DepartmentCode",
                unique: true,
                filter: "[DepartmentCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ParentId",
                table: "Departments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_MunicipalAccountId",
                table: "Invoices",
                column: "MunicipalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_VendorId",
                table: "Invoices",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_BudgetPeriodId",
                table: "MunicipalAccounts",
                column: "BudgetPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_DepartmentId",
                table: "MunicipalAccounts",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_Fund_Type",
                table: "MunicipalAccounts",
                columns: new[] { "Fund", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CheckNumber",
                table: "Payments",
                column: "CheckNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MunicipalAccountId",
                table: "Payments",
                column: "MunicipalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Payee",
                table: "Payments",
                column: "Payee");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentDate",
                table: "Payments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "Payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_VendorId",
                table: "Payments",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_TownOfWileyBudgetData_AccountCode",
                table: "TownOfWileyBudgetData",
                column: "AccountCode");

            migrationBuilder.CreateIndex(
                name: "IX_TownOfWileyBudgetData_FundOrDepartment",
                table: "TownOfWileyBudgetData",
                column: "FundOrDepartment");

            migrationBuilder.CreateIndex(
                name: "IX_TownOfWileyBudgetData_MappedDepartment",
                table: "TownOfWileyBudgetData",
                column: "MappedDepartment");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BudgetEntryId",
                table: "Transactions",
                column: "BudgetEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate",
                table: "Transactions",
                column: "TransactionDate");

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

            migrationBuilder.CreateIndex(
                name: "IX_Vendor_IsActive",
                table: "Vendor",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Vendor_Name",
                table: "Vendor",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLog");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "BudgetInteraction");

            migrationBuilder.DropTable(
                name: "Charges");

            migrationBuilder.DropTable(
                name: "ConversationHistories");

            migrationBuilder.DropTable(
                name: "DepartmentCurrentCharges");

            migrationBuilder.DropTable(
                name: "DepartmentGoals");

            migrationBuilder.DropTable(
                name: "FiscalYearSettings");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "TaxRevenueSummaries");

            migrationBuilder.DropTable(
                name: "TelemetryLogs");

            migrationBuilder.DropTable(
                name: "TownOfWileyBudgetData");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Enterprises");

            migrationBuilder.DropTable(
                name: "UtilityBills");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "BudgetEntries");

            migrationBuilder.DropTable(
                name: "UtilityCustomers");

            migrationBuilder.DropTable(
                name: "Vendor");

            migrationBuilder.DropTable(
                name: "Funds");

            migrationBuilder.DropTable(
                name: "MunicipalAccounts");

            migrationBuilder.DropTable(
                name: "BudgetPeriods");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}
