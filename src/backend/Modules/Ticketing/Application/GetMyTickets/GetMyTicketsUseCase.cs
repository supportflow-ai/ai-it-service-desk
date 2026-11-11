using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.GetMyTickets;

/// <summary>Truy vấn lấy danh sách vé của người dùng hiện tại có phân trang.</summary>
public sealed record GetMyTicketsQuery(int Page = 1, int PageSize = 10);

/// <summary>Hợp đồng use case lấy danh sách vé cá nhân.</summary>
public interface IGetMyTickets
{
    Task<PagedTicketResult> ExecuteAsync(GetMyTicketsQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Xử lý lấy danh sách vé thuộc quyền sở hữu của Requester hiện tại:
/// - Xác thực danh tính caller.
/// - Kiểm tra ràng buộc phân trang (Page >= 1, 1 <= PageSize <= 100, chống tràn số offset).
/// - Luôn ép phạm vi tìm kiếm theo RequesterId của caller (không bao giờ lộ vé người khác).
/// </summary>
internal sealed class GetMyTicketsUseCase(ITicketQueries queries, ICurrentUser currentUser) : IGetMyTickets
{
    public Task<PagedTicketResult> ExecuteAsync(GetMyTicketsQuery query, CancellationToken cancellationToken = default)
    {
        var requester = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100 || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
            throw TicketErrors.PagingInvalid();

        return queries.GetForRequesterAsync(requester, query.Page, query.PageSize, cancellationToken);
    }
}
