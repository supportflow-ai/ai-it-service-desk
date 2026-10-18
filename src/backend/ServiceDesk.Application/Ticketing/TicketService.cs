using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing;

public sealed class TicketService(ITicketWriter writer, ITicketQueries queries, ICurrentUser currentUser, IClock clock) : ITicketService
{
    public Task<TicketDto> CreateTicketAsync(CreateTicketCommand command, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var requester = RequireRequester();
        ArgumentNullException.ThrowIfNull(command);
        var ticket = Ticket.Create(requester, command.Title, command.Description, command.CategoryId, clock.UtcNow.UtcDateTime);
        var fingerprint = idempotencyKey is null ? null : CreateTicketFingerprint.Create(requester, idempotencyKey, command);
        return writer.CreateAsync(ticket, fingerprint, cancellationToken);
    }

    public Task<PagedTicketResult> GetMyTicketsAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var requester = RequireRequester();
        if (page < 1 || pageSize < 1 || pageSize > 100 || ((long)page - 1) * pageSize > int.MaxValue)
            throw new ArgumentException("page must be at least 1 and pageSize must be between 1 and 100");
        return queries.GetForRequesterAsync(requester, page, pageSize, cancellationToken);
    }

    public Task<TicketDto?> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var requester = RequireRequester();
        Guid? owner = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin) ? null : requester;
        return queries.GetByIdAsync(id, owner, cancellationToken);
    }

    private Guid RequireRequester()
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var id) || id == Guid.Empty)
            throw new UnauthorizedAccessException("Authenticated requester identity is required.");
        if (!currentUser.Roles.Any(RoleNames.All.Contains))
            throw new UnauthorizedAccessException("Requester role is required.");
        return id;
    }
}
