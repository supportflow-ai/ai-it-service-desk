using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.Common;

/// <summary>
/// Khung chung của mọi lệnh chuyển trạng thái phiếu. Mỗi use case chỉ khai báo phần riêng của mình
/// (ai được thao tác, điều kiện trước khi kiểm revision, thao tác Domain, lời nhắn audit); các bước còn lại
/// luôn chạy theo MỘT thứ tự duy nhất:
/// <list type="number">
/// <item>Xác định danh tính người gọi (<see cref="TicketActor.RequireId"/>).</item>
/// <item>Kiểm vai trò: lệnh của nhân viên yêu cầu Agent/Admin (403); lệnh của chủ sở hữu kiểm ở bước 3.</item>
/// <item>Tải phiếu trong phạm vi quyền đọc để cập nhật; không có hoặc không phải chủ sở hữu thì 404, không lộ sự tồn tại.</item>
/// <item>Điều kiện riêng của lệnh (nếu có), vẫn chạy trước kiểm revision như trước đây.</item>
/// <item>Kiểm <c>expectedRevision</c> (400 nếu thiếu, 409 nếu cũ).</item>
/// <item>Áp dụng quy tắc Domain (Domain tự ném <see cref="RuleViolationException"/>).</item>
/// <item>Lưu cùng bản ghi audit của hệ thống (trừ lệnh cập nhật nội dung, không ghi audit).</item>
/// </list>
/// </summary>
internal sealed class TicketTransitions(ITicketWriter writer, ICurrentUser currentUser, IClock clock)
{
    /// <summary>Lệnh của nhân viên (Agent/Admin) trong phạm vi quyền đọc của họ.</summary>
    /// <param name="staffAction">Cụm động từ cho thông điệp 403, ví dụ "giải quyết phiếu".</param>
    /// <param name="apply">Thao tác Domain: (phiếu, mã người thực hiện, thời điểm hiện tại).</param>
    /// <param name="beforeRevisionCheck">Điều kiện riêng của lệnh; ném lỗi để từ chối.</param>
    public async Task<TicketDto> RunAsStaffAsync(
        Guid ticketId,
        uint? expectedRevision,
        string staffAction,
        Action<Ticket, Guid, DateTime> apply,
        string auditMessage,
        CancellationToken cancellationToken,
        Func<Ticket, Guid, CancellationToken, Task>? beforeRevisionCheck = null)
    {
        var actorId = TicketActor.RequireId(currentUser);
        if (!TicketPermissions.IsStaff(currentUser))
        {
            throw TicketErrors.StaffRequired(staffAction);
        }

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(ticketId, scope, cancellationToken)
            ?? throw TicketErrors.NotFound();

        if (beforeRevisionCheck is not null)
        {
            await beforeRevisionCheck(ticket, actorId, cancellationToken);
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, expectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        apply(ticket, actorId, now);

        return await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, auditMessage, now, cancellationToken);
    }

    /// <summary>Lệnh của chủ sở hữu phiếu hoặc Admin.</summary>
    /// <param name="apply">Thao tác Domain: (phiếu, thời điểm hiện tại).</param>
    /// <param name="auditMessage">Lời nhắn audit của hệ thống; <c>null</c> nếu lệnh không ghi audit.</param>
    public async Task<TicketDto> RunAsOwnerAsync(
        Guid ticketId,
        uint? expectedRevision,
        Action<Ticket, DateTime> apply,
        string? auditMessage,
        CancellationToken cancellationToken)
    {
        var actorId = TicketActor.RequireId(currentUser);

        var scope = TicketPermissions.OwnerScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(ticketId, scope, cancellationToken);
        if (ticket is null || !TicketPermissions.CanActAsOwner(ticket, actorId, currentUser))
        {
            throw TicketErrors.NotFound();
        }

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, expectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        apply(ticket, now);

        return auditMessage is null
            ? await writer.UpdateTicketAsync(ticket, cancellationToken)
            : await TicketCommandGuard.SaveWithSystemAuditAsync(writer, ticket, actorId, auditMessage, now, cancellationToken);
    }
}
