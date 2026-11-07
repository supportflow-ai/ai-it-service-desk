using Moq;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Common;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

/// <summary>Kiểm thử ma trận quyền và các bước chung của lệnh ghi ticket.</summary>
public sealed class TicketPermissionsAndGuardTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    private static ICurrentUser UserInRoles(params string[] roles)
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.IsInRole(It.IsAny<string>())).Returns<string>(role => roles.Contains(role));
        return user.Object;
    }

    private static Ticket NewTicket(Guid owner) =>
        Ticket.Create(owner, "Không vào được VPN", "Mô tả đủ dài cho ticket kiểm thử", TicketCategories.VPN, Now);

    [Theory]
    [InlineData(RoleNames.Agent, true)]
    [InlineData(RoleNames.Admin, true)]
    [InlineData(RoleNames.Requester, false)]
    public void IsStaffOnlyForAgentOrAdmin(string role, bool expected) =>
        Assert.Equal(expected, TicketPermissions.IsStaff(UserInRoles(role)));

    [Fact]
    public void OwnerCanActAsOwner()
    {
        var owner = Guid.NewGuid();
        Assert.True(TicketPermissions.CanActAsOwner(NewTicket(owner), owner, UserInRoles(RoleNames.Requester)));
    }

    [Fact]
    public void AdminCanActForAnotherOwner() =>
        Assert.True(TicketPermissions.CanActAsOwner(NewTicket(Guid.NewGuid()), Guid.NewGuid(), UserInRoles(RoleNames.Admin)));

    [Theory]
    [InlineData(RoleNames.Requester)]
    [InlineData(RoleNames.Agent)]
    public void NonOwnerWithoutAdminCannotActAsOwner(string role) =>
        Assert.False(TicketPermissions.CanActAsOwner(NewTicket(Guid.NewGuid()), Guid.NewGuid(), UserInRoles(role)));

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    public void MissingExpectedRevisionIsBadRequest(uint? expected)
    {
        var ticket = NewTicket(Guid.NewGuid());
        var error = Assert.Throws<ArgumentException>(() =>
            TicketCommandGuard.RequireCurrentRevision(Mock.Of<ITicketWriter>(), ticket, expected));
        Assert.Equal("Expected revision is required.", error.Message);
    }

    [Fact]
    public void StaleRevisionIsConflict()
    {
        var ticket = NewTicket(Guid.NewGuid());
        var writer = new Mock<ITicketWriter>();
        writer.Setup(w => w.GetRevision(ticket)).Returns(3);

        Assert.Throws<ConcurrencyConflictException>(() => TicketCommandGuard.RequireCurrentRevision(writer.Object, ticket, 2));
        TicketCommandGuard.RequireCurrentRevision(writer.Object, ticket, 3);
    }

    [Fact]
    public void RuleViolationBecomesArgumentExceptionWithSameMessage()
    {
        var ticket = NewTicket(Guid.NewGuid());
        var error = Assert.Throws<ArgumentException>(() =>
            TicketCommandGuard.ApplyRule(ticket, _ => throw new InvalidOperationException("Không thể chuyển trạng thái.")));
        Assert.Equal("Không thể chuyển trạng thái.", error.Message);
    }

    [Fact]
    public void ArgumentErrorsAreOnlyRewrappedWhenRequested()
    {
        var ticket = NewTicket(Guid.NewGuid());
        var original = new ArgumentOutOfRangeException("agentId");

        Assert.Same(original, Assert.Throws<ArgumentOutOfRangeException>(() => TicketCommandGuard.ApplyRule(ticket, _ => throw original)));
        Assert.Throws<ArgumentException>(() => TicketCommandGuard.ApplyRule(ticket, _ => throw original, includeArgumentErrors: true));
    }

    [Fact]
    public async Task SystemAuditIsWrittenWithSystemAuthor()
    {
        var actor = Guid.NewGuid();
        var ticket = NewTicket(Guid.NewGuid());
        var writer = new Mock<ITicketWriter>(MockBehavior.Strict);
        var dto = new TicketDto(ticket.Id, "IT-2026-0001", ticket.Title, ticket.Description, ticket.CategoryId, (int)ticket.Status, ticket.CreatedAt);
        writer.Setup(w => w.UpdateTicketWithAuditAsync(ticket, It.Is<TicketComment>(c =>
                c.TicketId == ticket.Id && c.AuthorId == actor && c.AuthorName == SystemAuthor.Name &&
                c.AuthorRole == SystemAuthor.Role && c.Content == "Đã xử lý." && !c.IsInternal), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        Assert.Same(dto, await TicketCommandGuard.SaveWithSystemAuditAsync(writer.Object, ticket, actor, "Đã xử lý.", Now, default));
        writer.VerifyAll();
    }
}
