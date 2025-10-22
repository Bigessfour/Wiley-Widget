using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Migrations
{
    /// <inheritdoc />
    public partial class AddQboClientColumnsToAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QboClientId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "QboClientSecret",
                table: "AppSettings");
        }
    }
}
