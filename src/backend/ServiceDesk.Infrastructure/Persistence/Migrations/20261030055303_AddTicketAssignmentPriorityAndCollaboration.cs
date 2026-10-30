using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAssignmentPriorityAndCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "assigned_at",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_to_id",
                schema: "public",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "impact",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "priority",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "urgency",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ticket_attachments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_comments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    author_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_comments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tickets_assigned_to_id",
                schema: "public",
                table: "tickets",
                column: "assigned_to_id");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_priority",
                schema: "public",
                table: "tickets",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_attachments_ticket_id",
                schema: "public",
                table: "ticket_attachments",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_comments_ticket_id_created_at",
                schema: "public",
                table: "ticket_comments",
                columns: new[] { "ticket_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_attachments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_comments",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_tickets_assigned_to_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_priority",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "assigned_to_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "impact",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "priority",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "urgency",
                schema: "public",
                table: "tickets");
        }
    }
}
