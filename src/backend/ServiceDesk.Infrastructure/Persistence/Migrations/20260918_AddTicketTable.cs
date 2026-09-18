using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Infrastructure.Persistence;

#nullable disable

namespace ServiceDesk.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260918143000_AddTicketTable")]
    public partial class AddTicketTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ticket_number_seq",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    // Sequence format behavior: LPAD does not truncate when sequence > 9999.
                    // Numbers >= 10000 return full number (e.g. IT-YYYY-10000) which fits in VARCHAR(30).
                    ticket_number = table.Column<string>(maxLength: 30, nullable: false, 
                        defaultValueSql: "'IT-' || TO_CHAR(NOW(), 'YYYY') || '-' || LPAD(nextval('ticket_number_seq')::TEXT, 4, '0')"),
                    requester_id = table.Column<Guid>(nullable: false),
                    requester_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(maxLength: 200, nullable: false),
                    description = table.Column<string>(nullable: false),
                    category_id = table.Column<string>(maxLength: 20, nullable: false),
                    status = table.Column<int>(nullable: false),
                    created_at = table.Column<DateTime>(nullable: false),
                    updated_at = table.Column<DateTime>(nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tickets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tickets_ticket_number",
                schema: "public",
                table: "tickets",
                column: "ticket_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_requester_id",
                schema: "public",
                table: "tickets",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_status",
                schema: "public",
                table: "tickets",
                column: "status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tickets",
                schema: "public");
                
            migrationBuilder.DropSequence(
                name: "ticket_number_seq",
                schema: "public");
        }
    }
}
