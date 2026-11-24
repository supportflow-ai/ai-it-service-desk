namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

public record TransferTicketRequest(Guid TargetTeamId, string Reason, uint? ExpectedRevision = null);
