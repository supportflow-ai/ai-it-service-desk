using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;

namespace ServiceDesk.Application.Ticketing.GetMyTickets;

public sealed record GetMyTicketsQuery(int Page = 1, int PageSize = 10);

public interface IGetMyTickets
{
    Task<PagedTicketResult> ExecuteAsync(GetMyTicketsQuery query, CancellationToken cancellationToken = default);
}

internal sealed class GetMyTicketsUseCase(ITicketQueries queries, ICurrentUser currentUser) : IGetMyTickets
{
    public Task<PagedTicketResult> ExecuteAsync(GetMyTicketsQuery query, CancellationToken cancellationToken = default)
    {
        var requester = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100 || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
            throw new ArgumentException("page must be at least 1 and pageSize must be between 1 and 100");
        return queries.GetForRequesterAsync(requester, query.Page, query.PageSize, cancellationToken);
    }
}
