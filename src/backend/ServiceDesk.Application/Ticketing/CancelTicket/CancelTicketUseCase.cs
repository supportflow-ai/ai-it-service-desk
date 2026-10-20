using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.CancelTicket;

public record CancelTicketCommand(Guid Id);

public interface ICancelTicket
{
    Task<TicketDto> ExecuteAsync(CancelTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class CancelTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : ICancelTicket
{
    public async Task<TicketDto> ExecuteAsync(CancelTicketCommand command, CancellationToken cancellationToken = default)
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
            ticket.Cancel(clock.UtcNow.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
