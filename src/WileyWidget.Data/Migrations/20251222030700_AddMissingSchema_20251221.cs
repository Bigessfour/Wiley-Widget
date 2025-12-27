using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251222030700_AddMissingSchema_20251221")]
    public partial class AddMissingSchema_20251221 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Placeholder no-op migration to match database history
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op
        }
    }
}
