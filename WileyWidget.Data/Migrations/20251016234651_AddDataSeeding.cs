using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_MunicipalAccounts_MunicipalAccountId",
                table: "Invoice");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_Vendor_VendorId",
                table: "Invoice");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_BudgetPeriod_BudgetPeriodId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_Departments_DepartmentId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Invoice",
                table: "Invoice");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPeriod",
                table: "BudgetPeriod");

            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.Sql(@"
DELETE FROM [Transactions] WHERE [Id] = 1;
DELETE FROM [BudgetEntries] WHERE [Id] IN (1, 2, 3, 4);
DELETE FROM [MunicipalAccounts] WHERE [Id] IN (1, 2);
DELETE FROM [UtilityCustomers] WHERE [Id] IN (1, 2);
DELETE FROM [BudgetPeriods] WHERE [Id] = 1;
DELETE FROM [Departments] WHERE [Id] IN (3, 2, 1);
DELETE FROM [Funds] WHERE [Id] IN (2, 1);
");

            migrationBuilder.RenameTable(
                name: "Invoice",
                newName: "Invoices");

            migrationBuilder.RenameTable(
                name: "BudgetPeriod",
                newName: "BudgetPeriods");

            migrationBuilder.RenameColumn(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts",
                newName: "AccountNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Invoice_VendorId",
                table: "Invoices",
                newName: "IX_Invoices_VendorId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoice_MunicipalAccountId",
                table: "Invoices",
                newName: "IX_Invoices_MunicipalAccountId");

            migrationBuilder.AlterColumn<int>(
                name: "FundClass",
                table: "MunicipalAccounts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "MunicipalAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Enterprises",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Enterprises",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeChartsInReports",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReportEndDate",
                table: "AppSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReportStartDate",
                table: "AppSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSelectedEnterpriseId",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastSelectedFormat",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSelectedReportType",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Invoices",
                table: "Invoices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPeriods",
                table: "BudgetPeriods",
                column: "Id");

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

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_Fund_Type",
                table: "MunicipalAccounts",
                columns: new[] { "Fund", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

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

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries",
                column: "ParentId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction",
                column: "PrimaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction",
                column: "SecondaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Vendor_VendorId",
                table: "Invoices",
                column: "VendorId",
                principalTable: "Vendor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId",
                table: "MunicipalAccounts",
                column: "BudgetPeriodId",
                principalTable: "BudgetPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_Departments_DepartmentId",
                table: "MunicipalAccounts",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts",
                column: "ParentAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                table: "Transactions",
                column: "BudgetEntryId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.InsertData(
                table: "Enterprises",
                columns: new[]
                {
                    "Id",
                    "Name",
                    "Description",
                    "CurrentRate",
                    "MonthlyExpenses",
                    "CitizenCount",
                    "TotalBudget",
                    "BudgetAmount",
                    "Status",
                    "Type",
                    "Notes",
                    "CreatedBy",
                    "CreatedDate",
                    "ModifiedBy",
                    "ModifiedDate",
                    "DeletedBy",
                    "DeletedDate",
                    "IsDeleted",
                    "MeterReading",
                    "MeterReadDate",
                    "PreviousMeterReading",
                    "PreviousMeterReadDate",
                    "CreatedAt",
                    "UpdatedAt"
                },
                values: new object[,]
                {
                    {
                        1,
                        "Town of Wiley",
                        "Municipal government for Wiley, CO (pop ~300)",
                        8.50m,
                        0m,
                        300,
                        2500000m,
                        285755.00m,
                        0,
                        "General",
                        null,
                        null,
                        new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        null,
                        new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        null,
                        null,
                        false,
                        null,
                        null,
                        null,
                        null,
                        new DateTime(2025, 10, 16, 0, 0, 0, DateTimeKind.Utc),
                        null
                    },
                    {
                        2,
                        "Wiley Sanitation District",
                        "Sanitation services for Wiley area",
                        38.00m,
                        0m,
                        250,
                        1500000m,
                        5879527.00m,
                        0,
                        "Sanitation",
                        null,
                        null,
                        new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        null,
                        new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        null,
                        null,
                        false,
                        null,
                        null,
                        null,
                        null,
                        new DateTime(2025, 10, 16, 0, 0, 0, DateTimeKind.Utc),
                        null
                    },
                    {
                        3,
                        "Wiley Electric Cooperative",
                        "Electric utility provider for Wiley community",
                        0.12m,
                        0m,
                        180,
                        1200000m,
                        285755.00m,
                        0,
                        "Electric",
                        null,
                        null,
                        new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        null,
                        new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        null,
                        null,
                        false,
                        null,
                        null,
                        null,
                        null,
                        new DateTime(2025, 10, 16, 0, 0, 0, DateTimeKind.Utc),
                        null
                    }
                });

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Funds] ON;

IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 1)
    INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type]) VALUES (1, '100', 'General Fund', 1);

IF NOT EXISTS (SELECT 1 FROM [Funds] WHERE [Id] = 2)
    INSERT INTO [Funds] ([Id], [FundCode], [Name], [Type]) VALUES (2, '200', 'Utility Fund', 2);

SET IDENTITY_INSERT [Funds] OFF;
");

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Departments] ON;

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 1)
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId]) VALUES (1, 'GEN', 'General Government', NULL);

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 2)
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId]) VALUES (2, 'PW', 'Public Works', NULL);

IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = 3)
    INSERT INTO [Departments] ([Id], [DepartmentCode], [Name], [ParentId]) VALUES (3, 'SAN', 'Sanitation', 2);

SET IDENTITY_INSERT [Departments] OFF;
");

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [BudgetPeriods] ON;

IF NOT EXISTS (SELECT 1 FROM [BudgetPeriods] WHERE [Id] = 1)
    INSERT INTO [BudgetPeriods] ([Id], [Year], [Name], [CreatedDate], [Status], [StartDate], [EndDate], [IsActive])
    VALUES (1, 2026, 'FY 2026 Budget', '2025-01-01T00:00:00', 2, '2026-01-01T00:00:00', '2026-12-31T00:00:00', 1);

SET IDENTITY_INSERT [BudgetPeriods] OFF;
");

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [UtilityCustomers] ON;

IF NOT EXISTS (SELECT 1 FROM [UtilityCustomers] WHERE [Id] = 1)
    INSERT INTO [UtilityCustomers] (
        [Id], [AccountNumber], [FirstName], [LastName], [CompanyName], [CustomerType],
        [ServiceAddress], [ServiceCity], [ServiceState], [ServiceZipCode],
        [MailingAddress], [MailingCity], [MailingState], [MailingZipCode],
        [PhoneNumber], [EmailAddress], [MeterNumber], [ServiceLocation], [Status],
        [AccountOpenDate], [AccountCloseDate], [CurrentBalance], [TaxId], [BusinessLicenseNumber], [Notes],
        [ConnectDate], [DisconnectDate], [LastPaymentAmount], [LastPaymentDate], [CreatedDate], [LastModifiedDate]
    )
    VALUES (
        1, 'CUST001', 'John', 'Doe', NULL, 0,
        '123 Main St', 'Wiley', 'CO', '81092',
        NULL, NULL, NULL, NULL,
        '719-555-0100', 'john.doe@example.com', NULL, 0, 0,
        '2018-01-15T00:00:00', NULL, 45.67, NULL, NULL, NULL,
        '2018-01-15T00:00:00', NULL, 45.67, '2025-10-01T00:00:00', '2025-10-01T00:00:00', '2025-10-01T00:00:00'
    );

IF NOT EXISTS (SELECT 1 FROM [UtilityCustomers] WHERE [Id] = 2)
    INSERT INTO [UtilityCustomers] (
        [Id], [AccountNumber], [FirstName], [LastName], [CompanyName], [CustomerType],
        [ServiceAddress], [ServiceCity], [ServiceState], [ServiceZipCode],
        [MailingAddress], [MailingCity], [MailingState], [MailingZipCode],
        [PhoneNumber], [EmailAddress], [MeterNumber], [ServiceLocation], [Status],
        [AccountOpenDate], [AccountCloseDate], [CurrentBalance], [TaxId], [BusinessLicenseNumber], [Notes],
        [ConnectDate], [DisconnectDate], [LastPaymentAmount], [LastPaymentDate], [CreatedDate], [LastModifiedDate]
    )
    VALUES (
        2, 'CUST002', 'Jane', 'Smith', 'Jane Smith Business', 1,
        '456 Oak Ave', 'Wiley', 'CO', '81092',
        NULL, NULL, NULL, NULL,
        '719-555-0123', 'hello@janesmithbiz.com', NULL, 1, 0,
        '2016-05-10T00:00:00', NULL, 150.25, NULL, NULL, NULL,
        '2016-05-10T00:00:00', NULL, 150.25, '2025-09-15T00:00:00', '2025-09-15T00:00:00', '2025-09-15T00:00:00'
    );

SET IDENTITY_INSERT [UtilityCustomers] OFF;
");

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [MunicipalAccounts] ON;

IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 1)
    INSERT INTO [MunicipalAccounts] (
        [Id], [AccountNumber], [FundClass], [DepartmentId], [ParentAccountId], [BudgetPeriodId],
        [Name], [Type], [Fund], [Balance], [BudgetAmount], [IsActive], [QuickBooksId], [LastSyncDate], [Notes]
    )
    VALUES (
        1, '101.100', NULL, 1, NULL, 1,
        'General Fund Checking', 5, 0, 500000.00, 500000.00, 1, NULL, NULL, NULL
    );

IF NOT EXISTS (SELECT 1 FROM [MunicipalAccounts] WHERE [Id] = 2)
    INSERT INTO [MunicipalAccounts] (
        [Id], [AccountNumber], [FundClass], [DepartmentId], [ParentAccountId], [BudgetPeriodId],
        [Name], [Type], [Fund], [Balance], [BudgetAmount], [IsActive], [QuickBooksId], [LastSyncDate], [Notes]
    )
    VALUES (
        2, '201.100', NULL, 2, NULL, 1,
        'Enterprise Fund Checking', 5, 4, 200000.00, 200000.00, 1, NULL, NULL, NULL
    );

SET IDENTITY_INSERT [MunicipalAccounts] OFF;
");

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [BudgetEntries] ON;

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 1)
    INSERT INTO [BudgetEntries] (
        [Id], [AccountNumber], [Description], [BudgetedAmount], [ActualAmount], [Variance], [ParentId],
        [FiscalYear], [StartPeriod], [EndPeriod], [FundType], [EncumbranceAmount], [IsGASBCompliant],
        [DepartmentId], [FundId], [MunicipalAccountId], [SourceFilePath], [SourceRowNumber], [ActivityCode], [CreatedAt], [UpdatedAt]
    )
    VALUES (
        1, '110', 'CASH IN BANK', 100000.00, 95000.00, 5000.00, NULL,
        2026, '2026-01-01', '2026-12-31', 1, 0, 1,
        1, 1, 1, NULL, NULL, 'GOV', '2025-10-16T00:00:00', NULL
    );

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 2)
    INSERT INTO [BudgetEntries] (
        [Id], [AccountNumber], [Description], [BudgetedAmount], [ActualAmount], [Variance], [ParentId],
        [FiscalYear], [StartPeriod], [EndPeriod], [FundType], [EncumbranceAmount], [IsGASBCompliant],
        [DepartmentId], [FundId], [MunicipalAccountId], [SourceFilePath], [SourceRowNumber], [ActivityCode], [CreatedAt], [UpdatedAt]
    )
    VALUES (
        2, '310', 'STATE APPORTIONMENT', 50000.00, 45000.00, 5000.00, NULL,
        2026, '2026-01-01', '2026-12-31', 1, 0, 1,
        1, 1, 1, NULL, NULL, 'GOV', '2025-10-16T00:00:00', NULL
    );

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 3)
    INSERT INTO [BudgetEntries] (
        [Id], [AccountNumber], [Description], [BudgetedAmount], [ActualAmount], [Variance], [ParentId],
        [FiscalYear], [StartPeriod], [EndPeriod], [FundType], [EncumbranceAmount], [IsGASBCompliant],
        [DepartmentId], [FundId], [MunicipalAccountId], [SourceFilePath], [SourceRowNumber], [ActivityCode], [CreatedAt], [UpdatedAt]
    )
    VALUES (
        3, '410', 'CAPITAL IMP - BALL COMPLEX', 150000.00, 120000.00, 30000.00, NULL,
        2026, '2026-01-01', '2026-12-31', 1, 0, 1,
        1, 1, 1, NULL, NULL, 'CAP', '2025-10-16T00:00:00', NULL
    );

IF NOT EXISTS (SELECT 1 FROM [BudgetEntries] WHERE [Id] = 4)
    INSERT INTO [BudgetEntries] (
        [Id], [AccountNumber], [Description], [BudgetedAmount], [ActualAmount], [Variance], [ParentId],
        [FiscalYear], [StartPeriod], [EndPeriod], [FundType], [EncumbranceAmount], [IsGASBCompliant],
        [DepartmentId], [FundId], [MunicipalAccountId], [SourceFilePath], [SourceRowNumber], [ActivityCode], [CreatedAt], [UpdatedAt]
    )
    VALUES (
        4, '101', 'CHECKING ACCOUNT-Enterprise', 200000.00, 180000.00, 20000.00, NULL,
        2026, '2026-01-01', '2026-12-31', 2, 0, 1,
        2, 2, 2, NULL, NULL, 'ENT', '2025-10-16T00:00:00', NULL
    );

SET IDENTITY_INSERT [BudgetEntries] OFF;
");

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Transactions] ON;

IF NOT EXISTS (SELECT 1 FROM [Transactions] WHERE [Id] = 1)
    INSERT INTO [Transactions] (
        [Id], [MunicipalAccountId], [Amount], [Description], [TransactionDate], [Type], [BudgetEntryId], [CreatedAt], [UpdatedAt]
    )
    VALUES (
        1, 1, 10000.00, 'Initial payment for road work', '2025-10-10T00:00:00', 'Payment', 1, '2025-10-16T00:00:00', NULL
    );

SET IDENTITY_INSERT [Transactions] OFF;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Vendor_VendorId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_Departments_DepartmentId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_Fund_Type",
                table: "MunicipalAccounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Invoices",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPeriods",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_IsActive",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_Year",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_Year_Status",
                table: "BudgetPeriods");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Enterprises");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Enterprises");

            migrationBuilder.DropColumn(
                name: "IncludeChartsInReports",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastReportEndDate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastReportStartDate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastSelectedEnterpriseId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastSelectedFormat",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastSelectedReportType",
                table: "AppSettings");

            migrationBuilder.RenameTable(
                name: "Invoices",
                newName: "Invoice");

            migrationBuilder.RenameTable(
                name: "BudgetPeriods",
                newName: "BudgetPeriod");

            migrationBuilder.RenameColumn(
                name: "AccountNumber",
                table: "MunicipalAccounts",
                newName: "AccountNumber_Value");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_VendorId",
                table: "Invoice",
                newName: "IX_Invoice_VendorId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_MunicipalAccountId",
                table: "Invoice",
                newName: "IX_Invoice_MunicipalAccountId");

            migrationBuilder.AlterColumn<int>(
                name: "FundClass",
                table: "MunicipalAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Invoice",
                table: "Invoice",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPeriod",
                table: "BudgetPeriod",
                column: "Id");

            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.InsertData(
                table: "Enterprises",
                columns: new[] { "Id", "BudgetAmount", "CitizenCount", "CreatedBy", "CreatedDate", "CurrentRate", "DeletedBy", "DeletedDate", "Description", "IsDeleted", "LastModified", "MeterReadDate", "MeterReading", "ModifiedBy", "ModifiedDate", "MonthlyExpenses", "Name", "Notes", "PreviousMeterReadDate", "PreviousMeterReading", "Status", "TotalBudget", "Type" },
                values: new object[,]
                {
                    { 1, 285755.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 45.50m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Water Department", null, null, null, 0, 0m, "Water" },
                    { 2, 5879527.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 125.75m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Sewer Department", null, null, null, 0, 0m, "Sewer" },
                    { 3, 285755.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.12m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Electric Department", null, null, null, 0, 0m, "Electric" }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries",
                column: "ParentId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction",
                column: "PrimaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction",
                column: "SecondaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_MunicipalAccounts_MunicipalAccountId",
                table: "Invoice",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_Vendor_VendorId",
                table: "Invoice",
                column: "VendorId",
                principalTable: "Vendor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_BudgetPeriod_BudgetPeriodId",
                table: "MunicipalAccounts",
                column: "BudgetPeriodId",
                principalTable: "BudgetPeriod",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_Departments_DepartmentId",
                table: "MunicipalAccounts",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts",
                column: "ParentAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                table: "Transactions",
                column: "BudgetEntryId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id");
        }
    }
}
