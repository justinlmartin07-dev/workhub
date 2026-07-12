using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCustomerCompanyName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The company was the customer's display identity when present —
            // preserve it as the name before the column goes away.
            migrationBuilder.Sql(
                "UPDATE customers SET name = company_name WHERE company_name IS NOT NULL AND btrim(company_name) <> '';");

            migrationBuilder.DropColumn(
                name: "company_name",
                table: "customers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "company_name",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
