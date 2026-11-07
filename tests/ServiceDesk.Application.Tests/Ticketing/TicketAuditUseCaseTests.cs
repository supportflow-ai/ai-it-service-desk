using Moq;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Ticketing.Application.AssignTicket;
using ServiceDesk.Modules.Ticketing.Application.CancelTicket;
using ServiceDesk.Modules.Ticketing.Application.ClaimTicket;
using ServiceDesk.Modules.Ticketing.Application.CloseTicket;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.ReopenTicket;
using ServiceDesk.Modules.Ticketing.Application.ResolveTicket;
using ServiceDesk.Modules.Ticketing.Application.UnassignTicket;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class TicketAuditUseCaseTests
{
    [Theory]
    [InlineData("assign")]
    [InlineData("unassign")]
    [InlineData("claim")]
    [InlineData("resolve")]
    [InlineData("reopen")]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task StateChange_UsesAtomicWriter_AndDoesNotHidePersistenceFailure(string operation)
    {
        var actor = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        var ticket = Ticket.Create(actor, "Lỗi VPN", "Mô tả lỗi đủ độ dài", TicketCategories.VPN, now.UtcDateTime);
        if (operation == "unassign") ticket.Assign(actor, now.UtcDateTime);
        if (operation is "resolve" or "reopen" or "close") ticket.Claim(actor, now.UtcDateTime);
        if (operation is "reopen" or "close") ticket.Resolve(now.UtcDateTime);

        var user = new Mock<ICurrentUser>();
        user.SetupGet(value => value.IsAuthenticated).Returns(true);
        user.SetupGet(value => value.UserId).Returns(actor.ToString());
        user.SetupGet(value => value.Roles).Returns([RoleNames.Requester, RoleNames.Agent]);
        user.Setup(value => value.IsInRole(It.IsAny<string>()))
            .Returns((string role) => role is RoleNames.Requester or RoleNames.Agent);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(now);
        var directory = new Mock<IAgentDirectory>();
        directory.Setup(value => value.IsAgentAsync(actor, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var writer = new Mock<ITicketWriter>(MockBehavior.Strict);
        writer.Setup(value => value.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        writer.Setup(value => value.GetRevision(ticket)).Returns(42);
        var failure = new InvalidOperationException("Lỗi ghi dữ liệu được chủ động tạo trong kiểm thử.");
        writer.Setup(value => value.UpdateTicketWithAuditAsync(ticket,
                It.Is<TicketComment>(entry => entry.TicketId == ticket.Id && entry.AuthorId == actor &&
                    entry.AuthorRole == "System" && !entry.IsInternal && entry.CreatedAt == now.UtcDateTime),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        Task<TicketDto> Execute() => operation switch
        {
            "assign" => new AssignTicketUseCase(writer.Object, directory.Object, user.Object, clock.Object).ExecuteAsync(new(ticket.Id, actor, 42)),
            "unassign" => new UnassignTicketUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(ticket.Id, 42)),
            "claim" => new ClaimTicketUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(ticket.Id, 42)),
            "resolve" => new ResolveTicketUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(ticket.Id, 42)),
            "reopen" => new ReopenTicketUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(ticket.Id, 42)),
            "close" => new CloseTicketUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(ticket.Id, 42)),
            "cancel" => new CancelTicketUseCase(writer.Object, user.Object, clock.Object).ExecuteAsync(new(ticket.Id, 42)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(Execute));
        writer.Verify(value => value.UpdateTicketWithAuditAsync(ticket, It.IsAny<TicketComment>(), It.IsAny<CancellationToken>()), Times.Once);
        writer.Verify(value => value.UpdateTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
