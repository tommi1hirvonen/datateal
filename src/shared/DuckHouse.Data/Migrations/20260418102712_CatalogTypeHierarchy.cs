using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuckHouse.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogTypeHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add CatalogType with a temporary default so existing rows get "Managed"
            migrationBuilder.AddColumn<string>(
                name: "CatalogType",
                table: "Catalogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Managed");

            // Migrate existing rows: unmanaged catalogs become "Unmanaged"
            migrationBuilder.Sql(
                "UPDATE \"Catalogs\" SET \"CatalogType\" = 'Unmanaged' WHERE \"IsManaged\" = false;");

            migrationBuilder.DropColumn(
                name: "IsManaged",
                table: "Catalogs");

            // Remove the default constraint now that all rows have a value
            migrationBuilder.AlterColumn<string>(
                name: "CatalogType",
                table: "Catalogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: false,
                oldDefaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManaged",
                table: "Catalogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE \"Catalogs\" SET \"IsManaged\" = true WHERE \"CatalogType\" = 'Managed';");

            migrationBuilder.DropColumn(
                name: "CatalogType",
                table: "Catalogs");
        }
    }
}
