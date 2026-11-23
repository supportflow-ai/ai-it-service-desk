using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.AddInternalNote;

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

        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff)
        {
            throw new UnauthorizedAccessException("Requester không có quyền tạo ghi chú nội bộ.");
        }

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId);
        var ticket = await writer.GetForUpdateAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        var authorName = await agentDirectory.GetDisplayNameAsync(actorId, cancellationToken);
        var authorRole = currentUser.Roles.FirstOrDefault() ?? "Agent";
        var now = clock.UtcNow.UtcDateTime;

        // BIZ-02: Chống gửi ghi chú nội bộ trùng lặp cùng tác giả/vé trong 30 giây
        var recentDuplicate = await timelineStore.FindRecentDuplicateAsync(
            command.TicketId,
            actorId,
            command.Content,
            now.AddSeconds(-30),
            cancellationToken);

        if (recentDuplicate is not null)
        {
            return recentDuplicate;
        }

        var comment = TicketComment.CreateInternalNote(
            command.TicketId,
            actorId,
            authorName,
            authorRole,
            command.Content,
            now);

        return await timelineStore.AddAsync(comment, cancellationToken);
    }
}
