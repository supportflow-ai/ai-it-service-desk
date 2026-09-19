namespace ServiceDesk.Domain.Ticketing;

public class Ticket
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = null!; // IT-YYYY-NNNN format (DB sequence generated)
    public Guid RequesterId { get; set; }
    public Guid? RequesterDepartmentId { get; set; } // Snapshot at submission time (FR-TKT-10)
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // xmin (PostgreSQL system column) used as concurrency token - handled by EF Core
}
