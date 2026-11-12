using FluentAssertions;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

/// <summary>
/// Quy tắc tiêu đề, mô tả, danh mục dùng chung cho <see cref="Ticket.Create"/> và <see cref="Ticket.Update"/>:
/// biên độ dài, chuẩn hóa khoảng trắng, mã lỗi ổn định và thứ tự kiểm.
/// </summary>
public sealed class TicketFieldRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);
    private const string ValidDescription = "Mô tả đủ dài cho phiếu kiểm thử";

    private static Ticket NewTicket() =>
        Ticket.Create(Guid.NewGuid(), "Không vào được VPN", ValidDescription, TicketCategories.VPN, Now);

    /// <summary>Chạy cùng một đầu vào qua cả Create và Update để chứng minh hai đường dùng chung một quy tắc.</summary>
    private static (Action Create, Action Update) Both(string title, string description, string category) =>
        (() => Ticket.Create(Guid.NewGuid(), title, description, category, Now),
         () => NewTicket().Update(title, description, category, Now));

    [Theory]
    [InlineData(null, "ticket_title_required")]
    [InlineData("", "ticket_title_required")]
    [InlineData("   ", "ticket_title_required")]
    public void BlankTitleIsRejectedByCreateAndUpdate(string? title, string code)
    {
        var (create, update) = Both(title!, ValidDescription, TicketCategories.VPN);

        create.Should().Throw<InvalidInputException>().Which.Code.Should().Be(code);
        update.Should().Throw<InvalidInputException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void TitleBoundaryIs100CharactersAfterTrimming()
    {
        var atLimit = " " + new string('x', 100) + " ";
        var overLimit = new string('x', 101);

        var ticket = Ticket.Create(Guid.NewGuid(), atLimit, ValidDescription, TicketCategories.VPN, Now);
        ticket.Title.Should().Be(new string('x', 100));

        var (create, update) = Both(overLimit, ValidDescription, TicketCategories.VPN);
        var error = create.Should().Throw<InvalidInputException>().Which;
        error.Code.Should().Be("ticket_title_too_long");
        error.Message.Should().Be("Tiêu đề không được vượt quá 100 ký tự.");
        update.Should().Throw<InvalidInputException>().Which.Code.Should().Be("ticket_title_too_long");
    }

    [Theory]
    [InlineData(null, "ticket_description_required")]
    [InlineData("   ", "ticket_description_required")]
    [InlineData("123456789", "ticket_description_too_short")]
    [InlineData("   123456789   ", "ticket_description_too_short")]
    public void BlankOrShortDescriptionIsRejectedByCreateAndUpdate(string? description, string code)
    {
        var (create, update) = Both("Tiêu đề hợp lệ", description!, TicketCategories.VPN);

        create.Should().Throw<InvalidInputException>().Which.Code.Should().Be(code);
        update.Should().Throw<InvalidInputException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void DescriptionBoundaryIs10CharactersAfterTrimmingAndHasNoUpperLimit()
    {
        Ticket.Create(Guid.NewGuid(), "Tiêu đề", "  1234567890  ", TicketCategories.VPN, Now)
            .Description.Should().Be("1234567890");

        var huge = new string('y', 50_000);
        Ticket.Create(Guid.NewGuid(), "Tiêu đề", huge, TicketCategories.VPN, Now)
            .Description.Should().HaveLength(50_000);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("vpn")]
    [InlineData(" VPN")]
    [InlineData("VPN ")]
    [InlineData("UNKNOWN")]
    public void CategoryMustMatchExactlyAndIsNotTrimmed(string? category)
    {
        var (create, update) = Both("Tiêu đề hợp lệ", ValidDescription, category!);

        create.Should().Throw<InvalidInputException>().Which.Code.Should().Be("ticket_category_invalid");
        update.Should().Throw<InvalidInputException>().Which.Code.Should().Be("ticket_category_invalid");
    }

    [Fact]
    public void ValidationOrderIsTitleThenDescriptionThenCategory()
    {
        var (create, _) = Both("", "", "bad");
        create.Should().Throw<InvalidInputException>().Which.Code.Should().Be("ticket_title_required");

        (create, _) = Both("ok", "", "bad");
        create.Should().Throw<InvalidInputException>().Which.Code.Should().Be("ticket_description_required");

        (create, _) = Both("ok", ValidDescription, "bad");
        create.Should().Throw<InvalidInputException>().Which.Code.Should().Be("ticket_category_invalid");
    }

    [Fact]
    public void UpdateChecksStateBeforeFields()
    {
        var ticket = NewTicket();
        ticket.Claim(Guid.NewGuid(), Now);

        var error = ((Action)(() => ticket.Update("", "", "bad", Now))).Should().Throw<RuleViolationException>().Which;

        error.Code.Should().Be("ticket_update_requires_submitted");
    }

    [Fact]
    public void FailedUpdateLeavesTheTicketUntouched()
    {
        var ticket = NewTicket();

        ((Action)(() => ticket.Update("Tiêu đề mới", ValidDescription, "bad", Now.AddHours(1)))).Should().Throw<InvalidInputException>();

        ticket.Title.Should().Be("Không vào được VPN");
        ticket.CategoryId.Should().Be(TicketCategories.VPN);
        ticket.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void CategoryListHasASingleSourceInTheEnum()
    {
        TicketCategories.All.Should().Equal(Enum.GetNames<TicketCategory>());
        TicketCategories.All.Should().Equal(
            TicketCategories.ACC, TicketCategories.NET, TicketCategories.VPN, TicketCategories.MAIL, TicketCategories.SW,
            TicketCategories.HW, TicketCategories.PRINT, TicketCategories.ACCESS, TicketCategories.EQUIP,
            TicketCategories.SEC, TicketCategories.OTHER);
    }

    [Fact]
    public void EveryEnumCategoryIsAcceptedByCreate()
    {
        foreach (var name in Enum.GetNames<TicketCategory>())
            Ticket.Create(Guid.NewGuid(), "Tiêu đề", ValidDescription, name, Now).CategoryId.Should().Be(name);
    }
}
