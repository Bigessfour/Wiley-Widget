using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtilityCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServiceAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServiceCity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ServiceState = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ServiceZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MailingAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MailingCity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MailingState = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    MailingZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MeterNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ServiceLocation = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false, defaultValue: "Active"),
                    AccountOpenDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    AccountCloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BusinessLicenseNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityCustomers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_AccountNumber",
                table: "UtilityCustomers",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_CustomerType",
                table: "UtilityCustomers",
                column: "CustomerType");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_EmailAddress",
                table: "UtilityCustomers",
                column: "EmailAddress");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_MeterNumber",
                table: "UtilityCustomers",
                column: "MeterNumber");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_ServiceLocation",
                table: "UtilityCustomers",
                column: "ServiceLocation");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityCustomers_Status",
                table: "UtilityCustomers",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityCustomers");
        }
    }
}
