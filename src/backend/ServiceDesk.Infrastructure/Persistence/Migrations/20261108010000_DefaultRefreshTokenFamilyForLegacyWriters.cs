using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Cho phép bản API cũ (chưa biết cột <c>family_id</c>) tiếp tục ghi <c>refresh_tokens</c> trong lúc triển khai cuốn
    /// chiếu: trigger BEFORE INSERT điền <c>family_id = id</c> khi câu lệnh không gửi giá trị, nên ràng buộc NOT NULL
    /// không làm lỗi đăng nhập của bản cũ. Bản mới luôn gửi <c>family_id</c> nên trigger không đổi giá trị của nó.
    /// Không đổi model EF.
    /// </summary>
    public partial class DefaultRefreshTokenFamilyForLegacyWriters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.refresh_tokens_default_family_id() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.family_id IS NULL THEN
                        NEW.family_id := NEW.id;
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER refresh_tokens_default_family_id
                    BEFORE INSERT ON public.refresh_tokens
                    FOR EACH ROW EXECUTE FUNCTION public.refresh_tokens_default_family_id();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS refresh_tokens_default_family_id ON public.refresh_tokens;
                DROP FUNCTION IF EXISTS public.refresh_tokens_default_family_id();
                """);
        }
    }
}
