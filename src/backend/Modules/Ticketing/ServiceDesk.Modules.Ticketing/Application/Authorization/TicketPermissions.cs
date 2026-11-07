using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.Authorization;

/// <summary>
/// Quy tắc quyền dùng chung của Ticketing; gom các phép kiểm vai trò lặp lại trong use case
/// để ma trận quyền đọc được ở một chỗ. Hành vi giống hệt các phép kiểm trước đây.
/// </summary>
internal static class TicketPermissions
{
    /// <summary>Agent hoặc Admin: được xem toàn bộ hàng đợi và xử lý ticket.</summary>
    public static bool IsStaff(ICurrentUser currentUser) =>
        currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);

    /// <summary>Admin: được thao tác thay chủ sở hữu và tiếp nhận ticket đã phân công cho người khác.</summary>
    public static bool IsAdmin(ICurrentUser currentUser) => currentUser.IsInRole(RoleNames.Admin);

    /// <summary>Chủ sở hữu ticket hoặc Admin: được cập nhật, mở lại, đóng, hủy ticket.</summary>
    public static bool CanActAsOwner(Ticket ticket, Guid actorId, ICurrentUser currentUser) =>
        ticket.IsOwnedBy(actorId) || IsAdmin(currentUser);
}
