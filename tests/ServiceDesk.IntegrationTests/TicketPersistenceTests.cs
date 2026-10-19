using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public class TicketPersistenceTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;

    public TicketPersistenceTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SaveTicket_RoundTripsCommentsAndDerivedPriority()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ITicketStore>();
        var ticket = Ticket.Create(
            "requester-1", "Laptop", "Won't start", "Hardware",
            Impact.High, Urgency.High, DateTimeOffset.UtcNow);
        ticket.AddComment("requester-1", "Started this morning", DateTimeOffset.UtcNow);

        await store.AddAsync(ticket, CancellationToken.None);
        await store.SaveChangesAsync(CancellationToken.None);

        var saved = await store.GetByIdAsync(ticket.Id, CancellationToken.None);

        saved!.Priority.Should().Be(Priority.P1);
        (await store.GetCommentsAsync(ticket.Id, CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task AddCommentToExistingTicket_Succeeds()
    {
        await using var scope1 = _factory.Services.CreateAsyncScope();
        var store1 = scope1.ServiceProvider.GetRequiredService<ITicketStore>();
        var ticket = Ticket.Create("req-1", "Test", "Desc", "Cat", Impact.Low, Urgency.Low, DateTimeOffset.UtcNow);
        await store1.AddAsync(ticket, CancellationToken.None);
        await store1.SaveChangesAsync(CancellationToken.None);

        await using var scope2 = _factory.Services.CreateAsyncScope();
        var store2 = scope2.ServiceProvider.GetRequiredService<ITicketStore>();
        var loaded = await store2.GetByIdAsync(ticket.Id, CancellationToken.None);
        loaded!.AddComment("req-1", "A new comment", DateTimeOffset.UtcNow);
        await store2.SaveChangesAsync(CancellationToken.None);
    }
}
