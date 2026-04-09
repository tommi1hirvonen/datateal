using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuckHouse.ControlPlane.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NodeConfigs",
                columns: table => new
                {
                    NodeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    KernelIdleTimeout = table.Column<TimeSpan>(type: "interval", nullable: false),
                    NodeIdleTimeout = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeConfigs", x => x.NodeName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeConfigs");
        }
    }
}
