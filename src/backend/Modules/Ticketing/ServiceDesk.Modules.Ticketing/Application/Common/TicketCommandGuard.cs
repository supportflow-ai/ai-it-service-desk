using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Ticketing.Application.Common;

/// <summary>
/// Các bước chung của lệnh ghi ticket, dùng lại ở mọi use case thay đổi ticket:
/// kiểm revision mong đợi, đổi lỗi quy tắc Domain thành lỗi đầu vào (400) và lưu kèm bản ghi audit của hệ thống.
/// Kiểm quyền và tải ticket vẫn nằm trong từng use case vì mỗi lệnh có luật riêng.
/// </summary>
internal static class TicketCommandGuard
{
    /// <summary>Thông điệp khi revision mong đợi khác revision hiện tại (409).</summary>
    internal const string StaleRevisionMessage =
        "Dữ liệu đã bị thay đổi bởi người khác hoặc phiên làm việc đã cũ. Vui lòng tải lại trang.";

    /// <summary>Bắt buộc có revision mong đợi khác 0 và khớp revision hiện tại của ticket.</summary>
    /// <exception cref="ArgumentException">Thiếu revision mong đợi.</exception>
    /// <exception cref="ConcurrencyConflictException">Revision đã thay đổi.</exception>
    public static void RequireCurrentRevision(ITicketWriter writer, Ticket ticket, uint? expectedRevision)
    {
        if (!expectedRevision.HasValue || expectedRevision.Value == 0)
        {
            throw new ArgumentException("Expected revision is required.");
        }

        if (writer.GetRevision(ticket) != expectedRevision.Value)
        {
            throw new ConcurrencyConflictException(StaleRevisionMessage + $" STALE[expected={expectedRevision.Value} entity={writer.GetRevision(ticket)} status={ticket.Status} team={ticket.SupportTeamId}]");
        }
    }

    /// <summary>
    /// Thực hiện thay đổi trên aggregate; vi phạm quy tắc (<see cref="InvalidOperationException"/>, và
    /// <see cref="ArgumentException"/> khi <paramref name="includeArgumentErrors"/> bật) trả về dạng <see cref="ArgumentException"/>
    /// với cùng thông điệp để giữ mã HTTP 400 hiện có.
    /// </summary>
    public static void ApplyRule(Ticket ticket, Action<Ticket> change, bool includeArgumentErrors = false)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(change);
        try
        {
            change(ticket);
        }
        catch (Exception ex) when (ex is InvalidOperationException || (includeArgumentErrors && ex is ArgumentException))
        {
            throw new ArgumentException(ex.Message);
        }
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
