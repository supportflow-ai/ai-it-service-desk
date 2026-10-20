using FluentAssertions;
using ServiceDesk.Domain.Ticketing;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public class TicketTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_NormalizesInputAndInitializesRequesterTicket()
    {
        var requesterId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();

        var ticket = Ticket.Create(
            requesterId,
            "  Cannot access VPN  ",
            "  VPN connection fails from home.  ",
            TicketCategories.VPN,
            CreatedAt,
            departmentId);

        ticket.Id.Should().NotBeEmpty();
        ticket.TicketNumber.Should().BeNull();
        ticket.RequesterId.Should().Be(requesterId);
        ticket.RequesterDepartmentId.Should().Be(departmentId);
        ticket.Title.Should().Be("Cannot access VPN");
        ticket.Description.Should().Be("VPN connection fails from home.");
        ticket.CategoryId.Should().Be(TicketCategories.VPN);
        ticket.Status.Should().Be(TicketStatus.Submitted);
        ticket.CreatedAt.Should().Be(CreatedAt);
        ticket.UpdatedAt.Should().Be(CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankTitle(string title)
    {
        var act = () => CreateValidTicket(title: title);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsTitleLongerThan100Characters()
    {
        var act = () => CreateValidTicket(title: new string('x', 101));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Too short")]
    public void Create_RejectsBlankOrShortDescription(string description)
    {
        var act = () => CreateValidTicket(description: description);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("vpn")]
    public void Create_RejectsInvalidOrCaseMismatchedCategory(string categoryId)
    {
        var act = () => CreateValidTicket(categoryId: categoryId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsEmptyRequesterId()
    {
        var act = () => Ticket.Create(
            Guid.Empty,
            "Cannot access VPN",
            "VPN connection fails from home.",
            TicketCategories.VPN,
            CreatedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsOwnedBy_ReturnsTrueOnlyForRequester()
    {
        var requesterId = Guid.NewGuid();
        var ticket = CreateValidTicket(requesterId: requesterId);

        ticket.IsOwnedBy(requesterId).Should().BeTrue();
        ticket.IsOwnedBy(Guid.NewGuid()).Should().BeFalse();
        ticket.IsOwnedBy(Guid.Empty).Should().BeFalse();
    }

    [Fact]
    public void Claim_SubmittedTicket_TransitionsToInProgress()
    {
        var ticket = CreateValidTicket();
        var now = DateTime.UtcNow;

        ticket.Claim(now);

        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Claim_NonSubmittedTicket_Throws()
    {
        var ticket = CreateValidTicket();
        ticket.Claim(DateTime.UtcNow);

        var act = () => ticket.Claim(DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FullLifecycle_Resolve_Reopen_Close_Works()
    {
        var ticket = CreateValidTicket();
        var now = DateTime.UtcNow;

        ticket.Claim(now);
        ticket.Resolve(now);
        ticket.Status.Should().Be(TicketStatus.Resolved);

        ticket.Reopen(now);
        ticket.Status.Should().Be(TicketStatus.InProgress);

        ticket.Resolve(now);
        ticket.Close(now);
        ticket.Status.Should().Be(TicketStatus.Closed);
    }

    [Fact]
    public void Cancel_SubmittedTicket_SetsCancelled()
    {
        var ticket = CreateValidTicket();
        var now = DateTime.UtcNow;

        ticket.Cancel(now);

        ticket.Status.Should().Be(TicketStatus.Cancelled);
    }

    [Fact]
    public void Cancel_NonSubmittedTicket_Throws()
    {
        var ticket = CreateValidTicket();
        ticket.Claim(DateTime.UtcNow);

        var act = () => ticket.Cancel(DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Update_SubmittedTicket_UpdatesFields()
    {
        var ticket = CreateValidTicket();
        var now = DateTime.UtcNow;

        ticket.Update("New Title", "New Description at least 10 chars", TicketCategories.HW, now);

        ticket.Title.Should().Be("New Title");
        ticket.Description.Should().Be("New Description at least 10 chars");
        ticket.CategoryId.Should().Be(TicketCategories.HW);
        ticket.UpdatedAt.Should().Be(now);
    }

    private static Ticket CreateValidTicket(
        Guid? requesterId = null,
        string title = "Cannot access VPN",
        string description = "VPN connection fails from home.",
        string categoryId = TicketCategories.VPN) =>
        Ticket.Create(requesterId ?? Guid.NewGuid(), title, description, categoryId, CreatedAt);
}
