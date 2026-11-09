using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

/// <summary>
/// Đọc trạng thái phiếu trong giao dịch hiện tại và giữ khóa tới khi giao dịch kết thúc.
/// Dùng khóa chia sẻ cho bình luận; xác nhận tệp cần khóa ghi để tuần tự hóa việc kiểm hạn mức theo phiếu.
/// </summary>
internal static class TicketRowLock
{
    public static async Task RequireOpenAsync(DbContext context, Guid ticketId, string terminalMessage,
        CancellationToken cancellationToken, bool serializeWrites = false)
    {
        var query = serializeWrites
            ? context.Database.SqlQuery<int>($"SELECT status AS \"Value\" FROM public.tickets WHERE \"Id\" = {ticketId} FOR NO KEY UPDATE")
            : context.Database.SqlQuery<int>($"SELECT status AS \"Value\" FROM public.tickets WHERE \"Id\" = {ticketId} FOR SHARE");
        // ToListAsync giữ nguyên câu lệnh lấy khóa, không bọc vào truy vấn con.
        var status = (await query.ToListAsync(cancellationToken)).SingleOrDefault(-1);
        if (status < 0) throw new KeyNotFoundException("Ticket not found.");
        if (status is (int)TicketStatus.Closed or (int)TicketStatus.Cancelled) throw new ArgumentException(terminalMessage);
    }
}
