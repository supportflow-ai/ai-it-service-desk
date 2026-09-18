namespace ServiceDesk.Domain.Ticketing;

public class Ticket
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty; // IT-YYYY-NNNN format
    public Guid RequesterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // xmin (PostgreSQL system column) used as concurrency token - handled by EF Core
}
