using ServiceDesk.Application.Common.Interfaces;
namespace ServiceDesk.Application.Common;
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public string? UserId => null;
    public bool IsAuthenticated => false;
    public IReadOnlyList<string> Roles => [];
    public bool IsInRole(string role) => false;
}
