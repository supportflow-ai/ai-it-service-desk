namespace ServiceDesk.Application.Identity.Contracts;

public sealed record AssignRolesRequest(IReadOnlyList<string> Roles);
