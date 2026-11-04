using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkAttachmentUploadGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "attachment_id",
                schema: "public",
                table: "attachment_upload_grants",
                type: "uuid",
                nullable: true);

            // Nối grant cũ đã consume khi có duy nhất một attachment khớp toàn bộ metadata.
            // Giữ nguyên tệp/key và các grant thiếu hoặc mơ hồ; không tự đoán chủ sở hữu.
            migrationBuilder.Sql("""
                WITH matched AS (
                    SELECT g."Id" AS grant_id, MIN(a."Id"::text)::uuid AS attachment_id
                    FROM public.attachment_upload_grants g
                    JOIN public.ticket_attachments a
                      ON a.ticket_id = g.ticket_id
                     AND a.uploaded_by = g.actor_id
                     AND a.storage_key = g.storage_key
                     AND a.file_name = g.file_name
                     AND a.file_size_bytes = g.file_size_bytes
                     AND lower(a.content_type) = lower(g.content_type)
                    WHERE g.consumed_at IS NOT NULL
                    GROUP BY g."Id"
                    HAVING COUNT(*) = 1
                )
                UPDATE public.attachment_upload_grants g
                   SET attachment_id = matched.attachment_id
                  FROM matched
                 WHERE g."Id" = matched.grant_id AND g.attachment_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_attachment_upload_grants_actor_id_expires_at",
                schema: "public",
                table: "attachment_upload_grants",
                columns: new[] { "actor_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_attachment_upload_grants_attachment_id",
                schema: "public",
                table: "attachment_upload_grants",
                column: "attachment_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_attachment_upload_grants_ticket_attachments_attachment_id",
                schema: "public",
                table: "attachment_upload_grants",
                column: "attachment_id",
                principalSchema: "public",
                principalTable: "ticket_attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachment_upload_grants_ticket_attachments_attachment_id",
                schema: "public",
                table: "attachment_upload_grants");

            migrationBuilder.DropIndex(
                name: "IX_attachment_upload_grants_actor_id_expires_at",
                schema: "public",
                table: "attachment_upload_grants");

            migrationBuilder.DropIndex(
                name: "IX_attachment_upload_grants_attachment_id",
                schema: "public",
                table: "attachment_upload_grants");

            migrationBuilder.DropColumn(
                name: "attachment_id",
                schema: "public",
                table: "attachment_upload_grants");
        }
    }
}
