using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Infrastructure.Persistence;

/// <summary>Explicit operations invoked by the database tool and isolated test setup, never application startup.</summary>
public sealed class DatabaseMaintenance(ApplicationDbContext context, RoleManager<IdentityRole> roles, UserManager<IdentityUser> users)
{
    public static readonly string[] DemoEmails = ["admin@test.com", "agent@test.com", "requester@test.com"];

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await WithLockAsync(() => context.Database.MigrateAsync(ct), ct);
    }

    public Task SeedRolesAsync(CancellationToken ct = default) => WithLockAsync(async () =>
    {
        foreach (var role in RoleNames.All)
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole(role)));
    }, ct);

    public Task SeedDemoAsync(string environment, bool enabled, string password, CancellationToken ct = default)
    {
        if (!enabled || environment is not ("Development" or "Test"))
            throw new DatabaseMaintenanceException("demo_seed_forbidden");
        return WithLockAsync(async () =>
        {
            var demoRoles = new[] { RoleNames.Admin, RoleNames.Agent, RoleNames.Requester };
            for (var i = 0; i < DemoEmails.Length; i++)
            {
                // Never modify or elevate an existing account, even in development.
                if (await users.FindByEmailAsync(DemoEmails[i]) is not null) continue;
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                var user = new IdentityUser { UserName = DemoEmails[i], Email = DemoEmails[i], EmailConfirmed = true };
                Ensure(await users.CreateAsync(user, password));
                Ensure(await users.AddToRoleAsync(user, demoRoles[i]));
                await transaction.CommitAsync(ct);
            }
        }, ct);
    }

    public Task BootstrapAdminAsync(string email, string password, CancellationToken ct = default) => WithLockAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            throw new DatabaseMaintenanceException("bootstrap_configuration_required");
        if (await users.FindByEmailAsync(email) is not null)
            throw new DatabaseMaintenanceException("bootstrap_existing_account");
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        Ensure(await users.CreateAsync(user, password));
        Ensure(await users.AddToRoleAsync(user, RoleNames.Admin));
        await transaction.CommitAsync(ct);
    }, ct);

    public async Task CheckProductionAsync(CancellationToken ct = default)
    {
        foreach (var email in DemoEmails)
        {
            ct.ThrowIfCancellationRequested();
            var user = await users.FindByEmailAsync(email);
            if (user is not null && await users.CheckPasswordAsync(user, "Test@1234"))
                throw new DatabaseMaintenanceException("demo_default_password_present");
        }
    }

    private async Task WithLockAsync(Func<Task> operation, CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        var locked = false;
        try
        {
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(741812345)", ct);
            locked = true;
            await operation();
        }
        finally
        {
            try
            {
                if (locked) await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(741812345)", CancellationToken.None);
            }
            finally { await context.Database.CloseConnectionAsync(); }
        }
    }

    private static void Ensure(IdentityResult result)
    {
        // Identity error codes contain no submitted credential or raw user input.
        if (!result.Succeeded) throw new DatabaseMaintenanceException("identity_write_failed");
    }
}
