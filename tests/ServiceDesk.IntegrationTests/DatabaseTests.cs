using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ServiceDesk.Modules.Ticketing;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Tests that the database context can connect and run migrations
/// against a real PostgreSQL instance via Testcontainers.
/// </summary>
[Trait("Category", "Integration")]
public class DatabaseTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;

    public DatabaseTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AttachmentGrant_ReplayReturnsSameAttachment_AndOnlyOneAttachmentIsStored()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (ticketId, actorId) = await EnsureTicketAsync(context);
        var key = $"tickets/{ticketId}/{Guid.NewGuid()}_evidence.png";
        var now = DateTime.UtcNow;
        context.AttachmentUploadGrants.Add(AttachmentUploadGrant.Create(ticketId, actorId, "evidence.png", 1024,
            "image/png", key, now.AddMinutes(1)));
        await context.SaveChangesAsync();

        var request = new ConfirmAttachmentUploadRequest("evidence.png", 1024, "image/png", key);
        var objectInfo = new ObjectStorageObjectInfo(1024, "image/png");
        var first = await ConfirmThroughUseCaseAsync(context, ticketId, actorId, request, objectInfo, now);
        var repeated = await ConfirmThroughUseCaseAsync(context, ticketId, actorId, request, objectInfo, now);
        Assert.Equal(first.Id, repeated.Id);
        Assert.StartsWith($"tickets/{ticketId}/attachments/", first.StorageKey);
        Assert.Equal(1, await context.TicketAttachments.CountAsync(a => a.Id == first.Id));
        Assert.Equal(0, await context.TicketAttachments.CountAsync(a => a.StorageKey == key));
    }

    [Theory]
    [InlineData("unissued")]
    [InlineData("expired")]
    [InlineData("consumed")]
    [InlineData("wrong-actor")]
    [InlineData("wrong-ticket")]
    [InlineData("wrong-name")]
    [InlineData("wrong-size")]
    [InlineData("wrong-mime")]
    [InlineData("wrong-object-size")]
    [InlineData("wrong-object-mime")]
    public async Task AttachmentGrant_InvalidConfirmation_IsRejectedWithoutConsumingGrant(string scenario)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (ticketId, actorId) = await EnsureTicketAsync(context);
        var key = $"tickets/{ticketId}/{Guid.NewGuid()}_evidence.png";
        var now = DateTime.UtcNow;
        var grant = AttachmentUploadGrant.Create(ticketId, actorId, "evidence.png", 1024, "image/png", key,
            scenario == "expired" ? now.AddMinutes(-1) : now.AddMinutes(1));
        if (scenario == "consumed") grant.Consume(now);
        if (scenario != "unissued")
        {
            context.AttachmentUploadGrants.Add(grant);
            await context.SaveChangesAsync();
        }

        var request = new ConfirmAttachmentUploadRequest(
            scenario == "wrong-name" ? "other.png" : "evidence.png",
            scenario == "wrong-size" ? 2048 : 1024,
            scenario == "wrong-mime" ? "image/jpeg" : "image/png",
            key);
        var objectInfo = new ObjectStorageObjectInfo(
            scenario == "wrong-object-size" ? 2048 : 1024,
            scenario == "wrong-object-mime" ? "image/jpeg" : "image/png");
        var confirmationActor = scenario == "wrong-actor" ? Guid.NewGuid() : actorId;
        var confirmationTicket = scenario == "wrong-ticket" ? Guid.NewGuid() : ticketId;
        Func<Task> confirm = () => ConfirmThroughUseCaseAsync(context, confirmationTicket, confirmationActor, request, objectInfo, now);
        if (scenario is "unissued" or "wrong-actor")
            await Assert.ThrowsAsync<KeyNotFoundException>(confirm);
        else
            await Assert.ThrowsAsync<ArgumentException>(confirm);

        Assert.Equal(0, await context.TicketAttachments.CountAsync(a => a.StorageKey == key));
        if (scenario != "unissued")
        {
            var persistedGrant = await context.AttachmentUploadGrants.AsNoTracking().SingleAsync(g => g.Id == grant.Id);
            if (scenario == "consumed") Assert.NotNull(persistedGrant.ConsumedAt);
            else Assert.Null(persistedGrant.ConsumedAt);
        }
    }

    [Fact]
    public async Task AttachmentGrant_ConcurrentConfirmation_StoresOnlyOneAttachment()
    {
        Guid ticketId;
        Guid actorId;
        string key;
        var now = DateTime.UtcNow;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ticketInfo = await EnsureTicketAsync(seedContext);
            ticketId = ticketInfo.TicketId;
            actorId = ticketInfo.RequesterId;
            key = $"tickets/{ticketId}/{Guid.NewGuid()}_evidence.png";
            seedContext.AttachmentUploadGrants.Add(AttachmentUploadGrant.Create(ticketId, actorId, "evidence.png", 1024,
                "image/png", key, now.AddMinutes(1)));
            await seedContext.SaveChangesAsync();
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Guid> ConfirmAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await start.Task;
            return (await ConfirmThroughUseCaseAsync(context, ticketId, actorId,
                new ConfirmAttachmentUploadRequest("evidence.png", 1024, "image/png", key),
                new ObjectStorageObjectInfo(1024, "image/png"), now)).Id;
        }

        var first = ConfirmAsync();
        var second = ConfirmAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Equal(results[0], results[1]);
        using var assertScope = _factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedGrant = await assertContext.AttachmentUploadGrants.SingleAsync(g => g.StorageKey == key);
        Assert.Equal(results[0], persistedGrant.AttachmentId);
        Assert.Equal(1, await assertContext.TicketAttachments.CountAsync(a => a.Id == results[0]));
    }

    [Fact]
    public async Task DbContext_CanConnect_AndMigrateIdentitySchema()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var canConnect = await dbContext.Database.CanConnectAsync();
        canConnect.Should().BeTrue("Should be able to connect to PostgreSQL Testcontainer");
        (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await dbContext.Roles.CountAsync()).Should().Be(3);
        (await dbContext.Users.CountAsync()).Should().Be(3);
    }

    private static async Task<(Guid TicketId, Guid RequesterId)> EnsureTicketAsync(ApplicationDbContext context)
    {
        var existing = await context.Tickets.Select(t => new { t.Id, t.RequesterId }).FirstOrDefaultAsync();
        if (existing != null) return (existing.Id, existing.RequesterId);

        var requesterId = Guid.NewGuid();
        var ticket = Ticket.Create(requesterId, "Seed Ticket", "Valid ticket description for testing", TicketCategories.VPN, DateTime.UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return (ticket.Id, ticket.RequesterId);
    }

    // Persistence và quyền đọc dùng PostgreSQL thật; chỉ thay transport object storage.
    private static async Task<TicketAttachmentDto> ConfirmThroughUseCaseAsync(ApplicationDbContext context, Guid ticketId,
        Guid actorId, ConfirmAttachmentUploadRequest request, ObjectStorageObjectInfo objectInfo, DateTime now)
    {
        var actor = new Mock<ICurrentUser>();
        actor.SetupGet(a => a.IsAuthenticated).Returns(true);
        actor.SetupGet(a => a.UserId).Returns(actorId.ToString());
        actor.SetupGet(a => a.Roles).Returns(["Requester"]);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(now));
        var storage = new Mock<IObjectStorageService>();
        storage.SetupGet(s => s.DefaultBucket).Returns("synthetic-bucket");
        storage.Setup(s => s.StatAsync("synthetic-bucket", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredObject(objectInfo.Size, objectInfo.ContentType, "synthetic-etag"));
        storage.Setup(s => s.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddTicketingApplication();
        services.AddSingleton<ITicketQueries>(new PostgresTicketQueries(context));
        services.AddSingleton<ITicketAttachmentStore>(new PostgresTicketAttachmentStore(context));
        services.AddSingleton<IObjectStorageService>(storage.Object);
        services.AddSingleton<ICurrentUser>(actor.Object);
        services.AddSingleton<IClock>(clock.Object);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IConfirmAttachmentUpload>().ExecuteAsync(new(ticketId, request));
    }
}
