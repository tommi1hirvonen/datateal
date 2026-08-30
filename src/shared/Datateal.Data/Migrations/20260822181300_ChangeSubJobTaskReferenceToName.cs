using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Datateal.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSubJobTaskReferenceToName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubJobName",
                table: "JobTasks",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // Backfill SubJobName from the current SubJobId before dropping the old column. A
            // task whose SubJobId no longer resolves to a live job is left NULL — the next run of
            // that task now fails with a clear "not found" error instead of silently referencing
            // a non-existent job.
            migrationBuilder.Sql("""
                UPDATE "JobTasks" jt
                SET "SubJobName" = j."Name"
                FROM "Jobs" j
                WHERE jt."SubJobId" = j."Id" AND jt."TaskType" = 'SubJob';
                """);

            migrationBuilder.DropColumn(
                name: "SubJobId",
                table: "JobTasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubJobId",
                table: "JobTasks",
                type: "uuid",
                nullable: true);

            // Best-effort reverse backfill: resolve SubJobName back to the job's current id
            // within the same workspace. Names that no longer resolve are left NULL.
            migrationBuilder.Sql("""
                UPDATE "JobTasks" jt
                SET "SubJobId" = target."Id"
                FROM "Jobs" parent
                JOIN "Jobs" target ON target."WorkspaceId" = parent."WorkspaceId" AND target."Name" = jt."SubJobName"
                WHERE jt."JobId" = parent."Id" AND jt."TaskType" = 'SubJob';
                """);

            migrationBuilder.DropColumn(
                name: "SubJobName",
                table: "JobTasks");
        }
    }
}
