using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAnalysisResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalysisDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AnalysisType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AiResponse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessingTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApiCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiResponseCache",
                columns: table => new
                {
                    CacheKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Response = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccessCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiResponseCache", x => x.CacheKey);
                });

            migrationBuilder.CreateTable(
                name: "Enterprises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MonthlyExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CitizenCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ComputedDeficit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SuggestedRateHike = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QboClassId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QboSyncStatus = table.Column<int>(type: "int", nullable: false),
                    QboLastSync = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enterprises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OverallBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    TotalMonthlyRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalMonthlyExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalMonthlyBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCitizensServed = table.Column<int>(type: "int", nullable: false),
                    AverageRatePerCitizen = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverallBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAnalysisAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    OperationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "System"),
                    EnterpriseId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "LocalSystem"),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
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
                name: "AiRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnterpriseId = table.Column<int>(type: "int", nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RecommendationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    RecommendationText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ConfidenceLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 50),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ImplementationDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
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
                name: "BudgetInteractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrimaryEnterpriseId = table.Column<int>(type: "int", nullable: false),
                    SecondaryEnterpriseId = table.Column<int>(type: "int", nullable: true),
                    InteractionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsCost = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    QboAccountId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    QboAccountRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QboSyncStatus = table.Column<int>(type: "int", nullable: false),
                    QboLastSync = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetInteractions_Enterprises_PrimaryEnterpriseId",
                        column: x => x.PrimaryEnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetInteractions_Enterprises_SecondaryEnterpriseId",
                        column: x => x.SecondaryEnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteractions_InteractionType",
                table: "BudgetInteractions",
                column: "InteractionType");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteractions_PrimaryEnterpriseId",
                table: "BudgetInteractions",
                column: "PrimaryEnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteractions_SecondaryEnterpriseId",
                table: "BudgetInteractions",
                column: "SecondaryEnterpriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Enterprises_Name",
                table: "Enterprises",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OverallBudgets_IsCurrent",
                table: "OverallBudgets",
                column: "IsCurrent",
                unique: true,
                filter: "IsCurrent = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OverallBudgets_SnapshotDate",
                table: "OverallBudgets",
                column: "SnapshotDate");
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

            migrationBuilder.DropTable(
                name: "BudgetInteractions");

            migrationBuilder.DropTable(
                name: "OverallBudgets");

            migrationBuilder.DropTable(
                name: "Enterprises");
        }
    }
}
