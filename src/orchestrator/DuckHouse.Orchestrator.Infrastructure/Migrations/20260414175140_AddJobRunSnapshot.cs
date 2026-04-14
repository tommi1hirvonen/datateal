using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuckHouse.Orchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRunSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotJson",
                table: "JobRuns",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotJson",
                table: "JobRuns");
        }
    }
}
