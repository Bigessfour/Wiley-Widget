using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuickBooksConflictPolicy",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QuickBooksSyncConflicts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuickBooksInvoiceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocalTransactionId = table.Column<int>(type: "int", nullable: true),
                    RemoteAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    LocalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 19, scale: 4, nullable: false),
                    Policy = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuickBooksSyncConflicts", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "QuickBooksConflictPolicy",
                value: 0);

            migrationBuilder.CreateIndex(
                name: "IX_QuickBooksSyncConflicts_QuickBooksInvoiceId",
                table: "QuickBooksSyncConflicts",
                column: "QuickBooksInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_QuickBooksSyncConflicts_Status",
                table: "QuickBooksSyncConflicts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuickBooksSyncConflicts");

            migrationBuilder.DropColumn(
                name: "QuickBooksConflictPolicy",
                table: "AppSettings");
        }
    }
}
