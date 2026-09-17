namespace ServiceDesk.Application.Identity.Models;

/// <summary>
/// Authenticated user information and assigned roles.
/// </summary>
public sealed record UserInfo
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public string? FullName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}
