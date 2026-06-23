using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeJobStatusValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE jobs SET status = 'New' WHERE status = 'Pending';");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "New",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "New");
        }
    }
}
