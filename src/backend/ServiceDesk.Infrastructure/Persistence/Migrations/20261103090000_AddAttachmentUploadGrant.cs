using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ServiceDesk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261103090000_AddAttachmentUploadGrant")]
public sealed class AddAttachmentUploadGrant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "attachment_upload_grants",
            schema: "public",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_attachment_upload_grants", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_attachment_upload_grants_storage_key",
            schema: "public",
            table: "attachment_upload_grants",
            column: "storage_key",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(
        name: "attachment_upload_grants", schema: "public");
}
