using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveAiOutcomeAndDecisionRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "decision_revision",
                schema: "public",
                table: "ai_analyses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outcome",
                schema: "public",
                table: "ai_analyses",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string[]>(
                name: "validation_errors",
                schema: "public",
                table: "ai_analyses",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.Sql("UPDATE public.ai_analyses SET outcome = CASE WHEN succeeded THEN 'Success' ELSE 'Unavailable' END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "decision_revision",
                schema: "public",
                table: "ai_analyses");

            migrationBuilder.DropColumn(
                name: "outcome",
                schema: "public",
                table: "ai_analyses");

            migrationBuilder.DropColumn(
                name: "validation_errors",
                schema: "public",
                table: "ai_analyses");
        }
    }
}
