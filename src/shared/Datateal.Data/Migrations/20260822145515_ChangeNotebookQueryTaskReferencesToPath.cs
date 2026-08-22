using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Datateal.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeNotebookQueryTaskReferencesToPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotebookPath",
                table: "JobTasks",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueryPath",
                table: "JobTasks",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            // Backfill NotebookPath/QueryPath from the current NotebookId/QueryId + Folders parent
            // chain before dropping the old id columns. A row whose id no longer resolves to a live
            // workspace item (already orphaned by the bug this migration fixes — a bundle-driven
            // rename recreates the notebook/query row with a new id) is left NULL: the next run of
            // that task now fails with a clear "not found" error instead of silently resolving
            // against the wrong item.
            migrationBuilder.Sql("""
                WITH RECURSIVE folder_path AS (
                    SELECT "Id", "Name"::text AS path, "ParentId"
                    FROM "Folders"
                    WHERE "ParentId" IS NULL
                    UNION ALL
                    SELECT f."Id", fp.path || '/' || f."Name", f."ParentId"
                    FROM "Folders" f
                    JOIN folder_path fp ON f."ParentId" = fp."Id"
                )
                UPDATE "JobTasks" jt
                SET "NotebookPath" = COALESCE(fp.path || '/' || wi."Title", wi."Title")
                FROM "WorkspaceItems" wi
                LEFT JOIN folder_path fp ON fp."Id" = wi."FolderId"
                WHERE jt."NotebookId" = wi."Id" AND jt."TaskType" = 'Notebook';
                """);

            migrationBuilder.Sql("""
                WITH RECURSIVE folder_path AS (
                    SELECT "Id", "Name"::text AS path, "ParentId"
                    FROM "Folders"
                    WHERE "ParentId" IS NULL
                    UNION ALL
                    SELECT f."Id", fp.path || '/' || f."Name", f."ParentId"
                    FROM "Folders" f
                    JOIN folder_path fp ON f."ParentId" = fp."Id"
                )
                UPDATE "JobTasks" jt
                SET "QueryPath" = COALESCE(fp.path || '/' || wi."Title", wi."Title")
                FROM "WorkspaceItems" wi
                LEFT JOIN folder_path fp ON fp."Id" = wi."FolderId"
                WHERE jt."QueryId" = wi."Id" AND jt."TaskType" = 'SqlQuery';
                """);

            migrationBuilder.DropColumn(
                name: "NotebookId",
                table: "JobTasks");

            migrationBuilder.DropColumn(
                name: "QueryId",
                table: "JobTasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NotebookId",
                table: "JobTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QueryId",
                table: "JobTasks",
                type: "uuid",
                nullable: true);

            // Best-effort reverse backfill: resolve NotebookPath/QueryPath back to the workspace
            // item's current id. Paths that no longer resolve are left NULL.
            migrationBuilder.Sql("""
                WITH RECURSIVE folder_path AS (
                    SELECT "Id", "Name"::text AS path, "ParentId"
                    FROM "Folders"
                    WHERE "ParentId" IS NULL
                    UNION ALL
                    SELECT f."Id", fp.path || '/' || f."Name", f."ParentId"
                    FROM "Folders" f
                    JOIN folder_path fp ON f."ParentId" = fp."Id"
                )
                UPDATE "JobTasks" jt
                SET "NotebookId" = wi."Id"
                FROM "WorkspaceItems" wi
                LEFT JOIN folder_path fp ON fp."Id" = wi."FolderId"
                WHERE jt."TaskType" = 'Notebook'
                  AND jt."NotebookPath" = COALESCE(fp.path || '/' || wi."Title", wi."Title");
                """);

            migrationBuilder.Sql("""
                WITH RECURSIVE folder_path AS (
                    SELECT "Id", "Name"::text AS path, "ParentId"
                    FROM "Folders"
                    WHERE "ParentId" IS NULL
                    UNION ALL
                    SELECT f."Id", fp.path || '/' || f."Name", f."ParentId"
                    FROM "Folders" f
                    JOIN folder_path fp ON f."ParentId" = fp."Id"
                )
                UPDATE "JobTasks" jt
                SET "QueryId" = wi."Id"
                FROM "WorkspaceItems" wi
                LEFT JOIN folder_path fp ON fp."Id" = wi."FolderId"
                WHERE jt."TaskType" = 'SqlQuery'
                  AND jt."QueryPath" = COALESCE(fp.path || '/' || wi."Title", wi."Title");
                """);

            migrationBuilder.DropColumn(
                name: "NotebookPath",
                table: "JobTasks");

            migrationBuilder.DropColumn(
                name: "QueryPath",
                table: "JobTasks");
        }
    }
}
