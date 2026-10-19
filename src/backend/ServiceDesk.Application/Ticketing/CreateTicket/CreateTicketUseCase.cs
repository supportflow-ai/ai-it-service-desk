using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Idempotency;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.CreateTicket;

public interface ICreateTicket
{
    Task<TicketDto> ExecuteAsync(CreateTicketCommand command, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}

internal sealed class CreateTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : ICreateTicket
{
    public Task<TicketDto> ExecuteAsync(CreateTicketCommand command, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var requester = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);
        var ticket = Ticket.Create(requester, command.Title, command.Description, command.CategoryId, clock.UtcNow.UtcDateTime);
        var fingerprint = idempotencyKey is null ? null : CreateTicketFingerprint.Create(requester, idempotencyKey, command);
        return writer.CreateAsync(ticket, fingerprint, cancellationToken);
    }
}
