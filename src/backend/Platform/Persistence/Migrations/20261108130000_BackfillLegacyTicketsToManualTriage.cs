using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Platform.Persistence.Migrations
{
    /// <summary>
    /// Chuyển phiếu cũ chưa thuộc nhóm nào và chưa kết thúc vào hàng đợi phân loại thủ công (ADR-016): mọi trạng thái
    /// trừ Closed (5) và Cancelled (6), theo giá trị số đã lưu. Phiếu Closed/Cancelled lịch sử giữ null.
    /// Chỉ ghi cột support_team_id: không suy nhóm từ phòng ban, danh mục hay người phụ trách; không đổi người gửi,
    /// danh mục, người phụ trách, mức ưu tiên, trạng thái hay updated_at. Phiếu đã có nhóm không bị ghi đè nên câu
    /// lệnh chạy lại không đổi thêm gì. Lưu ý: UPDATE làm xmin (revision) của các dòng bị ảnh hưởng đổi một lần,
    /// client giữ revision cũ sẽ nhận 409 và tải lại. Down không hoàn tác (không phân biệt được dòng nào vốn là null).
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261108130000_BackfillLegacyTicketsToManualTriage")]
    public sealed class BackfillLegacyTicketsToManualTriage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public.tickets
                SET support_team_id = '5b1f0c2e-7a64-4d3b-9e18-2c6a8f4d1b07'
                WHERE support_team_id IS NULL
                  AND status NOT IN (5, 6);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
