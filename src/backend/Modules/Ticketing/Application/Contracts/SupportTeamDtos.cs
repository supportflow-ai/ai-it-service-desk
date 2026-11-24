namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

public record SupportTeamMemberDto(Guid UserId, string? DisplayName, bool IsLead);

public record SupportTeamDto(
    Guid Id,
    string Code,
    string Name,
    bool IsTriageQueue,
    bool IsActive,
    IReadOnlyList<SupportTeamMemberDto> Members);

public record AddTeamMemberRequest(Guid UserId, bool IsLead = false);

public record CreateSupportTeamRequest(string Code, string Name);

public record UpdateSupportTeamRequest(string Name, bool IsActive);
