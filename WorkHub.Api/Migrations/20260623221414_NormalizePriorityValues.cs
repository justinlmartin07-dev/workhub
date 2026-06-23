using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePriorityValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE jobs SET priority = CASE
                    WHEN priority = 'Normal' THEN 'Medium'
                    WHEN priority = 'low'    THEN 'Low'
                    WHEN priority = 'high'   THEN 'High'
                    ELSE priority
                END;");

            migrationBuilder.AlterColumn<string>(
                name: "priority",
                table: "jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Medium",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "priority",
                table: "jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Normal",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Medium");
        }
    }
}
