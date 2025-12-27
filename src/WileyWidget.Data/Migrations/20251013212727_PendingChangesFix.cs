using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251013212727_PendingChangesFix")]
    public partial class PendingChangesFix : Migration
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
