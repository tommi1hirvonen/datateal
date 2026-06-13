using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Datateal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceItems_Title_FolderId",
                table: "WorkspaceItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceItems_Title_Root",
                table: "WorkspaceItems");

            migrationBuilder.DropIndex(
                name: "IX_WheelPackages_Name",
                table: "WheelPackages");

            migrationBuilder.DropIndex(
                name: "IX_Secrets_Key",
                table: "Secrets");

            migrationBuilder.DropIndex(
                name: "IX_NodePoolConfigs_Name",
                table: "NodePoolConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_Name",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_EnvironmentVariables_Key",
                table: "EnvironmentVariables");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "WorkspaceItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "WheelPackages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Secrets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "NodePoolConfigs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "JobRuns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Folders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "EnvironmentVariables",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<bool>(
                name: "AccessibleFromAllWorkspaces",
                table: "Catalogs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            // Seed the default workspace that existing single-tenant data is migrated into.
            // Its id matches the column default applied to the new WorkspaceId columns above,
            // so existing rows are backfilled to it before the foreign keys are added.
            migrationBuilder.Sql(
                """
                INSERT INTO "Workspaces" ("Id", "Name", "Description", "IsDefault", "CreatedAt", "UpdatedAt")
                VALUES ('11111111-1111-1111-1111-111111111111', 'Default',
                        'Default workspace (migrated from single-tenant data).', true, now(), now());
                """);

            migrationBuilder.CreateTable(
                name: "CatalogWorkspaceAccess",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogWorkspaceAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogWorkspaceAccess_Catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "Catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogWorkspaceAccess_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Roles = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceMemberships_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkspaceMemberships_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceItems_Workspace_Title_FolderId",
                table: "WorkspaceItems",
                columns: new[] { "WorkspaceId", "Title", "FolderId" },
                unique: true,
                filter: "\"FolderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceItems_Workspace_Title_Root",
                table: "WorkspaceItems",
                columns: new[] { "WorkspaceId", "Title" },
                unique: true,
                filter: "\"FolderId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WheelPackages_WorkspaceId_Name",
                table: "WheelPackages",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_WorkspaceId_Key",
                table: "Secrets",
                columns: new[] { "WorkspaceId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodePoolConfigs_WorkspaceId_Name",
                table: "NodePoolConfigs",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_WorkspaceId_Name",
                table: "Jobs",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRuns_WorkspaceId",
                table: "JobRuns",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_WorkspaceId",
                table: "Folders",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentVariables_WorkspaceId_Key",
                table: "EnvironmentVariables",
                columns: new[] { "WorkspaceId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogWorkspaceAccess_CatalogId_WorkspaceId",
                table: "CatalogWorkspaceAccess",
                columns: new[] { "CatalogId", "WorkspaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogWorkspaceAccess_WorkspaceId",
                table: "CatalogWorkspaceAccess",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMemberships_UserId",
                table: "WorkspaceMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMemberships_WorkspaceId_UserId",
                table: "WorkspaceMemberships",
                columns: new[] { "WorkspaceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_IsDefault",
                table: "Workspaces",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\"");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_Name",
                table: "Workspaces",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EnvironmentVariables_Workspaces_WorkspaceId",
                table: "EnvironmentVariables",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Workspaces_WorkspaceId",
                table: "Folders",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_Workspaces_WorkspaceId",
                table: "Jobs",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NodePoolConfigs_Workspaces_WorkspaceId",
                table: "NodePoolConfigs",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Secrets_Workspaces_WorkspaceId",
                table: "Secrets",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WheelPackages_Workspaces_WorkspaceId",
                table: "WheelPackages",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkspaceItems_Workspaces_WorkspaceId",
                table: "WorkspaceItems",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Convert each user's existing workspace-scoped roles into a membership in the
            // default workspace. Tenant-global roles (Admin, CatalogContributor) stay on AppUsers.Roles.
            migrationBuilder.Sql(
                """
                INSERT INTO "WorkspaceMemberships" ("Id", "WorkspaceId", "UserId", "Roles", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(),
                       '11111111-1111-1111-1111-111111111111',
                       u."Id",
                       ws.roles,
                       now(),
                       now()
                FROM "AppUsers" u
                CROSS JOIN LATERAL (
                    SELECT COALESCE(jsonb_agg(r), '[]'::jsonb) AS roles
                    FROM jsonb_array_elements_text(u."Roles") AS r
                    WHERE r NOT IN ('Admin', 'CatalogContributor')
                ) ws
                WHERE ws.roles <> '[]'::jsonb;
                """);

            // Strip the now workspace-scoped roles from the tenant-global role list.
            migrationBuilder.Sql(
                """
                UPDATE "AppUsers" u
                SET "Roles" = COALESCE((
                    SELECT jsonb_agg(r)
                    FROM jsonb_array_elements_text(u."Roles") AS r
                    WHERE r IN ('Admin', 'CatalogContributor')
                ), '[]'::jsonb);
                """);

            // Remove the temporary column defaults; the application always sets WorkspaceId explicitly.
            foreach (var table in new[]
                     {
                         "Folders", "WorkspaceItems", "WheelPackages", "Secrets",
                         "EnvironmentVariables", "NodePoolConfigs", "Jobs", "JobRuns"
                     })
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" ALTER COLUMN \"WorkspaceId\" DROP DEFAULT;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EnvironmentVariables_Workspaces_WorkspaceId",
                table: "EnvironmentVariables");

            migrationBuilder.DropForeignKey(
                name: "FK_Folders_Workspaces_WorkspaceId",
                table: "Folders");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_Workspaces_WorkspaceId",
                table: "Jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_NodePoolConfigs_Workspaces_WorkspaceId",
                table: "NodePoolConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_Secrets_Workspaces_WorkspaceId",
                table: "Secrets");

            migrationBuilder.DropForeignKey(
                name: "FK_WheelPackages_Workspaces_WorkspaceId",
                table: "WheelPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkspaceItems_Workspaces_WorkspaceId",
                table: "WorkspaceItems");

            migrationBuilder.DropTable(
                name: "CatalogWorkspaceAccess");

            migrationBuilder.DropTable(
                name: "WorkspaceMemberships");

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceItems_Workspace_Title_FolderId",
                table: "WorkspaceItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceItems_Workspace_Title_Root",
                table: "WorkspaceItems");

            migrationBuilder.DropIndex(
                name: "IX_WheelPackages_WorkspaceId_Name",
                table: "WheelPackages");

            migrationBuilder.DropIndex(
                name: "IX_Secrets_WorkspaceId_Key",
                table: "Secrets");

            migrationBuilder.DropIndex(
                name: "IX_NodePoolConfigs_WorkspaceId_Name",
                table: "NodePoolConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_WorkspaceId_Name",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_JobRuns_WorkspaceId",
                table: "JobRuns");

            migrationBuilder.DropIndex(
                name: "IX_Folders_WorkspaceId",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_EnvironmentVariables_WorkspaceId_Key",
                table: "EnvironmentVariables");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "WorkspaceItems");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "WheelPackages");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "NodePoolConfigs");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "JobRuns");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "EnvironmentVariables");

            migrationBuilder.DropColumn(
                name: "AccessibleFromAllWorkspaces",
                table: "Catalogs");

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

            migrationBuilder.CreateIndex(
                name: "IX_WheelPackages_Name",
                table: "WheelPackages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_Key",
                table: "Secrets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodePoolConfigs_Name",
                table: "NodePoolConfigs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Name",
                table: "Jobs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentVariables_Key",
                table: "EnvironmentVariables",
                column: "Key",
                unique: true);
        }
    }
}
