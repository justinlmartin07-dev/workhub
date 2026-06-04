using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobNoteSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "job_notes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_job_notes_deleted_at",
                table: "job_notes",
                column: "deleted_at",
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_job_notes_deleted_at",
                table: "job_notes");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "job_notes");
        }
    }
}
