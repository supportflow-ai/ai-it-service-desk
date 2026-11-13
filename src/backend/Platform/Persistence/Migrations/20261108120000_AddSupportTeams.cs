using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <summary>
    /// Nền dữ liệu nhóm hỗ trợ (ADR-016), thuần cộng thêm: bảng support_teams, support_team_members, cột nullable
    /// tickets.support_team_id và hàng đợi phân loại thủ công có ID cố định. Không gán nhóm cho phiếu hiện có,
    /// không đổi dữ liệu cũ; bản API cũ (không biết cột mới) vẫn ghi được phiếu. Chỉ mục duy nhất có điều kiện
    /// bảo đảm tối đa một hàng đợi phân loại. support_team_members.user_id không có khóa ngoại tới Identity.
    /// </summary>
    public partial class AddSupportTeams : Migration
    {
        private static readonly Guid ManualTriageId = new("5b1f0c2e-7a64-4d3b-9e18-2c6a8f4d1b07");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_teams",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_triage_queue = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "support_team_members",
                schema: "public",
                columns: table => new
                {
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_lead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_team_members", x => new { x.team_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_support_team_members_support_teams_team_id",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "support_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "support_team_id",
                schema: "public",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_teams_code",
                schema: "public",
                table: "support_teams",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_support_teams_single_triage_queue",
                schema: "public",
                table: "support_teams",
                column: "is_triage_queue",
                unique: true,
                filter: "is_triage_queue = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_support_team_members_user_id",
                schema: "public",
                table: "support_team_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_support_team_id",
                schema: "public",
                table: "tickets",
                column: "support_team_id");

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_support_teams_support_team_id",
                schema: "public",
                table: "tickets",
                column: "support_team_id",
                principalSchema: "public",
                principalTable: "support_teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Hàng đợi phân loại thủ công: ID và mã cố định, trùng SupportTeam.ManualTriageId/ManualTriageCode.
            migrationBuilder.InsertData(
                schema: "public",
                table: "support_teams",
                columns: new[] { "Id", "code", "name", "is_triage_queue", "is_active" },
                values: new object[] { ManualTriageId, "MANUAL_TRIAGE", "Phân loại thủ công", true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tickets_support_teams_support_team_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_support_team_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "support_team_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropTable(
                name: "support_team_members",
                schema: "public");

            migrationBuilder.DropTable(
                name: "support_teams",
                schema: "public");
        }
    }
}
