using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuckHouse.Ui.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceItemTitleUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceItems_Title_FolderId",
                table: "WorkspaceItems",
                columns: new[] { "Title", "FolderId" },
                unique: true,
                filter: "\"FolderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceItems_Title_Root",
                table: "WorkspaceItems",
                column: "Title",
                unique: true,
                filter: "\"FolderId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceItems_Title_FolderId",
                table: "WorkspaceItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceItems_Title_Root",
                table: "WorkspaceItems");
        }
    }
}
