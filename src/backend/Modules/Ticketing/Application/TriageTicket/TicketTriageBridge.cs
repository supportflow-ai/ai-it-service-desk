using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.TriageTicket;

/// <summary>
/// Cài đặt <see cref="ITicketTriageBridge"/> ở tầng Application để chính sách quyền phiếu chỉ nằm trong Ticketing:
/// đọc theo phạm vi của người dùng hiện tại, ghi theo cùng quy tắc với triage thủ công.
/// </summary>
internal sealed class TicketTriageBridge(
    ITicketQueries queries,
    ITicketWriter writer,
    ICurrentUser currentUser,
    IClock clock) : ITicketTriageBridge
{
    public async Task<TicketTriageSnapshotDto?> GetSnapshotAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        TicketDto? ticket = null;
        var canTriage = false;
        if (TicketPermissions.IsStaff(currentUser))
        {
            ticket = await queries.GetByIdAsync(ticketId, TicketPermissions.ReadScopeFor(currentUser, actorId), cancellationToken);
            canTriage = ticket is not null;
        }

        // Chủ phiếu (kể cả có vai trò Agent) chỉ có quyền công khai trên phiếu của mình.
        ticket ??= await queries.GetByIdAsync(ticketId, new TicketReadScope.OwnTickets(actorId), cancellationToken);
        return ticket is null
            ? null
            : new TicketTriageSnapshotDto(ticket.Id, ticket.Revision, ticket.Title, ticket.Description, ticket.CategoryId, ticket.RequesterId, canTriage);
    }

    public async Task<TicketTriageSnapshotDto?> GetDecisionSnapshotAsync(Guid ticketId, uint? expectedRevision, CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        if (!TicketPermissions.IsStaff(currentUser)) throw new UnauthorizedAccessException("Yêu cầu quyền xử lý phiếu.");
        var ticket = await writer.GetForUpdateAsync(ticketId, TicketPermissions.ReadScopeFor(currentUser, actorId), cancellationToken);
        if (ticket is null) return null;
        if (expectedRevision.HasValue) TicketCommandGuard.RequireCurrentRevision(writer, ticket, expectedRevision);
        TicketCommandGuard.ApplyRule(ticket, t => t.EnsureCanTriage());
        return new(ticket.Id, writer.GetRevision(ticket), ticket.Title, ticket.Description, ticket.CategoryId, ticket.RequesterId, true);
    }

    public async Task<TicketTriageChange> ApplyTriageDecisionAsync(
        Guid ticketId,
        Impact impact,
        Urgency urgency,
        string? categoryId,
        string auditReason,
        uint? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var actorId = TicketActor.RequireId(currentUser);
        if (!TicketPermissions.IsStaff(currentUser))
            throw new UnauthorizedAccessException("Chỉ nhân viên hỗ trợ (Agent hoặc Admin) mới có quyền phân loại vé.");

        var ticket = await writer.GetForUpdateAsync(ticketId, TicketPermissions.ReadScopeFor(currentUser, actorId), cancellationToken)
            ?? throw new KeyNotFoundException("Ticket not found.");
        TicketCommandGuard.RequireCurrentRevision(writer, ticket, expectedRevision);

        var previous = ValuesOf(ticket);
        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.SetTriage(impact, urgency, now, categoryId), includeArgumentErrors: true);

        var categoryNote = previous.CategoryId == ticket.CategoryId ? "" : $" Danh mục: {previous.CategoryId} → {ticket.CategoryId}.";
        await TicketCommandGuard.SaveWithSystemAuditAsync(
            writer,
            ticket,
            actorId,
            $"[AI HITL Triage] {auditReason}. Ảnh hưởng {impact}, Khẩn cấp {urgency}, Mức ưu tiên {ticket.Priority}.{categoryNote}",
            now,
            cancellationToken, isInternal: true);

        return new TicketTriageChange(previous, ValuesOf(ticket));
    }

    private static TicketTriageValues ValuesOf(Ticket ticket) =>
        new(ticket.CategoryId, ticket.Impact?.ToString(), ticket.Urgency?.ToString(), ticket.Priority?.ToString());
}
