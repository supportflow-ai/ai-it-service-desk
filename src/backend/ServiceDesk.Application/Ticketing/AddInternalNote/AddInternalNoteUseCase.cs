using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.AddInternalNote;

public record AddInternalNoteCommand(Guid TicketId, string Content);

public interface IAddInternalNote
{
    Task<TimelineItemDto> ExecuteAsync(AddInternalNoteCommand command, CancellationToken cancellationToken = default);
}

internal sealed class AddInternalNoteUseCase(
    ITicketWriter writer,
    ITicketTimelineStore timelineStore,
    IAgentDirectory agentDirectory,
    ICurrentUser currentUser,
    IClock clock) : IAddInternalNote
{
    public async Task<TimelineItemDto> ExecuteAsync(AddInternalNoteCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var isStaff = currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Requester không có quyền tạo ghi chú nội bộ.");
        }

        var ticket = await writer.GetForUpdateAsync(command.TicketId, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        var authorName = await agentDirectory.GetDisplayNameAsync(actorId, cancellationToken);
        var authorRole = currentUser.Roles.FirstOrDefault() ?? "Agent";

        var comment = TicketComment.CreateInternalNote(
            command.TicketId,
            actorId,
            authorName,
            authorRole,
            command.Content,
            clock.UtcNow.UtcDateTime);

        return await timelineStore.AddAsync(comment, cancellationToken);
    }
}
