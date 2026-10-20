using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.ReopenTicket;

public record ReopenTicketCommand(Guid Id);

public interface IReopenTicket
{
    Task<TicketDto> ExecuteAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class ReopenTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : IReopenTicket
{
    public async Task<TicketDto> ExecuteAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null || (!ticket.IsOwnedBy(actorId) && !currentUser.IsInRole(RoleNames.Admin)))
        {
            throw new KeyNotFoundException("Ticket not found or outside access scope.");
        }

        try
        {
            ticket.Reopen(clock.UtcNow.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
