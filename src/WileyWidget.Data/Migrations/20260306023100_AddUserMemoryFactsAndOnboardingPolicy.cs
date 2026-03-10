using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMemoryFactsAndOnboardingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMemoryFacts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FactKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FactValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    ObservationCount = table.Column<int>(type: "int", nullable: false),
                    SourceConversationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemoryFacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryFacts_LastObservedAtUtc",
                table: "UserMemoryFacts",
                column: "LastObservedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryFacts_UserId_FactKey",
                table: "UserMemoryFacts",
                columns: new[] { "UserId", "FactKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMemoryFacts");
        }
    }
}
