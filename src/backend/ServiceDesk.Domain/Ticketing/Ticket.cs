namespace ServiceDesk.Domain.Ticketing;

public class Ticket
{
    private Ticket()
    {
    }

    public Guid Id { get; private set; }
    public string TicketNumber { get; private set; } = null!; // IT-YYYY-NNNN format (DB sequence generated)
    public Guid RequesterId { get; private set; }
    public Guid? RequesterDepartmentId { get; private set; } // Snapshot at submission time (FR-TKT-10)
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string CategoryId { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    // xmin (PostgreSQL system column) used as concurrency token - handled by EF Core

    public static Ticket Create(
        Guid requesterId,
        string title,
        string description,
        string categoryId,
        DateTime createdAt,
        Guid? requesterDepartmentId = null)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException("Requester ID is required");
        }

        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new ArgumentException("Title is required");
        }

        if (normalizedTitle.Length > 100)
        {
            throw new ArgumentException("Title must not exceed 100 characters");
        }

        var normalizedDescription = description?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            throw new ArgumentException("Description is required");
        }

        if (normalizedDescription.Length < 10)
        {
            throw new ArgumentException("Description must be at least 10 characters");
        }

        if (!TicketCategories.All.Contains(categoryId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Invalid category");
        }

        return new Ticket
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            RequesterDepartmentId = requesterDepartmentId,
            Title = normalizedTitle,
            Description = normalizedDescription,
            CategoryId = categoryId,
            Status = TicketStatus.Submitted,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public bool IsOwnedBy(Guid requesterId) =>
        requesterId != Guid.Empty && RequesterId == requesterId;
}
