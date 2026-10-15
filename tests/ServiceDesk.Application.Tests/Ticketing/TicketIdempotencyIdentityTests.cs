using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Infrastructure.Persistence;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class TicketIdempotencyIdentityTests
{
    private static readonly CreateTicketCommand Payload = new("Network issue", "Cannot connect to the network", "NET");

    [Theory]
    [InlineData("unauthenticated")]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("different-requester")]
    public async Task CallerCannotSupplyAnotherRequesterScope(string identity)
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        var requester = Guid.NewGuid();
        var current = new Mock<ICurrentUser>();
        current.SetupGet(user => user.IsAuthenticated).Returns(identity != "unauthenticated");
        current.SetupGet(user => user.UserId).Returns(identity switch
        {
            "missing" => null,
            "invalid" => "not-a-guid",
            "different-requester" => Guid.NewGuid().ToString(),
            _ => requester.ToString()
        });
        var service = new TicketService(context, current.Object);
        var action = () => service.CreateTicketIdempotentlyAsync(Payload, requester, "key");
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        context.ChangeTracker.Entries().Should().BeEmpty();
        context.Database.CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticatedRequesterOnUnsupportedProvider_FailsClosedWithoutCacheFallback()
    {
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("DataSource=:memory:").Options);
        var requester = Guid.NewGuid();
        var current = new Mock<ICurrentUser>();
        current.SetupGet(user => user.IsAuthenticated).Returns(true);
        current.SetupGet(user => user.UserId).Returns(requester.ToString());
        var service = new TicketService(context, current.Object);
        var action = () => service.CreateTicketIdempotentlyAsync(Payload, requester, "key");
        await action.Should().ThrowAsync<NotSupportedException>();
        context.ChangeTracker.Entries().Should().BeEmpty();
    }
}
