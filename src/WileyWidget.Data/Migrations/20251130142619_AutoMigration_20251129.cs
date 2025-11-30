using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AutoMigration_20251129 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Funds",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FundCode",
                table: "Funds",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Funds",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Funds",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Funds",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Funds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDebit = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartOfAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FundId = table.Column<int>(type: "int", nullable: false),
                    AccountTypeId = table.Column<int>(type: "int", nullable: false),
                    ParentAccountId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartOfAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChartOfAccounts_AccountTypes_AccountTypeId",
                        column: x => x.AccountTypeId,
                        principalTable: "AccountTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChartOfAccounts_ChartOfAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChartOfAccounts_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AccountTypes",
                columns: new[] { "Id", "CreatedAt", "Description", "IsDebit", "TypeName" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Revenue and income accounts", false, "Income" },
                    { 2, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expenditure and expense accounts", true, "Expense" },
                    { 3, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Asset accounts", true, "Asset" },
                    { 4, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Liability accounts", false, "Liability" },
                    { 5, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Equity and fund balance accounts", false, "Equity" },
                    { 6, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Cost of goods sold accounts", true, "Cost of Goods Sold" },
                    { 7, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bank and cash accounts", true, "Bank" },
                    { 8, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Accounts receivable", true, "Accounts Receivable" },
                    { 9, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Other current assets", true, "Other Current Asset" },
                    { 10, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fixed assets", true, "Fixed Asset" },
                    { 11, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Other assets", true, "Other Asset" },
                    { 12, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Accounts payable", false, "Accounts Payable" },
                    { 13, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Credit card accounts", false, "Credit Card" },
                    { 14, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Other current liabilities", false, "Other Current Liability" },
                    { 15, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Long term liabilities", false, "Long Term Liability" }
                });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Description", "FundCode", "IsActive", "Name" },
                values: new object[] { new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Town of Wiley General Operating Fund", "TOWN-GENERAL", true, "Town General Fund" });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "Description", "FundCode", "IsActive", "Name", "Type" },
                values: new object[] { new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Wiley Recreation Fund", "WILEY-REC", true, "Wiley Rec", 3 });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "Description", "FundCode", "IsActive", "Name", "Type" },
                values: new object[] { new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Conservation Trust Fund", "CONSERV-TRUST", true, "Conservation Trust Fund", 6 });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "Description", "FundCode", "IsActive", "Name", "Type" },
                values: new object[] { new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Wiley Sanitation District - General Fund", "WSD-GENERAL", true, "WSD General", 2 });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "Description", "FundCode", "IsActive", "Name", "Type" },
                values: new object[] { new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Wiley Community Center Fund", "WILEY-CC", true, "Wiley Community Center", 3 });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "Description", "FundCode", "IsActive", "Name", "Type" },
                values: new object[] { new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Wiley Housing Authority - Brookside", "WHA-BROOKSIDE", true, "WHA Brookside", 2 });

            migrationBuilder.InsertData(
                table: "ChartOfAccounts",
                columns: new[] { "Id", "AccountName", "AccountNumber", "AccountTypeId", "CreatedAt", "FundId", "IsActive", "ParentAccountId" },
                values: new object[,]
                {
                    { 1, "GENERAL REVENUES", "300", 1, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 2, "PROPERTY TAX", "301", 1, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 3, "SALES TAX", "304", 1, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 4, "INTEREST REVENUE", "315", 1, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 5, "MISCELLANEOUS", "320", 1, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 6, "ADMINISTRATION", "400", 2, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 7, "SALARIES EMPLOYEES", "401", 2, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 8, "INSURANCE", "405", 2, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 9, "UTILITIES", "422", 2, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null },
                    { 10, "CAPITAL OUTLAY", "495", 2, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Funds_FundCode",
                table: "Funds",
                column: "FundCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypes_TypeName",
                table: "AccountTypes",
                column: "TypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountNumber_FundId",
                table: "ChartOfAccounts",
                columns: new[] { "AccountNumber", "FundId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountTypeId",
                table: "ChartOfAccounts",
                column: "AccountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_FundId",
                table: "ChartOfAccounts",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_ParentAccountId",
                table: "ChartOfAccounts",
                column: "ParentAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartOfAccounts");

            migrationBuilder.DropTable(
                name: "AccountTypes");

            migrationBuilder.DropIndex(
                name: "IX_Funds_FundCode",
                table: "Funds");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Funds");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Funds");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Funds");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Funds");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Funds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "FundCode",
                table: "Funds",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FundCode", "Name" },
                values: new object[] { "100-GEN", "General Fund" });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FundCode", "Name", "Type" },
                values: new object[] { "200-ENT", "Enterprise Fund", 2 });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FundCode", "Name", "Type" },
                values: new object[] { "300-UTIL", "Utility Fund", 2 });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "FundCode", "Name", "Type" },
                values: new object[] { "400-COMM", "Community Center Fund", 3 });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "FundCode", "Name", "Type" },
                values: new object[] { "500-CONS", "Conservation Trust Fund", 6 });

            migrationBuilder.UpdateData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "FundCode", "Name", "Type" },
                values: new object[] { "600-REC", "Recreation Fund", 3 });
        }
    }
}
