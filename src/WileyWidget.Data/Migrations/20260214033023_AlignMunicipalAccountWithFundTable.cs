using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignMunicipalAccountWithFundTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Fund",
                table: "MunicipalAccounts",
                newName: "FundType");

            migrationBuilder.RenameIndex(
                name: "IX_MunicipalAccounts_Fund_Type",
                table: "MunicipalAccounts",
                newName: "IX_MunicipalAccounts_FundType_Type");

            migrationBuilder.AddColumn<int>(
                name: "FundId",
                table: "MunicipalAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 1,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 2,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 3,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 4,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 5,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 6,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 7,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 8,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 9,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 10,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 11,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 12,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 13,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 14,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 15,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 16,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 17,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 18,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 19,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 20,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 21,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 22,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 23,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 24,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 25,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 26,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 27,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 28,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 29,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 30,
                column: "FundId",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 31,
                column: "FundId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_FundId",
                table: "MunicipalAccounts",
                column: "FundId");

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_Funds_FundId",
                table: "MunicipalAccounts",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_Funds_FundId",
                table: "MunicipalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_FundId",
                table: "MunicipalAccounts");

            migrationBuilder.DropColumn(
                name: "FundId",
                table: "MunicipalAccounts");

            migrationBuilder.RenameColumn(
                name: "FundType",
                table: "MunicipalAccounts",
                newName: "Fund");

            migrationBuilder.RenameIndex(
                name: "IX_MunicipalAccounts_FundType_Type",
                table: "MunicipalAccounts",
                newName: "IX_MunicipalAccounts_Fund_Type");
        }
    }
}
