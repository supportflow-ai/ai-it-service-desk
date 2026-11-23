using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

public record TriageTicketRequest(Impact Impact, Urgency Urgency, uint? ExpectedRevision = null);
