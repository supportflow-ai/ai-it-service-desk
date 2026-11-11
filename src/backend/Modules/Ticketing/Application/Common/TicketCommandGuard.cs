using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Ticketing.Application.Common;

/// <summary>
/// Các bước chung của lệnh ghi ticket, dùng lại ở mọi use case thay đổi ticket:
/// kiểm revision mong đợi và lưu kèm bản ghi audit của hệ thống. Quy tắc trạng thái do Domain tự ném
/// <see cref="RuleViolationException"/> nên không cần bước đổi kiểu lỗi ở đây.
/// Kiểm quyền và tải ticket vẫn nằm trong từng use case vì mỗi lệnh có luật riêng.
/// </summary>
internal static class TicketCommandGuard
{
    /// <summary>Thông điệp khi revision mong đợi khác revision hiện tại (409).</summary>
    internal const string StaleRevisionMessage =
        "Dữ liệu đã bị thay đổi bởi người khác hoặc phiên làm việc đã cũ. Vui lòng tải lại trang.";

    /// <summary>Bắt buộc có revision mong đợi khác 0 và khớp revision hiện tại của ticket.</summary>
    /// <exception cref="InvalidInputException">Thiếu revision mong đợi (xem <see cref="ExpectedRevision"/>).</exception>
    /// <exception cref="ConcurrencyConflictException">Revision đã thay đổi.</exception>
    public static void RequireCurrentRevision(ITicketWriter writer, Ticket ticket, uint? expectedRevision)
    {
        ExpectedRevision.Require(expectedRevision, () => writer.GetRevision(ticket), StaleRevisionMessage);
    }

    /// <summary>Lưu ticket cùng một bình luận công khai do hệ thống ghi lại thao tác của <paramref name="actorId"/>.</summary>
    public static Task<TicketDto> SaveWithSystemAuditAsync(
        ITicketWriter writer, Ticket ticket, Guid actorId, string message, DateTime now, CancellationToken cancellationToken)
    {
        var audit = TicketComment.CreatePublic(ticket.Id, actorId, SystemAuthor.Name, SystemAuthor.Role, message, now);
        return writer.UpdateTicketWithAuditAsync(ticket, audit, cancellationToken);
    }
}

/// <summary>Tác giả của bản ghi audit do hệ thống sinh ra trên dòng thời gian ticket.</summary>
internal static class SystemAuthor
{
    public const string Name = "Hệ thống";
    public const string Role = "System";
}
