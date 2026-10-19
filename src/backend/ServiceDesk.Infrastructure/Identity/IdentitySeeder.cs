using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Auth;

namespace ServiceDesk.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = [ApplicationRoles.Requester, ApplicationRoles.Agent, ApplicationRoles.Admin];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to seed role '{role}': {errors}");
                }
            }
        }

        var adminEmail = configuration["ADMIN_EMAIL"] ?? configuration["Admin:Email"];
        var adminPassword = configuration["ADMIN_PASSWORD"] ?? configuration["Admin:Password"];

        var hasEmail = !string.IsNullOrWhiteSpace(adminEmail);
        var hasPassword = !string.IsNullOrWhiteSpace(adminPassword);

        if (hasEmail ^ hasPassword)
        {
            throw new InvalidOperationException(
                "Invalid startup configuration: Both ADMIN_EMAIL and ADMIN_PASSWORD must be configured together, or neither.");
        }

        if (hasEmail && hasPassword)
        {
            var adminUser = await userManager.FindByEmailAsync(adminEmail!);
            if (adminUser is null)
            {
                adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword!);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException(
                        $"Failed to create admin user '{adminEmail}' from environment configuration: {errors}");
                }
            }

            if (!await userManager.IsInRoleAsync(adminUser, ApplicationRoles.Admin))
            {
                var addRoleResult = await userManager.AddToRoleAsync(adminUser, ApplicationRoles.Admin);
                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to assign Admin role to '{adminEmail}': {errors}");
                }
            }
        }
    }
}
