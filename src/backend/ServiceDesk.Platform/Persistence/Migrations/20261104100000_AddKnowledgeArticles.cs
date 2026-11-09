using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "articles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "article_tags",
                schema: "public",
                columns: table => new
                {
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_article_tags", x => new { x.article_id, x.name });
                    table.ForeignKey(
                        name: "FK_article_tags_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "public",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "article_versions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    published_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_article_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_article_versions_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "public",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_articles_author_id",
                schema: "public",
                table: "articles",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_articles_created_at",
                schema: "public",
                table: "articles",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_articles_status",
                schema: "public",
                table: "articles",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_articles_title",
                schema: "public",
                table: "articles",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_article_versions_article_id_version_number",
                schema: "public",
                table: "article_versions",
                columns: new[] { "article_id", "version_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "article_tags",
                schema: "public");

            migrationBuilder.DropTable(
                name: "article_versions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "articles",
                schema: "public");
        }
    }
}
