using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationFont",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TownOfWileyBudgetData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceFile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FundOrDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriorYearActual = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    SevenMonthActual = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    EstimateCurrentYr = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    BudgetYear = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    ActualYTD = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Remaining = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PercentOfBudget = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MappedDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TownOfWileyBudgetData", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "ApplicationFont",
                value: "Segoe UI, 9pt");

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TownOfWileyBudgetData");

            migrationBuilder.DropColumn(
                name: "ApplicationFont",
                table: "AppSettings");

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "EndPeriod", "StartPeriod" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });
        }
    }
}
