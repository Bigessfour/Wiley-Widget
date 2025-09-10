using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Migrations
{
    /// <inheritdoc />
    public partial class AddAiEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ComputedDeficit",
                table: "Enterprises",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedRateHike",
                table: "Enterprises",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiAnalysisAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    OperationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "System"),
                    EnterpriseId = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "LocalSystem"),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    IsSuccessful = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAnalysisAudits_Enterprises_EnterpriseId",
                        column: x => x.EnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AiAnalysisResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnalysisDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AnalysisType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AiResponse = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessingTimeMs = table.Column<long>(type: "INTEGER", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ApiCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnterpriseId = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RecommendationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    RecommendationText = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ConfidenceLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 50),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ImplementationDeadline = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRecommendations_Enterprises_EnterpriseId",
                        column: x => x.EnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiResponseCache",
                columns: table => new
                {
                    CacheKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Response = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccessCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiResponseCache", x => x.CacheKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisAudits_EnterpriseId",
                table: "AiAnalysisAudits",
                column: "EnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisAudits_OperationType",
                table: "AiAnalysisAudits",
                column: "OperationType");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisAudits_Timestamp",
                table: "AiAnalysisAudits",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisAudits_UserId",
                table: "AiAnalysisAudits",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisResults_AnalysisDate",
                table: "AiAnalysisResults",
                column: "AnalysisDate");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisResults_AnalysisType",
                table: "AiAnalysisResults",
                column: "AnalysisType");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisResults_InputHash",
                table: "AiAnalysisResults",
                column: "InputHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendations_EnterpriseId",
                table: "AiRecommendations",
                column: "EnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendations_Priority",
                table: "AiRecommendations",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendations_RecommendationType",
                table: "AiRecommendations",
                column: "RecommendationType");

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendations_Status",
                table: "AiRecommendations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AiResponseCache_ExpiresAt",
                table: "AiResponseCache",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAnalysisAudits");

            migrationBuilder.DropTable(
                name: "AiAnalysisResults");

            migrationBuilder.DropTable(
                name: "AiRecommendations");

            migrationBuilder.DropTable(
                name: "AiResponseCache");

            migrationBuilder.DropColumn(
                name: "ComputedDeficit",
                table: "Enterprises");

            migrationBuilder.DropColumn(
                name: "SuggestedRateHike",
                table: "Enterprises");
        }
    }
}
