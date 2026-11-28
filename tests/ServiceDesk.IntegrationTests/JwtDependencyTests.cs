using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using ServiceDesk.Modules.Identity;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using ServiceDesk.Platform.Clock;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class JwtDependencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingPersistenceOrDatabaseFailureReturnsGenericServerError(bool databaseFailure)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Services.Configure<JwtOptions>(o =>
        {
            o.SecretKey = TicketIdempotencyPostgresFixture.Secret;
            o.Issuer = "ServiceDesk";
            o.Audience = "ServiceDeskClient";
        });
        builder.Services.AddIdentityAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddProblemDetails();
        if (databaseFailure)
        {
            var database = new Mock<DbContext>();
            database.Setup(db => db.Set<IdentityUser>()).Throws(new NpgsqlException("private-database-detail"));
            builder.Services.AddScoped<DbContext>(_ => database.Object);
        }
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/protected", () => "authenticated").RequireAuthorization();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var token = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            SecretKey = TicketIdempotencyPostgresFixture.Secret, Issuer = "ServiceDesk", Audience = "ServiceDeskClient"
        }), new SystemClock()).GenerateToken(new UserInfo { UserId = Guid.NewGuid().ToString(), Email = "dependency@test.invalid", SecurityStamp = "stamp" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-database-detail", body);
        Assert.DoesNotContain("DbContext", body);
        Assert.DoesNotContain("authenticated", body);
    }
}
