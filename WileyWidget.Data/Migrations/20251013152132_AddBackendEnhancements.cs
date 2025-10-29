using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBackendEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_BudgetPeriods_BudgetPeriodId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteractions_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteractions");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteractions_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteractions");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Departments_ParentDepartmentId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Vendors_VendorId",
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
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "OverallBudgets");

            migrationBuilder.DropTable(
                name: "Widgets");

            migrationBuilder.DropIndex(
                name: "IX_UtilityCustomers_AccountNumber",
                table: "UtilityCustomers");

            migrationBuilder.DropIndex(
                name: "IX_UtilityCustomers_CustomerType",
                table: "UtilityCustomers");

            migrationBuilder.DropIndex(
                name: "IX_UtilityCustomers_EmailAddress",
                table: "UtilityCustomers");

            migrationBuilder.DropIndex(
                name: "IX_UtilityCustomers_MeterNumber",
                table: "UtilityCustomers");

            migrationBuilder.DropIndex(
                name: "IX_UtilityCustomers_ServiceLocation",
                table: "UtilityCustomers");

            migrationBuilder.DropIndex(
                name: "IX_UtilityCustomers_Status",
                table: "UtilityCustomers");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_AccountNumber",
                table: "MunicipalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_Fund",
                table: "MunicipalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_IsActive",
                table: "MunicipalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_QuickBooksId",
                table: "MunicipalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_Type",
                table: "MunicipalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Enterprises_Name",
                table: "Enterprises");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Code",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Fund",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Name",
                table: "Departments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Vendors",
                table: "Vendors");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Invoices",
                table: "Invoices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPeriods",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_Status",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_Year",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_Year_Status",
                table: "BudgetPeriods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetInteractions",
                table: "BudgetInteractions");

            migrationBuilder.DropIndex(
                name: "IX_BudgetInteractions_InteractionType",
                table: "BudgetInteractions");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "MunicipalAccounts");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "Fund",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "BudgetEntries");

            migrationBuilder.RenameTable(
                name: "Vendors",
                newName: "Vendor");

            migrationBuilder.RenameTable(
                name: "Invoices",
                newName: "Invoice");

            migrationBuilder.RenameTable(
                name: "BudgetPeriods",
                newName: "BudgetPeriod");

            migrationBuilder.RenameTable(
                name: "BudgetInteractions",
                newName: "BudgetInteraction");

            migrationBuilder.RenameColumn(
                name: "ParentDepartmentId",
                table: "Departments",
                newName: "ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_Departments_ParentDepartmentId",
                table: "Departments",
                newName: "IX_Departments_ParentId");

            migrationBuilder.RenameColumn(
                name: "YearType",
                table: "BudgetEntries",
                newName: "FundType");

            migrationBuilder.RenameColumn(
                name: "EntryType",
                table: "BudgetEntries",
                newName: "FiscalYear");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "BudgetEntries",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "BudgetPeriodId",
                table: "BudgetEntries",
                newName: "DepartmentId");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "BudgetEntries",
                newName: "Variance");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetEntries_BudgetPeriodId",
                table: "BudgetEntries",
                newName: "IX_BudgetEntries_DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_VendorId",
                table: "Invoice",
                newName: "IX_Invoice_VendorId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_MunicipalAccountId",
                table: "Invoice",
                newName: "IX_Invoice_MunicipalAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetInteractions_SecondaryEnterpriseId",
                table: "BudgetInteraction",
                newName: "IX_BudgetInteraction_SecondaryEnterpriseId");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetInteractions_PrimaryEnterpriseId",
                table: "BudgetInteraction",
                newName: "IX_BudgetInteraction_PrimaryEnterpriseId");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "UtilityCustomers",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceLocation",
                table: "UtilityCustomers",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                table: "UtilityCustomers",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerType",
                table: "UtilityCustomers",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentBalance",
                table: "UtilityCustomers",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "UtilityCustomers",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AccountOpenDate",
                table: "UtilityCustomers",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "MunicipalAccountId",
                table: "Transactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "BudgetEntryId",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Transactions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "MunicipalAccounts",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "MunicipalAccounts",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<int>(
                name: "FundClass",
                table: "MunicipalAccounts",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "Fund",
                table: "MunicipalAccounts",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<decimal>(
                name: "BudgetAmount",
                table: "MunicipalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "MunicipalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentCode",
                table: "Departments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MunicipalAccountId",
                table: "BudgetEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "BudgetEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActivityCode",
                table: "BudgetEntries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualAmount",
                table: "BudgetEntries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetedAmount",
                table: "BudgetEntries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "BudgetEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "EncumbranceAmount",
                table: "BudgetEntries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndPeriod",
                table: "BudgetEntries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FundId",
                table: "BudgetEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGASBCompliant",
                table: "BudgetEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "BudgetEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFilePath",
                table: "BudgetEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceRowNumber",
                table: "BudgetEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartPeriod",
                table: "BudgetEntries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BudgetEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "BudgetPeriod",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsCost",
                table: "BudgetInteraction",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Vendor",
                table: "Vendor",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Invoice",
                table: "Invoice",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPeriod",
                table: "BudgetPeriod",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetInteraction",
                table: "BudgetInteraction",
                column: "Id");

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

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "DepartmentCode", "Name", "ParentId" },
                values: new object[] { 1, "DPW", "Public Works", null });

            migrationBuilder.InsertData(
                table: "Funds",
                columns: new[] { "Id", "FundCode", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "100", "General Fund", 1 },
                    { 2, "200", "Utility Fund", 2 }
                });

            migrationBuilder.InsertData(
                table: "BudgetEntries",
                columns: new[] { "Id", "AccountNumber", "ActivityCode", "ActualAmount", "BudgetedAmount", "CreatedAt", "DepartmentId", "Description", "EncumbranceAmount", "EndPeriod", "FiscalYear", "FundId", "FundType", "IsGASBCompliant", "MunicipalAccountId", "ParentId", "SourceFilePath", "SourceRowNumber", "StartPeriod", "UpdatedAt", "Variance" },
                values: new object[] { 1, "405", "GOV", 0m, 50000m, new DateTime(2025, 10, 13, 15, 21, 30, 809, DateTimeKind.Utc).AddTicks(1044), 1, "Road Maintenance", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 0, true, null, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0m });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "DepartmentCode", "Name", "ParentId" },
                values: new object[] { 2, "SAN", "Sanitation", 1 });

            migrationBuilder.InsertData(
                table: "BudgetEntries",
                columns: new[] { "Id", "AccountNumber", "ActivityCode", "ActualAmount", "BudgetedAmount", "CreatedAt", "DepartmentId", "Description", "EncumbranceAmount", "EndPeriod", "FiscalYear", "FundId", "FundType", "IsGASBCompliant", "MunicipalAccountId", "ParentId", "SourceFilePath", "SourceRowNumber", "StartPeriod", "UpdatedAt", "Variance" },
                values: new object[] { 2, "405.1", "GOV", 0m, 20000m, new DateTime(2025, 10, 13, 15, 21, 30, 809, DateTimeKind.Utc).AddTicks(2333), 1, "Paving", 0m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2026, 1, 0, true, null, 1, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0m });

            migrationBuilder.InsertData(
                table: "Transactions",
                columns: new[] { "Id", "Amount", "BudgetEntryId", "CreatedAt", "Description", "MunicipalAccountId", "TransactionDate", "Type", "UpdatedAt" },
                values: new object[] { 1, 10000m, 1, new DateTime(2025, 10, 13, 15, 21, 30, 809, DateTimeKind.Utc).AddTicks(3448), "Initial payment for road work", null, new DateTime(2025, 10, 13, 15, 21, 30, 809, DateTimeKind.Utc).AddTicks(4078), "Payment", null });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BudgetEntryId",
                table: "Transactions",
                column: "BudgetEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate",
                table: "Transactions",
                column: "TransactionDate");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transaction_NonZero",
                table: "Transactions",
                sql: "[Amount] != 0");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DepartmentCode",
                table: "Departments",
                column: "DepartmentCode",
                unique: true,
                filter: "[DepartmentCode] IS NOT NULL");

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
                name: "IX_BudgetEntries_FundId",
                table: "BudgetEntries",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_ParentId",
                table: "BudgetEntries",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_SourceRowNumber",
                table: "BudgetEntries",
                column: "SourceRowNumber");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Budget_Positive",
                table: "BudgetEntries",
                sql: "[BudgetedAmount] > 0");

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
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Departments_ParentId",
                table: "Departments",
                column: "ParentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

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
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Departments_ParentId",
                table: "Departments");

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

            migrationBuilder.DropTable(
                name: "Funds");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BudgetEntryId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransactionDate",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transaction_NonZero",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Departments_DepartmentCode",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_AccountNumber_FiscalYear",
                table: "BudgetEntries");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_ActivityCode",
                table: "BudgetEntries");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_FundId",
                table: "BudgetEntries");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_ParentId",
                table: "BudgetEntries");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_SourceRowNumber",
                table: "BudgetEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Budget_Positive",
                table: "BudgetEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Vendor",
                table: "Vendor");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Invoice",
                table: "Invoice");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPeriod",
                table: "BudgetPeriod");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetInteraction",
                table: "BudgetInteraction");

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "BudgetEntryId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts");

            migrationBuilder.DropColumn(
                name: "DepartmentCode",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "ActivityCode",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "ActualAmount",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "BudgetedAmount",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "EncumbranceAmount",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "EndPeriod",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "FundId",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "IsGASBCompliant",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "SourceFilePath",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "SourceRowNumber",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "StartPeriod",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BudgetEntries");

            migrationBuilder.RenameTable(
                name: "Vendor",
                newName: "Vendors");

            migrationBuilder.RenameTable(
                name: "Invoice",
                newName: "Invoices");

            migrationBuilder.RenameTable(
                name: "BudgetPeriod",
                newName: "BudgetPeriods");

            migrationBuilder.RenameTable(
                name: "BudgetInteraction",
                newName: "BudgetInteractions");

            migrationBuilder.RenameColumn(
                name: "ParentId",
                table: "Departments",
                newName: "ParentDepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Departments_ParentId",
                table: "Departments",
                newName: "IX_Departments_ParentDepartmentId");

            migrationBuilder.RenameColumn(
                name: "Variance",
                table: "BudgetEntries",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "FundType",
                table: "BudgetEntries",
                newName: "YearType");

            migrationBuilder.RenameColumn(
                name: "FiscalYear",
                table: "BudgetEntries",
                newName: "EntryType");

            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "BudgetEntries",
                newName: "BudgetPeriodId");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "BudgetEntries",
                newName: "CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetEntries_DepartmentId",
                table: "BudgetEntries",
                newName: "IX_BudgetEntries_BudgetPeriodId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoice_VendorId",
                table: "Invoices",
                newName: "IX_Invoices_VendorId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoice_MunicipalAccountId",
                table: "Invoices",
                newName: "IX_Invoices_MunicipalAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetInteraction_SecondaryEnterpriseId",
                table: "BudgetInteractions",
                newName: "IX_BudgetInteractions_SecondaryEnterpriseId");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetInteraction_PrimaryEnterpriseId",
                table: "BudgetInteractions",
                newName: "IX_BudgetInteractions_PrimaryEnterpriseId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "UtilityCustomers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceLocation",
                table: "UtilityCustomers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                table: "UtilityCustomers",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerType",
                table: "UtilityCustomers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentBalance",
                table: "UtilityCustomers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "UtilityCustomers",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AccountOpenDate",
                table: "UtilityCustomers",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Transactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "MunicipalAccountId",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "MunicipalAccounts",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "MunicipalAccounts",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "FundClass",
                table: "MunicipalAccounts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Fund",
                table: "MunicipalAccounts",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "BudgetAmount",
                table: "MunicipalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "MunicipalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "MunicipalAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Departments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Fund",
                table: "Departments",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "MunicipalAccountId",
                table: "BudgetEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "BudgetEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "BudgetPeriods",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "IsCost",
                table: "BudgetInteractions",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Vendors",
                table: "Vendors",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Invoices",
                table: "Invoices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPeriods",
                table: "BudgetPeriods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetInteractions",
                table: "BudgetInteractions",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "OverallBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AverageRatePerCitizen = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TotalCitizensServed = table.Column<int>(type: "int", nullable: false),
                    TotalMonthlyBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalMonthlyExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalMonthlyRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverallBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Widgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SKU = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Widgets", x => x.Id);
                });

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
                name: "IX_MunicipalAccounts_AccountNumber",
                table: "MunicipalAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_Fund",
                table: "MunicipalAccounts",
                column: "Fund");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_IsActive",
                table: "MunicipalAccounts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_QuickBooksId",
                table: "MunicipalAccounts",
                column: "QuickBooksId");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_Type",
                table: "MunicipalAccounts",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Enterprises_Name",
                table: "Enterprises",
                column: "Name",
                unique: true);

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
                name: "IX_BudgetPeriods_Status",
                table: "BudgetPeriods",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Year",
                table: "BudgetPeriods",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Year_Status",
                table: "BudgetPeriods",
                columns: new[] { "Year", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteractions_InteractionType",
                table: "BudgetInteractions",
                column: "InteractionType");

            migrationBuilder.CreateIndex(
                name: "IX_OverallBudgets_IsCurrent",
                table: "OverallBudgets",
                column: "IsCurrent",
                unique: true,
                filter: "IsCurrent = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OverallBudgets_SnapshotDate",
                table: "OverallBudgets",
                column: "SnapshotDate");

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
                unique: true,
                filter: "[SKU] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_BudgetPeriods_BudgetPeriodId",
                table: "BudgetEntries",
                column: "BudgetPeriodId",
                principalTable: "BudgetPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteractions_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteractions",
                column: "PrimaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteractions_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteractions",
                column: "SecondaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Departments_ParentDepartmentId",
                table: "Departments",
                column: "ParentDepartmentId",
                principalTable: "Departments",
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
                name: "FK_Invoices_Vendors_VendorId",
                table: "Invoices",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
