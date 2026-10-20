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

    public void Claim(DateTime now)
    {
        if (Status != TicketStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot claim ticket in status {Status}");
        }

        Status = TicketStatus.InProgress;
        UpdatedAt = now;
    }

    public void Resolve(DateTime now)
    {
        if (Status != TicketStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot resolve ticket in status {Status}");
        }

        Status = TicketStatus.Resolved;
        UpdatedAt = now;
    }

    public void Reopen(DateTime now)
    {
        if (Status != TicketStatus.Resolved)
        {
            throw new InvalidOperationException($"Cannot reopen ticket in status {Status}");
        }

        Status = TicketStatus.InProgress;
        UpdatedAt = now;
    }

    public void Close(DateTime now)
    {
        if (Status != TicketStatus.Resolved)
        {
            throw new InvalidOperationException($"Cannot close ticket in status {Status}");
        }

        Status = TicketStatus.Closed;
        UpdatedAt = now;
    }

    public void Cancel(DateTime now)
    {
        if (Status != TicketStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot cancel ticket in status {Status}");
        }

        Status = TicketStatus.Cancelled;
        UpdatedAt = now;
    }

    public void Update(string title, string description, string categoryId, DateTime now)
    {
        if (Status != TicketStatus.Submitted)
        {
            throw new InvalidOperationException("Ticket can only be updated when Submitted");
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

        Title = normalizedTitle;
        Description = normalizedDescription;
        CategoryId = categoryId;
        UpdatedAt = now;
    }
}
