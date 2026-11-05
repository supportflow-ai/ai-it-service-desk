using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeIndexPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_article_versions_article_id_version_number",
                schema: "public",
                table: "article_versions");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "knowledge_index_jobs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProfileId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProfileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_index_jobs", x => x.Id);
                    table.CheckConstraint("CK_index_job_status", "\"Status\" IN ('Pending','Running','Succeeded','Failed','Superseded')");
                    table.ForeignKey(
                        name: "FK_knowledge_index_jobs_article_versions_ArticleVersionId",
                        column: x => x.ArticleVersionId,
                        principalSchema: "public",
                        principalTable: "article_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_index_jobs_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalSchema: "public",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_index_sets",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProfileId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProfileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProfileJson = table.Column<string>(type: "jsonb", nullable: false),
                    Dimension = table.Column<int>(type: "integer", nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_index_sets", x => x.Id);
                    table.CheckConstraint("CK_index_set_dimension", "\"Dimension\" = 384");
                    table.ForeignKey(
                        name: "FK_knowledge_index_sets_article_versions_ArticleVersionId",
                        column: x => x.ArticleVersionId,
                        principalSchema: "public",
                        principalTable: "article_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_index_sets_knowledge_index_jobs_Id",
                        column: x => x.Id,
                        principalSchema: "public",
                        principalTable: "knowledge_index_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                schema: "public",
                columns: table => new
                {
                    IndexSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    ChunkId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    TextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartChar = table.Column<int>(type: "integer", nullable: false),
                    EndChar = table.Column<int>(type: "integer", nullable: false),
                    StartLine = table.Column<int>(type: "integer", nullable: false),
                    EndLine = table.Column<int>(type: "integer", nullable: false),
                    HeadingPath = table.Column<string[]>(type: "text[]", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunks", x => new { x.IndexSetId, x.Index });
                    table.CheckConstraint("CK_chunk_offsets", "\"StartChar\" >= 0 AND \"EndChar\" > \"StartChar\" AND \"Index\" >= 0");
                    table.ForeignKey(
                        name: "FK_knowledge_chunks_knowledge_index_sets_IndexSetId",
                        column: x => x.IndexSetId,
                        principalSchema: "public",
                        principalTable: "knowledge_index_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_index_states",
                schema: "public",
                columns: table => new
                {
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActiveIndexSetId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_index_states", x => x.ArticleId);
                    table.ForeignKey(
                        name: "FK_knowledge_index_states_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalSchema: "public",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_index_states_knowledge_index_jobs_RequestedJobId",
                        column: x => x.RequestedJobId,
                        principalSchema: "public",
                        principalTable: "knowledge_index_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_index_states_knowledge_index_sets_ActiveIndexSetId",
                        column: x => x.ActiveIndexSetId,
                        principalSchema: "public",
                        principalTable: "knowledge_index_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_article_versions_article_id_version_number",
                schema: "public",
                table: "article_versions",
                columns: new[] { "article_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_IndexSetId_ChunkId",
                schema: "public",
                table: "knowledge_chunks",
                columns: new[] { "IndexSetId", "ChunkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_index_jobs_ArticleId",
                schema: "public",
                table: "knowledge_index_jobs",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_index_jobs_ArticleVersionId_ProfileHash",
                schema: "public",
                table: "knowledge_index_jobs",
                columns: new[] { "ArticleVersionId", "ProfileHash" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_index_jobs_Status_NextAttemptAt_CreatedAt",
                schema: "public",
                table: "knowledge_index_jobs",
                columns: new[] { "Status", "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_index_sets_ArticleId_VersionNumber_ProfileHash",
                schema: "public",
                table: "knowledge_index_sets",
                columns: new[] { "ArticleId", "VersionNumber", "ProfileHash" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_index_sets_ArticleVersionId",
                schema: "public",
                table: "knowledge_index_sets",
                column: "ArticleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_index_states_ActiveIndexSetId",
                schema: "public",
                table: "knowledge_index_states",
                column: "ActiveIndexSetId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_index_states_RequestedJobId",
                schema: "public",
                table: "knowledge_index_states",
                column: "RequestedJobId");

            // Deliberately owned SQL table: the pgvector type does not leak into EF/Application.
            migrationBuilder.Sql("""
                CREATE TABLE public.knowledge_chunk_vectors (
                    "IndexSetId" uuid NOT NULL,
                    "ChunkIndex" integer NOT NULL,
                    "Embedding" vector(384) NOT NULL,
                    PRIMARY KEY ("IndexSetId", "ChunkIndex"),
                    FOREIGN KEY ("IndexSetId", "ChunkIndex")
                        REFERENCES public.knowledge_chunks ("IndexSetId", "Index") ON DELETE CASCADE
                );
                CREATE VIEW public.knowledge_active_chunks AS
                SELECT c.*, s."ArticleId", s."ArticleVersionId", s."VersionNumber",
                       s."ProfileId", s."ProfileHash", v."Embedding"
                FROM public.knowledge_index_states AS state
                JOIN public.knowledge_index_sets AS s ON s."Id" = state."ActiveIndexSetId"
                JOIN public.articles AS a ON a."Id" = state."ArticleId" AND a."Id" = s."ArticleId"
                JOIN public.article_versions AS av ON av."Id" = s."ArticleVersionId"
                    AND av.article_id = a."Id" AND av.version_number = a.version
                JOIN public.knowledge_chunks AS c ON c."IndexSetId" = s."Id"
                JOIN public.knowledge_chunk_vectors AS v ON v."IndexSetId" = c."IndexSetId"
                    AND v."ChunkIndex" = c."Index"
                WHERE a.status = 1 AND a.version = s."VersionNumber";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW public.knowledge_active_chunks; DROP TABLE public.knowledge_chunk_vectors;");
            migrationBuilder.DropTable(
                name: "knowledge_chunks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "knowledge_index_states",
                schema: "public");

            migrationBuilder.DropTable(
                name: "knowledge_index_sets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "knowledge_index_jobs",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_article_versions_article_id_version_number",
                schema: "public",
                table: "article_versions");

            // Keep the shared vector extension installed; rollback removes only this feature's data.

            migrationBuilder.CreateIndex(
                name: "IX_article_versions_article_id_version_number",
                schema: "public",
                table: "article_versions",
                columns: new[] { "article_id", "version_number" });
        }
    }
}
