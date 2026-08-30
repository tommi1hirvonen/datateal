using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Datateal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNaturalKeyIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskDependencies_TaskId",
                table: "TaskDependencies");

            migrationBuilder.DropIndex(
                name: "IX_JobSchedules_JobId",
                table: "JobSchedules");

            migrationBuilder.DropIndex(
                name: "IX_JobParameters_JobId",
                table: "JobParameters");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "JobSchedules",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Back-fill: assign sequential names to existing schedules, partitioned per job.
            migrationBuilder.Sql("""
                UPDATE "JobSchedules" s
                SET "Name" = 'schedule-' || ranked.rn
                FROM (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "JobId" ORDER BY "Id") AS rn
                    FROM "JobSchedules"
                ) ranked
                WHERE s."Id" = ranked."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_TaskId_DependsOnTaskId",
                table: "TaskDependencies",
                columns: new[] { "TaskId", "DependsOnTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobSchedules_JobId_Name",
                table: "JobSchedules",
                columns: new[] { "JobId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobParameters_JobId_Name",
                table: "JobParameters",
                columns: new[] { "JobId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_WorkspaceId_Name_Root",
                table: "Folders",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true,
                filter: "\"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_WorkspaceId_ParentId_Name",
                table: "Folders",
                columns: new[] { "WorkspaceId", "ParentId", "Name" },
                unique: true,
                filter: "\"ParentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskDependencies_TaskId_DependsOnTaskId",
                table: "TaskDependencies");

            migrationBuilder.DropIndex(
                name: "IX_JobSchedules_JobId_Name",
                table: "JobSchedules");

            migrationBuilder.DropIndex(
                name: "IX_JobParameters_JobId_Name",
                table: "JobParameters");

            migrationBuilder.DropIndex(
                name: "IX_Folders_WorkspaceId_Name_Root",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Folders_WorkspaceId_ParentId_Name",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "JobSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_TaskId",
                table: "TaskDependencies",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_JobSchedules_JobId",
                table: "JobSchedules",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobParameters_JobId",
                table: "JobParameters",
                column: "JobId");
        }
    }
}
