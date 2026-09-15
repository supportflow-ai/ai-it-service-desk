namespace ServiceDesk.Domain.Identity;

/// <summary>
/// Core system role names.
/// Pure C# constants with zero framework dependencies.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Agent = "Agent";
    public const string Requester = "Requester";

    public static readonly IReadOnlyList<string> All = [Admin, Agent, Requester];
}
