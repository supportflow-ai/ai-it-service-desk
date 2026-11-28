using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Departments;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class IdentityApiBoundaryTests(ServiceDeskWebApplicationFactory factory) : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private const string Password = "Test@1234";

    private async Task<(HttpClient Client, UserInfo User)> ActorAsync(string role = RoleNames.Requester)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountGateway>();
        var user = (await accounts.CreateRequesterAsync($"g306-{Guid.NewGuid():N}@test.invalid", Password, "Original", default)).User!;
        if (role != RoleNames.Requester)
        {
            Assert.True((await accounts.AssignRolesAsync(user.UserId, [role], DateTime.UtcNow, default)).Succeeded);
            user = (await accounts.FindByIdAsync(user.UserId, default))!;
        }
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>().GenerateToken(user));
        return (client, user);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(RoleNames.Requester)]
    [InlineData(RoleNames.Agent)]
    [InlineData(RoleNames.Admin)]
    public async Task IdentityEndpointRoleMatrix(string? role)
    {
        var actor = await ActorAsync(role ?? RoleNames.Requester);
        using var client = actor.Client;
        if (role is null) client.DefaultRequestHeaders.Authorization = null;
        var target = await ActorAsync();
        using var targetClient = target.Client;
        Guid departmentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dept = Department.Create($"D-{Guid.NewGuid():N}", "Department", null, DateTime.UtcNow);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<Department>().Add(dept);
            await db.SaveChangesAsync();
            departmentId = dept.Id;
        }
        var routes = new (HttpMethod Method, string Path, object? Body, string Access, HttpStatusCode Success)[]
        {
            (HttpMethod.Get, "/api/auth/me", null, "user", HttpStatusCode.OK),
            (HttpMethod.Put, "/api/auth/me", new { fullName = "Updated", phoneNumber = "0123456789" }, "user", HttpStatusCode.OK),
            (HttpMethod.Get, "/api/departments", null, "user", HttpStatusCode.OK),
            (HttpMethod.Get, "/api/users?role=Agent", null, "staff", HttpStatusCode.OK),
            (HttpMethod.Put, $"/api/users/{target.User.UserId}/roles", new { roles = new[] { RoleNames.Agent } }, "admin", HttpStatusCode.OK),
            (HttpMethod.Post, "/api/departments", new { code = $"R-{Guid.NewGuid():N}", name = "Created" }, "admin", HttpStatusCode.Created),
            (HttpMethod.Put, $"/api/departments/{departmentId}", new { name = "Updated" }, "admin", HttpStatusCode.OK),
            (HttpMethod.Post, "/api/auth/change-password", new { currentPassword = Password, newPassword = "Next@1234" }, "user", HttpStatusCode.OK)
        };
        foreach (var route in routes)
        {
            using var request = new HttpRequestMessage(route.Method, route.Path);
            if (route.Body is not null) request.Content = JsonContent.Create(route.Body);
            using var response = await client.SendAsync(request);
            var expected = role is null ? HttpStatusCode.Unauthorized
                : route.Access == "admin" && role != RoleNames.Admin || route.Access == "staff" && role == RoleNames.Requester
                    ? HttpStatusCode.Forbidden : route.Success;
            Assert.Equal(expected, response.StatusCode);
            if (response.IsSuccessStatusCode)
                AssertNoPrivateFields(await response.Content.ReadFromJsonAsync<JsonElement>());
        }
    }

    [Fact]
    public async Task RegistrationAndProfileIgnorePrivilegedFieldsAndReturnPersistedName()
    {
        using var client = factory.CreateClient();
        var email = $"g306-register-{Guid.NewGuid():N}@test.invalid";
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password = Password, fullName = "  Example  ", role = "Admin", roles = new[] { "Admin" },
            securityStamp = "attacker", passwordHash = "attacker", departmentId = Guid.NewGuid(), emailConfirmed = false
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertNoPrivateFields(json);
        Assert.Equal("Example", json.GetProperty("user").GetProperty("fullName").GetString());
        Assert.Equal(RoleNames.Requester, Assert.Single(json.GetProperty("user").GetProperty("roles").EnumerateArray()).GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        using var update = await client.PutAsJsonAsync("/api/auth/me", new
        {
            fullName = "  New name  ", phoneNumber = " 0123456789 ", userId = Guid.NewGuid(), email = "changed@test.invalid",
            roles = new[] { "Admin" }, securityStamp = "attacker", passwordHash = "attacker", departmentId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var profile = await update.Content.ReadFromJsonAsync<JsonElement>();
        AssertNoPrivateFields(profile);
        Assert.Equal(email, profile.GetProperty("email").GetString());
        Assert.Equal("New name", profile.GetProperty("fullName").GetString());
        Assert.Equal("0123456789", profile.GetProperty("phoneNumber").GetString());
        Assert.Equal(RoleNames.Requester, Assert.Single(profile.GetProperty("roles").EnumerateArray()).GetString());
        using var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task ClaimWriteFailureRollsBackPhoneAndNameTogether()
    {
        var actor = await ActorAsync();
        using var client = actor.Client;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/auth/me", new { phoneNumber = "111" })).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION g306_fail_profile() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW."ClaimValue" = 'g3-profile-failure' THEN RAISE EXCEPTION 'synthetic profile write failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER g306_profile_failure BEFORE UPDATE ON "AspNetUserClaims" FOR EACH ROW EXECUTE FUNCTION g306_fail_profile();
            """);
        try
        {
            using var failed = await client.PutAsJsonAsync("/api/auth/me", new { fullName = "g3-profile-failure", phoneNumber = "222" });
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal("111", (await db.Users.AsNoTracking().SingleAsync(u => u.Id == actor.User.UserId)).PhoneNumber);
            Assert.Equal("Original", (await db.UserClaims.AsNoTracking().SingleAsync(c => c.UserId == actor.User.UserId && c.ClaimType == ClaimTypes.Name)).ClaimValue);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER g306_profile_failure ON \"AspNetUserClaims\"; DROP FUNCTION g306_fail_profile();"); }
    }

    [Fact]
    public async Task FailedIdentityResultIsNotReportedAsSuccessfulProfileUpdate()
    {
        var actor = await ActorAsync();
        using var client = actor.Client;
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().UserValidators.Add(new RejectProfileValidator());
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IUserAccountGateway>()
            .UpdateProfileAsync(actor.User.UserId, "Rejected", null, default));
        await using var fresh = factory.Services.CreateAsyncScope();
        var persisted = await fresh.ServiceProvider.GetRequiredService<IUserAccountGateway>().FindByIdAsync(actor.User.UserId, default);
        Assert.Equal("Original", persisted!.FullName);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("null-code")]
    public async Task InvalidDepartmentIsRejectedBeforeDatabaseWrite(string field)
    {
        var actor = await ActorAsync(RoleNames.Admin);
        using var client = actor.Client;
        using var response = await client.PostAsJsonAsync("/api/departments", new
        {
            code = field == "null-code" ? null : field == "code" ? new string('c', 51) : $"V-{Guid.NewGuid():N}",
            name = field == "name" ? new string('n', 101) : "Valid",
            description = field == "description" ? new string('d', 501) : null
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentDepartmentWritersReturnConflictForDuplicateCanonicalCode()
    {
        var code = $"D-{Guid.NewGuid():N}";
        async Task<bool> WriteAsync(string input)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IDepartmentStore>().AddAsync(Department.Create(input, "Department", null, DateTime.UtcNow));
                return true;
            }
            catch (DepartmentConflictException) { return false; }
        }
        var results = await Task.WhenAll(WriteAsync(code), WriteAsync($" {code.ToLowerInvariant()} "));
        Assert.Single(results, success => success);
        Assert.Single(results, success => !success);
    }

    private sealed class RejectProfileValidator : IUserValidator<IdentityUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user) =>
            Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "SyntheticProfileFailure", Description = "Synthetic profile failure" }));
    }

    private static void AssertNoPrivateFields(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Array)
            foreach (var item in json.EnumerateArray()) AssertNoPrivateFields(item);
        if (json.ValueKind != JsonValueKind.Object) return;
        foreach (var property in json.EnumerateObject())
        {
            Assert.DoesNotContain(property.Name.ToLowerInvariant(), new[] { "passwordhash", "securitystamp", "otphash", "refreshtokenhash" });
            AssertNoPrivateFields(property.Value);
        }
    }
}
