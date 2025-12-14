using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMunicipalAccountDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "FundClass",
                table: "MunicipalAccounts");

            migrationBuilder.AddColumn<string>(
                name: "FundDescription",
                table: "MunicipalAccounts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TypeDescription",
                table: "MunicipalAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "FundDescription", "TypeDescription" },
                values: new object[] { "General Fund", "Asset" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "FundDescription",
                table: "MunicipalAccounts");

            migrationBuilder.DropColumn(
                name: "TypeDescription",
                table: "MunicipalAccounts");

            migrationBuilder.AddColumn<int>(
                name: "FundClass",
                table: "MunicipalAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 1,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 2,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 3,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 4,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 5,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 6,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 7,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 8,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 9,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 10,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 11,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 12,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 13,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 14,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 15,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 16,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 17,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 18,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 19,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 20,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 21,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 22,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 23,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 24,
                column: "FundClass",
                value: null);

            migrationBuilder.UpdateData(
                table: "MunicipalAccounts",
                keyColumn: "Id",
                keyValue: 25,
                column: "FundClass",
                value: null);
        }
    }
}
