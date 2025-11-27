using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixBudgetEntrySelfReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries",
                column: "ParentId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries",
                column: "ParentId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
