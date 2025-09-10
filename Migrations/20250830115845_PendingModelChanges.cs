using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QboAccountRef",
                table: "BudgetInteractions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QboAccountRef",
                table: "BudgetInteractions");
        }
    }
}
