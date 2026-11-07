namespace ServiceDesk.Modules.Identity.Application.Contracts;

public sealed record AssignRolesRequest(IReadOnlyList<string> Roles);
