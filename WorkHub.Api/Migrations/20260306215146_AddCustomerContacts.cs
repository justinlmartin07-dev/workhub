using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "customers");

            migrationBuilder.CreateTable(
                name: "customer_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_contacts_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_customer_contacts_customer_id",
                table: "customer_contacts",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_contacts");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
