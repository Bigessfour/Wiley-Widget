using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQboClientColumnsToAppSettings : Migration
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

            migrationBuilder.DropPrimaryKey(
                name: "PK_Invoices",
                table: "Invoices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPeriods",
                table: "BudgetPeriods");

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Funds",
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

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.RenameTable(
                name: "Invoice",
                newName: "Invoices");

            migrationBuilder.RenameTable(
                name: "BudgetPeriod",
                newName: "BudgetPeriods");

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

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "MunicipalAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddColumn<int>(
                name: "EnterpriseId",
                table: "BudgetInteraction",
                type: "int",
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

            migrationBuilder.AddColumn<string>(
                name: "QboClientId",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QboClientSecret",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts",
                type: "nvarchar(max)",
                nullable: true,
                computedColumnSql: "[AccountNumber]",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

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
                name: "IX_Vendor_IsActive",
                table: "Vendor",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Vendor_Name",
                table: "Vendor",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_Fund_Type",
                table: "MunicipalAccounts",
                columns: new[] { "Fund", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteraction_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId");

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
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
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
                name: "IX_Vendor_IsActive",
                table: "Vendor");

            migrationBuilder.DropIndex(
                name: "IX_Vendor_Name",
                table: "Vendor");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_Fund_Type",
                table: "MunicipalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BudgetInteraction_EnterpriseId",
                table: "BudgetInteraction");

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
                name: "AccountNumber",
                table: "MunicipalAccounts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Enterprises");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Enterprises");

            migrationBuilder.DropColumn(
                name: "EnterpriseId",
                table: "BudgetInteraction");

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

            migrationBuilder.DropColumn(
                name: "QboClientId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "QboClientSecret",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Invoices");

            migrationBuilder.RenameTable(
                name: "Invoices",
                newName: "Invoice");

            migrationBuilder.RenameTable(
                name: "BudgetPeriods",
                newName: "BudgetPeriod");

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
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComputedColumnSql: "[AccountNumber]");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Invoice",
                table: "Invoice",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPeriods",
                table: "BudgetPeriods",
                column: "Id");

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
                name: "FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId",
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
