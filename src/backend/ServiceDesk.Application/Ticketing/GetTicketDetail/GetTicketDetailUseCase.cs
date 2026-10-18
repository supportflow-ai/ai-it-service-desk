using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.GetTicketDetail;

public sealed record GetTicketDetailQuery(Guid Id);

public interface IGetTicketDetail
{
    Task<TicketDto?> ExecuteAsync(GetTicketDetailQuery query, CancellationToken cancellationToken = default);
}

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
