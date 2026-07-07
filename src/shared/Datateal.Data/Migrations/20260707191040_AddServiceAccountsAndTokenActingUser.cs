using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Datateal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAccountsAndTokenActingUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppUsers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserType",
                table: "AppUsers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "UserAccount");

            // Backfill all existing rows — they are all interactive users.
            migrationBuilder.Sql("UPDATE \"AppUsers\" SET \"UserType\" = 'UserAccount' WHERE \"UserType\" = ''");

            migrationBuilder.AddColumn<Guid>(
                name: "ActingUserId",
                table: "ApiTokens",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "ActingUserId",
                table: "ApiTokens");
        }
    }
}
