using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQboClientColumnsToAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Add columns that were missing from schema
            migrationBuilder.AddColumn<int>(
                name: "LastSelectedEnterpriseId",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeChartsInReports",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime?>(
                name: "LastReportStartDate",
                table: "AppSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime?>(
                name: "LastReportEndDate",
                table: "AppSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSelectedFormat",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSelectedReportType",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(name: "LastSelectedEnterpriseId", table: "AppSettings");
            migrationBuilder.DropColumn(name: "IncludeChartsInReports", table: "AppSettings");
            migrationBuilder.DropColumn(name: "LastReportStartDate", table: "AppSettings");
            migrationBuilder.DropColumn(name: "LastReportEndDate", table: "AppSettings");
            migrationBuilder.DropColumn(name: "LastSelectedFormat", table: "AppSettings");
            migrationBuilder.DropColumn(name: "LastSelectedReportType", table: "AppSettings");
        }
    }
}
