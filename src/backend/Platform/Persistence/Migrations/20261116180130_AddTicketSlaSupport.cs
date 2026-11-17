using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "first_responded_at",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "first_response_breached",
                schema: "public",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "first_response_target_minutes",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_sla_applicable",
                schema: "public",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "resolution_accumulated_duration",
                schema: "public",
                table: "tickets",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "resolution_breached",
                schema: "public",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolution_segment_started_at",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resolution_target_minutes",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_is_sla_applicable",
                schema: "public",
                table: "tickets",
                column: "is_sla_applicable");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_is_sla_applicable_status",
                schema: "public",
                table: "tickets",
                columns: new[] { "is_sla_applicable", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tickets_is_sla_applicable",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_is_sla_applicable_status",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "first_responded_at",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "first_response_breached",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "first_response_target_minutes",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "is_sla_applicable",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution_accumulated_duration",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution_breached",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution_segment_started_at",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution_target_minutes",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                schema: "public",
                table: "tickets");
        }
    }
}
