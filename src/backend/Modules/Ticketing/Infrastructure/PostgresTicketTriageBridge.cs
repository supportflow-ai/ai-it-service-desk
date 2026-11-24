using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class PostgresTicketTriageBridge(
    DbContext context,
    ITicketWriter writer,
    IClock clock) : ITicketTriageBridge
{
    public async Task<TicketTriageSnapshotDto?> GetSnapshotAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var raw = await context.Set<Ticket>()
            .AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new
            {
                Ticket = t,
                Xmin = EF.Property<uint>(t, "xmin")
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is null) return null;

        var t = raw.Ticket;
        return new TicketTriageSnapshotDto(
            t.Id,
            raw.Xmin,
            t.Title,
            t.Description,
            t.CategoryId,
            t.Impact,
            t.Urgency,
            t.Priority,
            t.Status,
            t.RequesterId
        );
    }

    public async Task<bool> ApplyTriageDecisionAsync(
        Guid ticketId,
        Impact impact,
        Urgency urgency,
        string? categoryId,
        string auditReason,
        Guid reviewerId,
        uint? expectedRevision = null,
        CancellationToken cancellationToken = default)
    {
        var scope = new TicketReadScope.AllTickets();
        var ticket = await writer.GetForUpdateAsync(ticketId, scope, cancellationToken);
        if (ticket is null) return false;

        TicketCommandGuard.RequireCurrentRevision(writer, ticket, expectedRevision);

        var now = clock.UtcNow.UtcDateTime;
        TicketCommandGuard.ApplyRule(ticket, t => t.SetTriage(impact, urgency, now), includeArgumentErrors: true);

        await TicketCommandGuard.SaveWithSystemAuditAsync(
            writer,
            ticket,
            reviewerId,
            $"[AI HITL Triage] {auditReason}. Mức ưu tiên: {ticket.Priority}.",
            now,
            cancellationToken);

        return true;
    }
}
