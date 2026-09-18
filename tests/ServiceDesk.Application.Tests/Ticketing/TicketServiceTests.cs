using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class TicketServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly TicketService _service;
    private readonly Mock<ICurrentUser> _currentUserMock;

    public TicketServiceTests()
    {
        // Setup in-memory SQLite
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        // Configure TicketNumber generation trigger for SQLite
        _context.Database.ExecuteSqlRaw(@"
            CREATE TRIGGER IF NOT EXISTS trg_ticket_number
            AFTER INSERT ON tickets
            FOR EACH ROW
            WHEN NEW.ticket_number = '' OR NEW.ticket_number IS NULL
            BEGIN
                UPDATE tickets
                SET ticket_number = 'IT-' || strftime('%Y', 'now') || '-' || substr('0000' || NEW.rowid, -4)
                WHERE Id = NEW.Id;
            END;
        ");

        _currentUserMock = new Mock<ICurrentUser>();
        _service = new TicketService(_context, _currentUserMock.Object);
    }

    [Fact]
    public async Task CreateTicketAsync_ValidInput_ReturnsTicketDtoWithTicketNumber()
    {
        var command = new CreateTicketCommand(
            Title: "Test ticket",
            Description: "Valid description with more than ten characters",
            CategoryId: TicketCategories.ACC
        );
        var requesterId = Guid.NewGuid();

        var result = await _service.CreateTicketAsync(command, requesterId);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.TicketNumber.Should().MatchRegex(@"^IT-\d{4}-\d{4}$");
        result.Title.Should().Be("Test ticket");
        result.Description.Should().Be("Valid description with more than ten characters");
        result.CategoryId.Should().Be(TicketCategories.ACC);
        result.Status.Should().Be((int)TicketStatus.Submitted);
    }

    [Fact]
    public async Task CreateTicketAsync_EmptyTitle_ThrowsArgumentException()
    {
        var command = new CreateTicketCommand(
            Title: "",
            Description: "Valid description",
            CategoryId: TicketCategories.NET
        );
        var requesterId = Guid.NewGuid();

        var act = async () => await _service.CreateTicketAsync(command, requesterId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Title is required");
    }

    [Fact]
    public async Task CreateTicketAsync_TitleTooLong_ThrowsArgumentException()
    {
        var command = new CreateTicketCommand(
            Title: new string('a', 101),
            Description: "Valid description",
            CategoryId: TicketCategories.SW
        );
        var requesterId = Guid.NewGuid();

        var act = async () => await _service.CreateTicketAsync(command, requesterId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Title must not exceed 100 characters");
    }

    [Fact]
    public async Task CreateTicketAsync_DescriptionTooShort_ThrowsArgumentException()
    {
        var command = new CreateTicketCommand(
            Title: "Valid title",
            Description: "Too short",
            CategoryId: TicketCategories.HW
        );
        var requesterId = Guid.NewGuid();

        var act = async () => await _service.CreateTicketAsync(command, requesterId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Description must be at least 10 characters");
    }

    [Fact]
    public async Task CreateTicketAsync_InvalidCategoryId_ThrowsArgumentException()
    {
        var command = new CreateTicketCommand(
            Title: "Valid title",
            Description: "Valid description with enough characters",
            CategoryId: "INVALID"
        );
        var requesterId = Guid.NewGuid();

        var act = async () => await _service.CreateTicketAsync(command, requesterId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid category");
    }

    [Fact]
    public async Task CreateTicketAsync_WhitespaceTitleAndDescription_Trimmed()
    {
        var command = new CreateTicketCommand(
            Title: "  Whitespace title  ",
            Description: "  Whitespace description with sufficient length  ",
            CategoryId: TicketCategories.MAIL
        );
        var requesterId = Guid.NewGuid();

        var result = await _service.CreateTicketAsync(command, requesterId);

        result.Title.Should().Be("Whitespace title");
        result.Description.Should().Be("Whitespace description with sufficient length");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
