using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Datateal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "JobRuns",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "JobRuns");
        }
    }
}
