namespace ServiceDesk.Application.Identity.Contracts;

/// <summary>
/// Request payload for user registration.
/// </summary>
public sealed record RegisterRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? FullName { get; init; }
    public string? Role { get; init; }
}
