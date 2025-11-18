using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLookupSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Insert Departments that may not be present from prior migrations
            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "DepartmentCode", "Name", "ParentId" },
                values: new object[,]
                {
                    { 3, "CULT", "Culture and Recreation", null },
                    { 4, "SAN", "Sanitation", 2 },
                    { 5, "UTIL", "Utilities", null },
                    { 6, "COMM", "Community Center", null },
                    { 7, "CONS", "Conservation", null },
                    { 8, "REC", "Recreation", null }
                });

            // Insert Funds that are not present in earlier migrations (avoid duplicating Ids 1-2)
            migrationBuilder.InsertData(
                table: "Funds",
                columns: new[] { "Id", "FundCode", "Name", "Type" },
                values: new object[,]
                {
                    { 3, "300-UTIL", "Utility Fund", 2 },
                    { 4, "400-COMM", "Community Center Fund", 3 },
                    { 5, "500-CONS", "Conservation Trust Fund", 6 },
                    { 6, "600-REC", "Recreation Fund", 3 }
                });

            // Insert a few sample vendors
            migrationBuilder.InsertData(
                table: "Vendor",
                columns: new[] { "Id", "ContactInfo", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "contact@acmesupplies.example.com", true, "Acme Supplies" },
                    { 2, "info@muniservices.example.com", true, "Municipal Services Co." },
                    { 3, "projects@trailbuilders.example.com", true, "Trail Builders LLC" }
                });

            // Insert a default AppSettings row if missing (Id = 1)
            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "Theme", "EnableDataCaching", "CacheExpirationMinutes", "SelectedLogLevel", "EnableFileLogging", "LogFilePath", "QuickBooksEnvironment", "QboTokenExpiry", "LastSelectedEnterpriseId" },
                values: new object[] { 1, "FluentDark", true, 30, "Information", true, "logs/wiley-widget.log", "sandbox", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DeleteData(table: "AppSettings", keyColumn: "Id", keyValue: 1);

            migrationBuilder.DeleteData(table: "Vendor", keyColumn: "Id", keyValue: 1);
            migrationBuilder.DeleteData(table: "Vendor", keyColumn: "Id", keyValue: 2);
            migrationBuilder.DeleteData(table: "Vendor", keyColumn: "Id", keyValue: 3);

            migrationBuilder.DeleteData(table: "Funds", keyColumn: "Id", keyValue: 3);
            migrationBuilder.DeleteData(table: "Funds", keyColumn: "Id", keyValue: 4);
            migrationBuilder.DeleteData(table: "Funds", keyColumn: "Id", keyValue: 5);
            migrationBuilder.DeleteData(table: "Funds", keyColumn: "Id", keyValue: 6);

            migrationBuilder.DeleteData(table: "Departments", keyColumn: "Id", keyValue: 3);
            migrationBuilder.DeleteData(table: "Departments", keyColumn: "Id", keyValue: 4);
            migrationBuilder.DeleteData(table: "Departments", keyColumn: "Id", keyValue: 5);
            migrationBuilder.DeleteData(table: "Departments", keyColumn: "Id", keyValue: 6);
            migrationBuilder.DeleteData(table: "Departments", keyColumn: "Id", keyValue: 7);
            migrationBuilder.DeleteData(table: "Departments", keyColumn: "Id", keyValue: 8);
        }
    }
}
