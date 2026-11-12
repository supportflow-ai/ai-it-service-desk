using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.AddComment;

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

        var scope = TicketPermissions.ReadScopeFor(currentUser, actorId, includeOwner: true);
        var ticket = await writer.GetForUpdateAsync(command.TicketId, scope, cancellationToken);
        if (ticket == null) throw TicketErrors.NotFound();

        // Kiểm quyền xem trước trạng thái: ticket của người khác luôn trả 404, không lộ là đã đóng hay hủy.
        var isStaff = TicketPermissions.IsStaff(currentUser);
        if (!isStaff && !ticket.IsOwnedBy(actorId))
            throw TicketErrors.NotFound();

        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw TicketErrors.TicketClosed("Không thể thêm bình luận vào vé đã kết thúc.");

        var authorName = await agentDirectory.GetDisplayNameAsync(actorId, cancellationToken);
        var authorRole = currentUser.IsInRole(RoleNames.Admin) ? RoleNames.Admin
            : currentUser.IsInRole(RoleNames.Agent) ? RoleNames.Agent : RoleNames.Requester;

        var comment = TicketComment.CreatePublic(
            command.TicketId,
            actorId,
            authorName,
            authorRole,
            command.Content,
            clock.UtcNow.UtcDateTime);

        // Kiểm lại trạng thái trong giao dịch ghi: ticket có thể vừa bị đóng sau lần kiểm ở trên.
        return await timelineStore.AddToOpenTicketAsync(comment, cancellationToken);
    }
}
