using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableAiTriage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_analyses",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_revision = table.Column<long>(type: "bigint", nullable: false),
                    input_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    previous_category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    previous_impact = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    previous_urgency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    previous_priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    applied_category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    applied_impact = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    applied_urgency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    applied_priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_analyses", x => x.id);
                    table.CheckConstraint("ck_ai_analyses_decision", "(decision = 'Pending') = (decided_by IS NULL) AND (decided_by IS NULL) = (decided_at IS NULL) AND (decision = 'Pending' OR succeeded)");
                });

            migrationBuilder.CreateTable(
                name: "ai_suggestions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    suggested_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_score = table.Column<double>(type: "double precision", nullable: true),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    final_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reviewed_by = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_suggestions", x => x.id);
                    table.CheckConstraint("ck_ai_suggestions_model_score", "model_score IS NULL OR (model_score >= 0 AND model_score <= 1)");
                    table.ForeignKey(
                        name: "FK_ai_suggestions_ai_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalSchema: "public",
                        principalTable: "ai_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_analyses_ticket_id_created_at_id",
                schema: "public",
                table: "ai_analyses",
                columns: new[] { "ticket_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_ai_suggestions_analysis_id_type",
                schema: "public",
                table: "ai_suggestions",
                columns: new[] { "analysis_id", "type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_suggestions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ai_analyses",
                schema: "public");
        }
    }
}
