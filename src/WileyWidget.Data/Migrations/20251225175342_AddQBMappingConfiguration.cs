using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQBMappingConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuickBooksId",
                table: "Transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuickBooksInvoiceId",
                table: "Transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Transactions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });

            migrationBuilder.AddColumn<string>(
                name: "DepartmentCode",
                table: "BudgetEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts",
                type: "nvarchar(max)",
                nullable: true,
                computedColumnSql: "[AccountNumber]",
                stored: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComputedColumnSql: "[AccountNumber]",
                oldStored: null);

            migrationBuilder.CreateTable(
                name: "AIInsights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Response = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActioned = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnterpriseId = table.Column<int>(type: "int", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIInsights", x => x.Id);
                });

            // NOTE: `DepartmentCurrentCharges` already exists in the target database.
            // Migration intentionally skips creating this table to avoid conflicts.

            // `DepartmentGoals` already exists in the target database; skip creation.

            migrationBuilder.CreateTable(
                name: "QBMappingConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QBEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QBEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QBEntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BudgetEntryId = table.Column<int>(type: "int", nullable: false),
                    MappingStrategy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QBMappingConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QBMappingConfigurations_BudgetEntries_BudgetEntryId",
                        column: x => x.BudgetEntryId,
                        principalTable: "BudgetEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 1,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 3,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 4,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 5,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 6,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 7,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 8,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 9,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 10,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 11,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 12,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 13,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 14,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 15,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 16,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 17,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 18,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 19,
                column: "DepartmentCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 20,
                column: "DepartmentCode",
                value: null);

            // Indexes for `DepartmentCurrentCharges` are skipped because the table
            // already exists in the database.

            // Index for `DepartmentGoals` skipped because the table already exists.

            migrationBuilder.CreateIndex(
                name: "IX_QBMappingConfigurations_BudgetEntryId",
                table: "QBMappingConfigurations",
                column: "BudgetEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIInsights");

            // Skipping drop of `DepartmentCurrentCharges` in Down() because the
            // table pre-existed this migration and should not be removed.

            // Skipping drop of `DepartmentGoals` in Down() because the table
            // pre-existed this migration and should not be removed.

            migrationBuilder.DropTable(
                name: "QBMappingConfigurations");

            migrationBuilder.DropColumn(
                name: "QuickBooksId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "QuickBooksInvoiceId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DepartmentCode",
                table: "BudgetEntries");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts",
                type: "nvarchar(max)",
                nullable: true,
                computedColumnSql: "[AccountNumber]",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComputedColumnSql: "[AccountNumber]");
        }
    }
}
