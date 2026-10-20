using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.UpdateTicket;

public record UpdateTicketCommand(Guid Id, string Title, string Description, string CategoryId);

public interface IUpdateTicket
{
    Task<TicketDto> ExecuteAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class UpdateTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : IUpdateTicket
{
    public async Task<TicketDto> ExecuteAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default)
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
            ticket.Update(command.Title, command.Description, command.CategoryId, clock.UtcNow.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
