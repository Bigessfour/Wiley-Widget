using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    public partial class AddQuickBooksConflictPolicy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add as int with default value 0 (PreferQBO) if not present
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='AppSettings' AND COLUMN_NAME='QuickBooksConflictPolicy') ALTER TABLE AppSettings ADD QuickBooksConflictPolicy int NOT NULL DEFAULT(0);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='AppSettings' AND COLUMN_NAME='QuickBooksConflictPolicy') ALTER TABLE AppSettings DROP COLUMN QuickBooksConflictPolicy;");
        }
    }
}
