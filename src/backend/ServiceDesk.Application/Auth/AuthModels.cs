namespace ServiceDesk.Application.Auth;

public static class ApplicationRoles
{
    public const string Requester = "Requester";
    public const string Agent = "Agent";
    public const string Admin = "Admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Requester,
        Agent,
        Admin
    };

    public static bool IsValid(string role) => All.Contains(role);
}

public sealed record IdentityUserRecord(string Id, string Email, IReadOnlyList<string> Roles);

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResult(string Token, AuthUser User);

public sealed record AuthUser(string Id, string Email, IReadOnlyList<string> Roles);

public sealed record ChangeRoleRequest(string Role);
