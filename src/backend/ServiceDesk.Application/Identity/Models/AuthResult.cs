namespace ServiceDesk.Application.Identity.Models;

/// <summary>
/// Result of authentication operations (register/login).
/// </summary>
public sealed record AuthResult
{
    public bool Succeeded { get; init; }
    public string? Token { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public UserInfo? User { get; init; }

    public static AuthResult Success(string token, UserInfo user) => new()
    {
        Succeeded = true,
        Token = token,
        User = user
    };

    public static AuthResult Failed(params string[] errors) => new()
    {
        Succeeded = false,
        Errors = errors
    };

    public static AuthResult Failed(IEnumerable<string> errors) => new()
    {
        Succeeded = false,
        Errors = errors.ToList()
    };
}
