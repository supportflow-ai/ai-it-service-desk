namespace ServiceDesk.Modules.Identity.Application.Contracts;

public sealed record DepartmentDto(
    Guid Id,
    string Code,
    string Name,
    string? Description);
