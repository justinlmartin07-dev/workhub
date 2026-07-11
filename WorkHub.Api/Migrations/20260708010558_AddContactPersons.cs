using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContactPersons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "main_contact_id",
                table: "jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contact_persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_persons", x => x.id);
                    table.ForeignKey(
                        name: "FK_contact_persons_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_jobs_main_contact_id",
                table: "jobs",
                column: "main_contact_id");

            migrationBuilder.CreateIndex(
                name: "idx_contact_persons_customer_id",
                table: "contact_persons",
                column: "customer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_jobs_contact_persons_main_contact_id",
                table: "jobs",
                column: "main_contact_id",
                principalTable: "contact_persons",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_jobs_contact_persons_main_contact_id",
                table: "jobs");

            migrationBuilder.DropTable(
                name: "contact_persons");

            migrationBuilder.DropIndex(
                name: "idx_jobs_main_contact_id",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "main_contact_id",
                table: "jobs");
        }
    }
}
