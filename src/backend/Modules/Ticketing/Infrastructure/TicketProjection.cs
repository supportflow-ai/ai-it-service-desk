using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

/// <summary>
/// Ánh xạ dữ liệu giữa thực thể Domain Ticket và đối tượng truyền tải TicketDto.
/// </summary>
internal static class TicketProjection
{
    internal static TicketDto ToDto(Ticket ticket, uint revision = 0, DateTime? evaluatedAt = null)
    {
        SlaSummaryDto? sla = null;
        if (evaluatedAt.HasValue)
        {
            var status = SlaEvaluator.Evaluate(ticket, evaluatedAt.Value);
            sla = MapSla(status);
        }
        else if (ticket.IsSlaApplicable)
        {
            var status = SlaEvaluator.Evaluate(ticket, ticket.UpdatedAt);
            sla = MapSla(status);
        }
        else
        {
            sla = new SlaSummaryDto(false, ticket.UpdatedAt, "NotApplicable", null, null);
        }

        return new TicketDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Title,
            ticket.Description,
            ticket.CategoryId,
            (int)ticket.Status,
            ticket.CreatedAt,
            ticket.RequesterDepartmentId,
            ticket.RequesterId,
            ticket.AssignedToId,
            ticket.AssignedAt,
            ticket.UpdatedAt,
            revision,
            ticket.Priority?.ToString(),
            ticket.Impact?.ToString(),
            ticket.Urgency?.ToString(),
            sla);
    }

    private static SlaSummaryDto MapSla(TicketSlaStatus status)
    {
        return new SlaSummaryDto(
            status.IsApplicable,
            status.EvaluatedAt,
            status.OverallStatus,
            status.FirstResponse is null ? null : new SlaClockSummaryDto(
                status.FirstResponse.TargetMinutes,
                status.FirstResponse.ElapsedMinutes,
                status.FirstResponse.RemainingMinutes,
                status.FirstResponse.Deadline,
                status.FirstResponse.IsBreached,
                status.FirstResponse.State),
            status.Resolution is null ? null : new SlaClockSummaryDto(
                status.Resolution.TargetMinutes,
                status.Resolution.ElapsedMinutes,
                status.Resolution.RemainingMinutes,
                status.Resolution.Deadline,
                status.Resolution.IsBreached,
                status.Resolution.State));
    }
}
