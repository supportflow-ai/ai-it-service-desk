using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Application.Common;

internal static class AuthenticatedActor
{
    public static Guid RequireId(ICurrentUser user)
    {
        if (!user.IsAuthenticated || !Guid.TryParse(user.UserId, out var id) || id == Guid.Empty)
            throw new UnauthorizedAccessException("Authenticated actor identity is required.");
        return id;
    }
}
