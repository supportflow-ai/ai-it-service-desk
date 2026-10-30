using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Authorization;
using ServiceDesk.Application.Ticketing.Contracts;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing.AddComment;

public record AddCommentCommand(Guid TicketId, string Content);

public interface IAddComment
{
    Task<TimelineItemDto> ExecuteAsync(AddCommentCommand command, CancellationToken cancellationToken = default);
}

internal sealed class AddCommentUseCase(
    ITicketWriter writer,
    ITicketTimelineStore timelineStore,
    IAgentDirectory agentDirectory,
    ICurrentUser currentUser,
    IClock clock) : IAddComment
{
    public async Task<TimelineItemDto> ExecuteAsync(AddCommentCommand command, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await writer.GetForUpdateAsync(command.TicketId, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        var authorName = await agentDirectory.GetDisplayNameAsync(actorId, cancellationToken);
        var authorRole = currentUser.Roles.FirstOrDefault() ?? "Requester";

        var comment = TicketComment.CreatePublic(
            command.TicketId,
            actorId,
            authorName,
            authorRole,
            command.Content,
            clock.UtcNow.UtcDateTime);

        return await timelineStore.AddAsync(comment, cancellationToken);
    }
}
