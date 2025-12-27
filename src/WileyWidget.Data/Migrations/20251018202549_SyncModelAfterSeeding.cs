using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251018202549_SyncModelAfterSeeding")]
    public partial class SyncModelAfterSeeding : Migration
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
