using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Domain.Ticketing;
using ServiceDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ServiceDesk.Application.Ticketing;

public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public TicketService(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketCommand command, Guid requesterId, CancellationToken cancellationToken = default)
    {
        // Title validation
        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ArgumentException("Title is required");
        
        if (command.Title.Length > 100)
            throw new ArgumentException("Title must not exceed 100 characters");

        // Description validation
        if (string.IsNullOrWhiteSpace(command.Description))
            throw new ArgumentException("Description is required");
        
        if (command.Description.Trim().Length < 10)
            throw new ArgumentException("Description must be at least 10 characters");

        // CategoryId validation
        if (!TicketCategories.All.Contains(command.CategoryId))
            throw new ArgumentException("Invalid category");

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            Title = command.Title.Trim(),
            Description = command.Description.Trim(),
            CategoryId = command.CategoryId,
            Status = TicketStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload to get DB-generated TicketNumber
        await _context.Entry(ticket).ReloadAsync(cancellationToken);

        return new TicketDto(
            ticket.Id,
            ticket.TicketNumber!,
            ticket.Title,
            ticket.Description,
            ticket.CategoryId,
            (int)ticket.Status,
            ticket.CreatedAt
        );
    }
}
