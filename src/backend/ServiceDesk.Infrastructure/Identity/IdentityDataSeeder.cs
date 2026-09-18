using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Identity;

/// <summary>
/// Idempotent startup seeder for Identity roles and default test users.
/// Runs once at application startup.
/// </summary>
public sealed class IdentityDataSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdentityDataSeeder> _logger;

    public IdentityDataSeeder(
        IServiceScopeFactory scopeFactory,
        ILogger<IdentityDataSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        await SeedRolesAsync(roleManager);
        await SeedUsersAsync(userManager);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)
                {
                    _logger.LogInformation("Seeded role: {Role}", roleName);
                }
                else
                {
                    _logger.LogError("Failed to seed role {Role}: {Errors}",
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private async Task SeedUsersAsync(UserManager<IdentityUser> userManager)
    {
        var seedUsers = new[]
        {
            (Email: "admin@test.com", Role: RoleNames.Admin),
            (Email: "agent@test.com", Role: RoleNames.Agent),
            (Email: "requester@test.com", Role: RoleNames.Requester)
        };

        const string defaultPassword = "Test@1234";

        foreach (var (email, role) in seedUsers)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user, defaultPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                    _logger.LogInformation("Seeded user: {Email} with role {Role}", email, role);
                }
                else
                {
                    _logger.LogError("Failed to seed user {Email}: {Errors}",
                        email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }
            else if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
                _logger.LogInformation("Added role {Role} to existing user {Email}", role, email);
            }
        }
    }
}
