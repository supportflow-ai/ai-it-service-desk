namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>
/// Trạng thái SLA của một đồng hồ đo (First Response hoặc Resolution).
/// </summary>
public sealed record SlaClockStatus(
    int? TargetMinutes,
    double? ElapsedMinutes,
    double? RemainingMinutes,
    DateTime? Deadline,
    bool IsBreached,
    string State
);

/// <summary>
/// Snapshot trạng thái SLA tổng thể của ticket tại một mốc thời gian xác định.
/// </summary>
public sealed record TicketSlaStatus(
    bool IsApplicable,
    DateTime EvaluatedAt,
    string OverallStatus,
    SlaClockStatus? FirstResponse,
    SlaClockStatus? Resolution
);

/// <summary>
/// Domain service thuần túy đánh giá trạng thái SLA tức thời của một ticket tại mốc thời gian bất kỳ.
/// Tuyệt đối không thay đổi trạng thái entity, không persist marker, không thay đổi xmin.
/// </summary>
public static class SlaEvaluator
{
    public static TicketSlaStatus Evaluate(Ticket ticket, DateTime evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (!ticket.IsSlaApplicable)
        {
            return new TicketSlaStatus(
                IsApplicable: false,
                EvaluatedAt: evaluatedAt,
                OverallStatus: "NotApplicable",
                FirstResponse: null,
                Resolution: null
            );
        }

        var frStatus = EvaluateFirstResponse(ticket, evaluatedAt);
        var resStatus = EvaluateResolution(ticket, evaluatedAt);

        var overallStatus = DetermineOverallStatus(frStatus, resStatus);

        return new TicketSlaStatus(
            IsApplicable: true,
            EvaluatedAt: evaluatedAt,
            OverallStatus: overallStatus,
            FirstResponse: frStatus,
            Resolution: resStatus
        );
    }

    private static SlaClockStatus EvaluateFirstResponse(Ticket ticket, DateTime evaluatedAt)
    {
        var targetMinutes = ticket.FirstResponseTargetMinutes;
        if (!targetMinutes.HasValue)
        {
            return new SlaClockStatus(null, null, null, null, false, "NotApplicable");
        }

        var target = targetMinutes.Value;
        double elapsed;
        if (ticket.FirstRespondedAt.HasValue)
        {
            elapsed = (ticket.FirstRespondedAt.Value - ticket.CreatedAt).TotalMinutes;
        }
        else if (ticket.Status == TicketStatus.Cancelled)
        {
            var stopAt = ticket.UpdatedAt;
            elapsed = Math.Max(0, (stopAt - ticket.CreatedAt).TotalMinutes);
        }
        else
        {
            elapsed = Math.Max(0, (evaluatedAt - ticket.CreatedAt).TotalMinutes);
        }

        var remaining = target - elapsed;
        var deadline = ticket.CreatedAt.AddMinutes(target);

        bool isBreached;
        if (ticket.FirstResponseBreached)
        {
            isBreached = true;
        }
        else if (ticket.FirstRespondedAt.HasValue)
        {
            isBreached = elapsed > target;
        }
        else if (ticket.Status == TicketStatus.Cancelled)
        {
            isBreached = elapsed > target;
        }
        else if (ticket.Status == TicketStatus.Closed)
        {
            isBreached = false;
        }
        else
        {
            isBreached = elapsed > target;
        }

        string state;
        if (ticket.FirstRespondedAt.HasValue)
        {
            state = isBreached ? "CompletedBreached" : "CompletedAchieved";
        }
        else if (ticket.Status == TicketStatus.Cancelled)
        {
            state = isBreached ? "CancelledBreached" : "Cancelled";
        }
        else if (ticket.Status == TicketStatus.Closed)
        {
            state = isBreached ? "ClosedBreached" : "Closed";
        }
        else
        {
            if (isBreached)
            {
                state = "RunningBreached";
            }
            else if (elapsed >= 0.75 * target)
            {
                state = "RunningAtRisk";
            }
            else
            {
                state = "RunningOnTrack";
            }
        }

        return new SlaClockStatus(
            TargetMinutes: target,
            ElapsedMinutes: elapsed,
            RemainingMinutes: remaining,
            Deadline: deadline,
            IsBreached: isBreached,
            State: state
        );
    }

    private static SlaClockStatus EvaluateResolution(Ticket ticket, DateTime evaluatedAt)
    {
        var targetMinutes = ticket.ResolutionTargetMinutes;
        if (!targetMinutes.HasValue)
        {
            return new SlaClockStatus(null, null, null, null, false, "NotApplicable");
        }

        var target = targetMinutes.Value;
        double elapsed;
        DateTime? deadline;

        if (ticket.ResolutionSegmentStartedAt.HasValue)
        {
            var segmentElapsed = Math.Max(0, (evaluatedAt - ticket.ResolutionSegmentStartedAt.Value).TotalMinutes);
            elapsed = ticket.ResolutionAccumulatedDuration.TotalMinutes + segmentElapsed;
            deadline = ticket.ResolutionSegmentStartedAt.Value.Add(TimeSpan.FromMinutes(target) - ticket.ResolutionAccumulatedDuration);
        }
        else
        {
            elapsed = ticket.ResolutionAccumulatedDuration.TotalMinutes;
            deadline = null; // Paused hoặc Completed/Cancelled: deadline là null
        }

        var remaining = target - elapsed;

        bool isBreached;
        if (ticket.ResolutionBreached)
        {
            isBreached = true;
        }
        else if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
        {
            isBreached = elapsed > target;
        }
        else if (ticket.Status == TicketStatus.Cancelled)
        {
            isBreached = elapsed > target;
        }
        else
        {
            isBreached = elapsed > target;
        }

        string state;
        if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
        {
            state = isBreached ? "ResolvedBreached" : "ResolvedAchieved";
        }
        else if (ticket.Status == TicketStatus.Cancelled)
        {
            state = isBreached ? "CancelledBreached" : "Cancelled";
        }
        else if (ticket.Status == TicketStatus.PendingUser || ticket.Status == TicketStatus.PendingExternal)
        {
            if (isBreached)
            {
                state = "PausedBreached";
            }
            else if (elapsed >= 0.75 * target)
            {
                state = "PausedAtRisk";
            }
            else
            {
                state = "PausedOnTrack";
            }
        }
        else
        {
            if (isBreached)
            {
                state = "RunningBreached";
            }
            else if (elapsed >= 0.75 * target)
            {
                state = "RunningAtRisk";
            }
            else
            {
                state = "RunningOnTrack";
            }
        }

        return new SlaClockStatus(
            TargetMinutes: target,
            ElapsedMinutes: elapsed,
            RemainingMinutes: remaining,
            Deadline: deadline,
            IsBreached: isBreached,
            State: state
        );
    }

    private static string DetermineOverallStatus(SlaClockStatus frStatus, SlaClockStatus resStatus)
    {
        if (frStatus.IsBreached || resStatus.IsBreached)
        {
            return "Breached";
        }

        if (frStatus.State.EndsWith("AtRisk", StringComparison.Ordinal) || resStatus.State.EndsWith("AtRisk", StringComparison.Ordinal))
        {
            return "AtRisk";
        }

        if (frStatus.State.EndsWith("OnTrack", StringComparison.Ordinal) || resStatus.State.EndsWith("OnTrack", StringComparison.Ordinal))
        {
            return "OnTrack";
        }

        return "Completed";
    }
}
