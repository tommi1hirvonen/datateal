using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuckHouse.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarmNodePoolFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxNodes",
                table: "NodePoolConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "NodeAcquireTimeout",
                table: "NodePoolConfigs",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarmNodes",
                table: "NodePoolConfigs",
                type: "integer",
                nullable: true,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxNodes",
                table: "NodePoolConfigs");

            migrationBuilder.DropColumn(
                name: "NodeAcquireTimeout",
                table: "NodePoolConfigs");

            migrationBuilder.DropColumn(
                name: "WarmNodes",
                table: "NodePoolConfigs");
        }
    }
}
