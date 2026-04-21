using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuckHouse.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveScheduleNextFireTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextFireTime",
                table: "JobSchedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextFireTime",
                table: "JobSchedules",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
