using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuckHouse.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CatalogNames",
                table: "WorkspaceItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Catalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsManaged = table.Column<bool>(type: "boolean", nullable: false),
                    DataPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    EncryptedStorageConnectionString = table.Column<string>(type: "text", nullable: true),
                    CatalogHost = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CatalogPort = table.Column<int>(type: "integer", nullable: true),
                    CatalogDatabase = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CatalogUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EncryptedCatalogPassword = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catalogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Catalogs_Name",
                table: "Catalogs",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Catalogs");

            migrationBuilder.DropColumn(
                name: "CatalogNames",
                table: "WorkspaceItems");
        }
    }
}
