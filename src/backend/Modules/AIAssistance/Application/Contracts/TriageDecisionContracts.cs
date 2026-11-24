using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.AIAssistance.Application.Contracts;

public enum TriageDecisionType
{
    Accept = 1,
    Override = 2,
    Reject = 3
}

public sealed record TriageDecisionCommand(
    Guid TicketId,
    TriageDecisionType Decision,
    Impact? Impact = null,
    Urgency? Urgency = null,
    string? CategoryId = null,
    string? Reason = null,
    uint? ExpectedRevision = null
);

public sealed record TriageDecisionResultDto(
    Guid TicketId,
    TriageDecisionType Decision,
    bool AppliedToTicket,
    string Message
);
