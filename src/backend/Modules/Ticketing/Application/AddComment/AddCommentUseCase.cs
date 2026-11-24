using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
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
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        // Kiểm quyền xem trước trạng thái: ticket của người khác luôn trả 404, không lộ là đã đóng hay hủy.
        var isStaff = TicketPermissions.IsStaff(currentUser);
        var isOwner = ticket.IsOwnedBy(actorId);
        if (!isStaff && !isOwner)
            throw new KeyNotFoundException("Ticket not found.");

        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw new ArgumentException("Không thể thêm bình luận vào vé đã kết thúc.");

        var authorName = await agentDirectory.GetDisplayNameAsync(actorId, cancellationToken);
        var authorRole = currentUser.IsInRole(RoleNames.Admin) ? RoleNames.Admin
            : currentUser.IsInRole(RoleNames.Agent) ? RoleNames.Agent : RoleNames.Requester;

        var now = clock.UtcNow.UtcDateTime;

        // BIZ-02: Chống gửi bình luận trùng lặp cùng tác giả/vé trong 30 giây
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

        var comment = TicketComment.CreatePublic(
            command.TicketId,
            actorId,
            authorName,
            authorRole,
            command.Content,
            now);

        var ticketModified = false;
        TicketComment? resumeAudit = null;

        // DEC-03: Người dùng sở hữu phản hồi trong PendingUser tự động resume InProgress
        if (isOwner && ticket.Status == TicketStatus.PendingUser)
        {
            ticket.ResumeFromPending(now);
            resumeAudit = TicketComment.CreatePublic(
                ticket.Id,
                actorId,
                SystemAuthor.Name,
                SystemAuthor.Role,
                "Tự động tiếp tục xử lý phiếu sau phản hồi của người dùng.",
                now);
            ticketModified = true;
        }

        // First Response: Phản hồi công khai đầu tiên từ Staff (không phải chủ sở hữu)
        if (isStaff && !isOwner && ticket.IsSlaApplicable && !ticket.FirstRespondedAt.HasValue)
        {
            ticket.RecordFirstResponse(now);
            ticketModified = true;
        }

        // Kiểm lại trạng thái trong giao dịch ghi: ticket có thể vừa bị đóng sau lần kiểm ở trên.
        return ticketModified
            ? await timelineStore.AddToOpenTicketAsync(comment, ticket, resumeAudit, cancellationToken)
            : await timelineStore.AddToOpenTicketAsync(comment, cancellationToken);
    }
}
