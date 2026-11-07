using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

/// <summary>
/// Đọc trạng thái ticket với khóa chia sẻ (<c>FOR SHARE</c>) trong giao dịch hiện tại. Lệnh đổi trạng thái (UPDATE dòng
/// ticket) phải chờ giao dịch này commit, còn lệnh đã commit trước thì được thấy ở đây; dùng cho mọi thao tác ghi chỉ
/// được phép khi ticket chưa đóng hoặc hủy.
/// </summary>
internal static class TicketRowLock
{
    public static async Task RequireOpenAsync(DbContext context, Guid ticketId, string terminalMessage, CancellationToken cancellationToken)
    {
        // ToListAsync không bọc câu lệnh trong truy vấn con nên FOR SHARE giữ nguyên.
        var status = (await context.Database.SqlQuery<int>(
                $"SELECT status AS \"Value\" FROM public.tickets WHERE \"Id\" = {ticketId} FOR SHARE")
            .ToListAsync(cancellationToken)).SingleOrDefault(-1);
        if (status < 0) throw new KeyNotFoundException("Ticket not found.");
        if (status is (int)TicketStatus.Closed or (int)TicketStatus.Cancelled) throw new ArgumentException(terminalMessage);
    }
}
