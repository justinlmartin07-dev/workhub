using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobNoteUpdatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                table: "job_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_notes_updated_by",
                table: "job_notes",
                column: "updated_by");

            migrationBuilder.AddForeignKey(
                name: "FK_job_notes_users_updated_by",
                table: "job_notes",
                column: "updated_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_job_notes_users_updated_by",
                table: "job_notes");

            migrationBuilder.DropIndex(
                name: "IX_job_notes_updated_by",
                table: "job_notes");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "job_notes");
        }
    }
}
