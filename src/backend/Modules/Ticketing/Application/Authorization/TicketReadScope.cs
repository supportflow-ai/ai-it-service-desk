using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Ticketing.Application.Authorization;

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

    /// <summary>Đọc phiếu thuộc nhóm đang hoạt động hoặc được phân công trực tiếp cho Agent.</summary>
    public sealed record AgentTickets : TicketReadScope
    {
        public Guid ActorId { get; }
        public bool IncludeOwnTickets { get; }

        public AgentTickets(Guid actorId, bool includeOwnTickets = false)
        {
            if (actorId == Guid.Empty) throw new ArgumentException("Yêu cầu danh tính Agent.", nameof(actorId));
            ActorId = actorId;
            IncludeOwnTickets = includeOwnTickets;
        }
    }

    /// <summary>Đọc toàn bộ phiếu trong workspace (Admin).</summary>
    public sealed record AllTickets : TicketReadScope;
}

/// <summary>Tiện ích kiểm tra danh tính và vai trò của actor thao tác ticket.</summary>
internal static class TicketActor
{
    public static Guid RequireId(ICurrentUser currentUser)
    {
        var id = AuthenticatedActor.RequireId(currentUser);
        if (!currentUser.Roles.Any(RoleNames.All.Contains))
            throw new ForbiddenException("ticket_role_required", "Yêu cầu vai trò hợp lệ trong hệ thống.");
        return id;
    }
}
