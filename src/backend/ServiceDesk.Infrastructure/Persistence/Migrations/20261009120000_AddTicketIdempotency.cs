using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Infrastructure.Persistence.Migrations;

public sealed partial class AddTicketIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ticket_idempotency",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                resource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ticket_idempotency", record => record.id);
                table.ForeignKey(
                    name: "FK_ticket_idempotency_tickets_ticket_id",
                    column: record => record.ticket_id,
                    principalSchema: "public", principalTable: "tickets", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_ticket_idempotency_expiry", "expires_at > created_at");
            });

        migrationBuilder.CreateIndex(
            name: "UX_ticket_idempotency_scope", schema: "public", table: "ticket_idempotency",
            columns: new[] { "requester_id", "method", "resource", "key_hash" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ticket_idempotency_ticket_id", schema: "public", table: "ticket_idempotency", column: "ticket_id");
        migrationBuilder.CreateIndex(
            name: "IX_ticket_idempotency_expires_at", schema: "public", table: "ticket_idempotency", column: "expires_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ticket_idempotency", schema: "public");
}
