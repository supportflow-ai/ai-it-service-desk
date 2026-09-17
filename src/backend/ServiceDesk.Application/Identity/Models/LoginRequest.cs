namespace ServiceDesk.Application.Identity.Models;

/// <summary>
/// Request payload for user login.
/// </summary>
public sealed record LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
