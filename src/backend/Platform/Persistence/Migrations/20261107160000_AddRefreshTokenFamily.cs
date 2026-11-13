using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <summary>
    /// Thêm chuỗi phiên cho refresh token: đăng xuất hoặc phát hiện token bị dùng lại thu hồi mọi token sinh từ cùng
    /// một lần đăng nhập. Token có sẵn được coi là đầu chuỗi của chính nó (family_id = id).
    /// </summary>
    public partial class AddRefreshTokenFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "family_id",
                schema: "public",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE public.refresh_tokens SET family_id = id WHERE family_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "family_id",
                schema: "public",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_family_id",
                schema: "public",
                table: "refresh_tokens",
                column: "family_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_family_id",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "family_id",
                schema: "public",
                table: "refresh_tokens");
        }
    }
}
