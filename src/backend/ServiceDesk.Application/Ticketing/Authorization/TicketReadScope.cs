using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.Authorization;

/// <summary>
/// Định nghĩa phạm vi đọc dữ liệu vé (Scope) dựa trên vai trò của người gọi.
/// </summary>
public abstract record TicketReadScope
{
    private TicketReadScope() { }

    /// <summary>Chỉ đọc vé do chính người dùng sở hữu (Requester scope).</summary>
    public sealed record OwnTickets : TicketReadScope
    {
        public Guid ActorId { get; }
        public OwnTickets(Guid actorId)
        {
            if (actorId == Guid.Empty) throw new ArgumentException("Yêu cầu danh tính chủ sở hữu.", nameof(actorId));
            ActorId = actorId;
        }
    }

    /// <summary>Đọc toàn bộ vé trong workspace (Agent / Admin scope).</summary>
    public sealed record AllTickets : TicketReadScope;
}

/// <summary>Tiện ích kiểm tra danh tính và vai trò của actor thao tác ticket.</summary>
internal static class TicketActor
{
    public static Guid RequireId(ICurrentUser currentUser)
    {
        var id = AuthenticatedActor.RequireId(currentUser);
        if (!currentUser.Roles.Any(RoleNames.All.Contains))
            throw new UnauthorizedAccessException("Yêu cầu vai trò hợp lệ trong hệ thống.");
        return id;
    }
}
