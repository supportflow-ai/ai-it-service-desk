using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Infrastructure.Identity;

/// <summary>
/// Reads the current user from the HTTP context claims.
/// Registered as scoped — one instance per request.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
