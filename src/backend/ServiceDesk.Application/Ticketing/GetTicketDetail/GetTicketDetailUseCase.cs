using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.GetTicketDetail;

/// <summary>Truy vấn thông tin chi tiết của một ticket theo Id.</summary>
public sealed record GetTicketDetailQuery(Guid Id);

/// <summary>Hợp đồng use case lấy chi tiết ticket.</summary>
public interface IGetTicketDetail
{
    Task<TicketDto?> ExecuteAsync(GetTicketDetailQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý lấy chi tiết ticket theo phạm vi quyền (Scope):
/// - Requester: Chỉ đọc được ticket do chính mình tạo (nếu xem vé người khác -> trả về null / 404).
/// - Agent / Admin: Đọc được vé trên toàn bộ hệ thống.
/// </summary>
internal sealed class GetTicketDetailUseCase(ITicketQueries queries, ICurrentUser currentUser) : IGetTicketDetail
{
    public Task<TicketDto?> ExecuteAsync(GetTicketDetailQuery query, CancellationToken cancellationToken = default)
    {
        var requester = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);
        TicketReadScope scope = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin)
            ? new TicketReadScope.AllTickets() : new TicketReadScope.OwnTickets(requester);
        return queries.GetByIdAsync(query.Id, scope, cancellationToken);
    }
}
