using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentConfirmationLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "confirmation_attempt_id",
                schema: "public",
                table: "attachment_upload_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmation_lease_until",
                schema: "public",
                table: "attachment_upload_grants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confirmation_attempt_id",
                schema: "public",
                table: "attachment_upload_grants");

            migrationBuilder.DropColumn(
                name: "confirmation_lease_until",
                schema: "public",
                table: "attachment_upload_grants");
        }
    }
}
