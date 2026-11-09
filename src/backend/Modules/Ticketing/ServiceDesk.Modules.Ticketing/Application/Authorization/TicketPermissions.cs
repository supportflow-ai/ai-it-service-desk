using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.Authorization;

/// <summary>
/// Quy tắc quyền dùng chung của Ticketing; gom các phép kiểm vai trò lặp lại trong use case
/// để ma trận quyền đọc được ở một chỗ.
/// </summary>
internal static class TicketPermissions
{
    /// <summary>Agent hoặc Admin: có thể dùng các chức năng dành cho nhân viên hỗ trợ.</summary>
    public static bool IsStaff(ICurrentUser currentUser) =>
        currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);

    /// <summary>Admin: được thao tác thay chủ sở hữu và tiếp nhận ticket đã phân công cho người khác.</summary>
    public static bool IsAdmin(ICurrentUser currentUser) => currentUser.IsInRole(RoleNames.Admin);

    public static TicketReadScope ReadScopeFor(ICurrentUser currentUser, Guid actorId, bool includeOwner = false)
    {
        if (IsAdmin(currentUser)) return new TicketReadScope.AllTickets();
        if (currentUser.IsInRole(RoleNames.Agent))
            return new TicketReadScope.AgentTickets(actorId, includeOwner);
        return new TicketReadScope.OwnTickets(actorId);
    }

    public static TicketReadScope OwnerScopeFor(ICurrentUser currentUser, Guid actorId) =>
        IsAdmin(currentUser) ? new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(actorId);

    /// <summary>Chủ sở hữu ticket hoặc Admin: được cập nhật, mở lại, đóng, hủy ticket.</summary>
    public static bool CanActAsOwner(Ticket ticket, Guid actorId, ICurrentUser currentUser) =>
        ticket.IsOwnedBy(actorId) || IsAdmin(currentUser);
}
