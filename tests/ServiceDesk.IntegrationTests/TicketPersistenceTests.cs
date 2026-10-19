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
}
