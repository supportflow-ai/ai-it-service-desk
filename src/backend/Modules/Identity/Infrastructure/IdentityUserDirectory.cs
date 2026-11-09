using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>
/// Danh bạ người dùng đọc từ bảng ASP.NET Core Identity. Chỉ dùng <see cref="DbContext"/> (không cần UserManager)
/// để host nền như Worker cũng đăng ký được.
/// </summary>
internal sealed class IdentityUserDirectory(DbContext context) : IUserDirectory
{
    public async Task<bool> IsInAnyRoleAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roleNames);
        if (roleNames.Count == 0) return false;
        var id = userId.ToString();
        var names = roleNames.ToArray();
        return await (from userRole in context.Set<IdentityUserRole<string>>().AsNoTracking()
                      join role in context.Set<IdentityRole>().AsNoTracking() on userRole.RoleId equals role.Id
                      where userRole.UserId == id && role.Name != null && names.Contains(role.Name)
                      select userRole).AnyAsync(cancellationToken);
    }

    public async Task<string?> FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var id = userId.ToString();
        if (!await context.Set<IdentityUser>().AsNoTracking().AnyAsync(u => u.Id == id, cancellationToken))
            return null;
        return await context.Set<IdentityUserClaim<string>>().AsNoTracking()
            .Where(c => c.UserId == id && c.ClaimType == ClaimTypes.Name)
            .Select(c => c.ClaimValue)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
