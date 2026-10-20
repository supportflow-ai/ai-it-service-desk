using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.ClaimTicket;

public record ClaimTicketCommand(Guid Id);

public interface IClaimTicket
{
    Task<TicketDto> ExecuteAsync(ClaimTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class ClaimTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : IClaimTicket
{
    public async Task<TicketDto> ExecuteAsync(ClaimTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        if (!currentUser.IsInRole(RoleNames.Agent))
        {
            throw new UnauthorizedAccessException("Agent role is required to claim a ticket.");
        }

        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        try
        {
            ticket.Claim(clock.UtcNow.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
