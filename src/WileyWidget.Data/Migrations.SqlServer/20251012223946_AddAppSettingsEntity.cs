using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppSettingsEntity : Migration
    {
        private const string BudgetPeriodsTable = "BudgetPeriods";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    WindowWidth = table.Column<double>(type: "REAL", nullable: true),
                    WindowHeight = table.Column<double>(type: "REAL", nullable: true),
                    WindowLeft = table.Column<double>(type: "REAL", nullable: true),
                    WindowTop = table.Column<double>(type: "REAL", nullable: true),
                    WindowMaximized = table.Column<bool>(type: "INTEGER", nullable: true),
                    UseDynamicColumns = table.Column<bool>(type: "INTEGER", nullable: false),
                    QuickBooksAccessToken = table.Column<string>(type: "TEXT", nullable: true),
                    QuickBooksRefreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    QuickBooksRealmId = table.Column<string>(type: "TEXT", nullable: true),
                    QuickBooksEnvironment = table.Column<string>(type: "TEXT", nullable: false),
                    QuickBooksTokenExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    QboAccessToken = table.Column<string>(type: "TEXT", nullable: true),
                    QboRefreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    QboTokenExpiry = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: BudgetPeriodsTable,
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Fund = table.Column<string>(type: "TEXT", nullable: false),
                    ParentDepartmentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Departments_ParentDepartmentId",
                        column: x => x.ParentDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enterprises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CurrentRate = table.Column<decimal>(type: "REAL", nullable: false),
                    MonthlyExpenses = table.Column<decimal>(type: "REAL", nullable: false),
                    CitizenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalBudget = table.Column<decimal>(type: "REAL", nullable: false),
                    BudgetAmount = table.Column<decimal>(type: "REAL", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MeterReading = table.Column<decimal>(type: "REAL", nullable: true),
                    MeterReadDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousMeterReading = table.Column<decimal>(type: "REAL", nullable: true),
                    PreviousMeterReadDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enterprises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYearSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FiscalYearStartMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    FiscalYearStartDay = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OverallBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TotalMonthlyRevenue = table.Column<decimal>(type: "REAL", nullable: false),
                    TotalMonthlyExpenses = table.Column<decimal>(type: "REAL", nullable: false),
                    TotalMonthlyBalance = table.Column<decimal>(type: "REAL", nullable: false),
                    TotalCitizensServed = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageRatePerCitizen = table.Column<decimal>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverallBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UtilityCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomerType = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ServiceCity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ServiceState = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    ServiceZipCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    MailingAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MailingCity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MailingState = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    MailingZipCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 15, nullable: true),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MeterNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ServiceLocation = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Active"),
                    AccountOpenDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    AccountCloseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "REAL", nullable: false, defaultValue: 0m),
                    TaxId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BusinessLicenseNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ConnectDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DisconnectDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPaymentAmount = table.Column<decimal>(type: "REAL", nullable: false),
                    LastPaymentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityCustomers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContactInfo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Widgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "REAL", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SKU = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Widgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MunicipalAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FundClass = table.Column<string>(type: "TEXT", nullable: false),
                    DepartmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    BudgetPeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Fund = table.Column<string>(type: "TEXT", nullable: false),
                    Balance = table.Column<decimal>(type: "REAL", nullable: false, defaultValue: 0m),
                    BudgetAmount = table.Column<decimal>(type: "REAL", nullable: false, defaultValue: 0m),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    QuickBooksId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LastSyncDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MunicipalAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId",
                        column: x => x.BudgetPeriodId,
                        principalTable: BudgetPeriodsTable,
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
                name: "BudgetInteractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrimaryEnterpriseId = table.Column<int>(type: "INTEGER", nullable: false),
                    SecondaryEnterpriseId = table.Column<int>(type: "INTEGER", nullable: true),
                    InteractionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "REAL", nullable: false),
                    InteractionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsCost = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetInteractions_Enterprises_PrimaryEnterpriseId",
                        column: x => x.PrimaryEnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetInteractions_Enterprises_SecondaryEnterpriseId",
                        column: x => x.SecondaryEnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MunicipalAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    BudgetPeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                    YearType = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryType = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "REAL", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetEntries_BudgetPeriods_BudgetPeriodId",
                        column: x => x.BudgetPeriodId,
                        principalTable: BudgetPeriodsTable,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                        column: x => x.MunicipalAccountId,
                        principalTable: "MunicipalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VendorId = table.Column<int>(type: "INTEGER", nullable: false),
                    MunicipalAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "REAL", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsPaid = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                        column: x => x.MunicipalAccountId,
                        principalTable: "MunicipalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invoices_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MunicipalAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "REAL", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                        column: x => x.MunicipalAccountId,
                        principalTable: "MunicipalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_BudgetPeriodId",
                table: "BudgetEntries",
                column: "BudgetPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_MunicipalAccountId",
                table: "BudgetEntries",
                column: "MunicipalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteractions_InteractionType",
                table: "BudgetInteractions",
                column: "InteractionType");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteractions_PrimaryEnterpriseId",
                table: "BudgetInteractions",
                column: "PrimaryEnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteractions_SecondaryEnterpriseId",
                table: "BudgetInteractions",
                column: "SecondaryEnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Status",
                table: BudgetPeriodsTable,
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Year",
                table: BudgetPeriodsTable,
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Year_Status",
                table: BudgetPeriodsTable,
                columns: new[] { "Year", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Fund",
                table: "Departments",
                column: "Fund");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ParentDepartmentId",
                table: "Departments",
                column: "ParentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Enterprises_Name",
                table: "Enterprises",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_MunicipalAccountId",
                table: "Invoices",
                column: "MunicipalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_VendorId",
                table: "Invoices",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_AccountNumber",
                table: "MunicipalAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_BudgetPeriodId",
                table: "MunicipalAccounts",
                column: "BudgetPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_DepartmentId",
                table: "MunicipalAccounts",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_Fund",
                table: "MunicipalAccounts",
                column: "Fund");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_IsActive",
                table: "MunicipalAccounts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_QuickBooksId",
                table: "MunicipalAccounts",
                column: "QuickBooksId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_Type",
                table: "MunicipalAccounts",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_OverallBudgets_IsCurrent",
                table: "OverallBudgets",
                column: "IsCurrent",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OverallBudgets_SnapshotDate",
                table: "OverallBudgets",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_AccountNumber",
                table: "UtilityCustomers",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_CustomerType",
                table: "UtilityCustomers",
                column: "CustomerType");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_EmailAddress",
                table: "UtilityCustomers",
                column: "EmailAddress");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_MeterNumber",
                table: "UtilityCustomers",
                column: "MeterNumber");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_ServiceLocation",
                table: "UtilityCustomers",
                column: "ServiceLocation");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_Status",
                table: "UtilityCustomers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Widgets_Category",
                table: "Widgets",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Widgets_CreatedDate",
                table: "Widgets",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Widgets_IsActive",
                table: "Widgets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Widgets_Name",
                table: "Widgets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Widgets_SKU",
                table: "Widgets",
                column: "SKU",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "BudgetEntries");

            migrationBuilder.DropTable(
                name: "BudgetInteractions");

            migrationBuilder.DropTable(
                name: "FiscalYearSettings");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "OverallBudgets");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "UtilityCustomers");

            migrationBuilder.DropTable(
                name: "Widgets");

            migrationBuilder.DropTable(
                name: "Enterprises");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropTable(
                name: "MunicipalAccounts");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: BudgetPeriodsTable);
        }
    }
}
