using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurablePasswordResetJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "password_reset_jobs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    otp_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    protected_code = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_jobs_completed_at",
                schema: "public",
                table: "password_reset_jobs",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_jobs_next_attempt_at_created_at",
                schema: "public",
                table: "password_reset_jobs",
                columns: new[] { "next_attempt_at", "created_at" },
                filter: "completed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_reset_jobs",
                schema: "public");
        }
    }
}
