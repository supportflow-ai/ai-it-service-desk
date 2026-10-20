using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Ticketing.ResolveTicket;

public record ResolveTicketCommand(Guid Id);

public interface IResolveTicket
{
    Task<TicketDto> ExecuteAsync(ResolveTicketCommand command, CancellationToken cancellationToken = default);
}

internal sealed class ResolveTicketUseCase(ITicketWriter writer, ICurrentUser currentUser, IClock clock) : IResolveTicket
{
    public async Task<TicketDto> ExecuteAsync(ResolveTicketCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Only an agent or admin can resolve this ticket.");
        }

        try
        {
            ticket.Resolve(clock.UtcNow.UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message);
        }

        return await writer.UpdateTicketAsync(ticket, cancellationToken);
    }
}
