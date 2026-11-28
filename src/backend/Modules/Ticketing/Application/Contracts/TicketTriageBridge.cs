using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

/// <summary>
/// Snapshot phiếu cho AI phân loại, không làm rò rỉ thực thể Ticket.
/// <see cref="CanTriage"/> do Ticketing tính: người dùng hiện tại là Agent/Admin và phiếu thuộc phạm vi xử lý
/// (quyền của chủ phiếu không tính, theo ADR-016).
/// </summary>
public sealed record TicketTriageSnapshotDto(
    Guid TicketId,
    long Revision,
    string Title,
    string Description,
    string CategoryId,
    Guid RequesterId,
    bool CanTriage
);

/// <summary>
/// Cổng Ticketing công bố cho AIAssistance. Mọi kiểm quyền đọc/ghi phiếu thực hiện bên trong theo
/// <c>ICurrentUser</c>; AIAssistance không tự diễn giải chính sách quyền của phiếu.
/// </summary>
public interface ITicketTriageBridge
{
    /// <summary>Trả null khi phiếu không tồn tại hoặc nằm ngoài phạm vi đọc của người dùng hiện tại.</summary>
    Task<TicketTriageSnapshotDto?> GetSnapshotAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi Impact, Urgency và (nếu có) Category thay người dùng hiện tại, kèm audit hệ thống.
    /// Kiểm lại vai trò và phạm vi xử lý tại thời điểm ghi; ném 403/404/409/400 theo quy ước của Ticketing.
    /// </summary>
    Task ApplyTriageDecisionAsync(
        Guid ticketId,
        Impact impact,
        Urgency urgency,
        string? categoryId,
        string auditReason,
        uint? expectedRevision,
        CancellationToken cancellationToken = default);
}
