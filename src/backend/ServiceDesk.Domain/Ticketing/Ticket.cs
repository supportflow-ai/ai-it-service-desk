namespace ServiceDesk.Domain.Ticketing;

public sealed class Ticket
{
    private readonly List<TicketComment> _comments = [];

    public Guid Id { get; private set; }
    public string RequesterId { get; private set; } = string.Empty;
    public string? AssigneeId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public Impact Impact { get; private set; }
    public Urgency Urgency { get; private set; }
    public Priority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<TicketComment> Comments => _comments.AsReadOnly();

    private Ticket()
    {
    }

    public static Ticket Create(
        string requesterId,
        string title,
        string description,
        string category,
        Impact impact,
        Urgency urgency,
        DateTimeOffset createdAt)
    {
        return new Ticket
        {
            Id = Guid.NewGuid(),
            RequesterId = RequireValue(requesterId, nameof(requesterId)),
            Title = RequireValue(title, nameof(title)),
            Description = RequireValue(description, nameof(description)),
            Category = RequireValue(category, nameof(category)),
            Impact = EnsureDefined(impact, nameof(impact)),
            Urgency = EnsureDefined(urgency, nameof(urgency)),
            Priority = PriorityMatrix.Calculate(impact, urgency),
            Status = TicketStatus.Submitted,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Update(
        string title,
        string description,
        string category,
        Impact impact,
        Urgency urgency,
        DateTimeOffset updatedAt)
    {
        EnsureStatus(TicketStatus.Submitted);
        Title = RequireValue(title, nameof(title));
        Description = RequireValue(description, nameof(description));
        Category = RequireValue(category, nameof(category));
        Impact = EnsureDefined(impact, nameof(impact));
        Urgency = EnsureDefined(urgency, nameof(urgency));
        Priority = PriorityMatrix.Calculate(impact, urgency);
        UpdatedAt = updatedAt;
    }

    public void Claim(string agentId, DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Submitted);
        if (!string.IsNullOrWhiteSpace(AssigneeId))
            throw new InvalidOperationException("Ticket is already assigned.");

        AssigneeId = RequireValue(agentId, nameof(agentId));
        Status = TicketStatus.InProgress;
        UpdatedAt = now;
    }

    public void Assign(string assigneeId, DateTimeOffset now)
    {
        if (Status is not (TicketStatus.Submitted or TicketStatus.InProgress))
            throw new InvalidOperationException($"Cannot assign ticket while {Status}.");

        AssigneeId = RequireValue(assigneeId, nameof(assigneeId));
        if (Status == TicketStatus.Submitted)
            Status = TicketStatus.InProgress;
        UpdatedAt = now;
    }

    public void Resolve(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.InProgress);
        if (string.IsNullOrWhiteSpace(AssigneeId))
            throw new InvalidOperationException("Assigned ticket is required to resolve.");

        Status = TicketStatus.Resolved;
        ResolvedAt = now;
        UpdatedAt = now;
    }

    public void Reopen(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Resolved);
        Status = TicketStatus.InProgress;
        ResolvedAt = null;
        UpdatedAt = now;
    }

    public void Close(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Resolved);
        Status = TicketStatus.Closed;
        ClosedAt = now;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Submitted);
        Status = TicketStatus.Cancelled;
        CancelledAt = now;
        UpdatedAt = now;
    }

    public void AddComment(string authorId, string content, DateTimeOffset now)
    {
        if (Status is TicketStatus.Cancelled or TicketStatus.Closed)
            throw new InvalidOperationException("Terminal tickets cannot receive comments.");

        _comments.Add(TicketComment.Create(Id, authorId, content, now));
    }

    private void EnsureStatus(TicketStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Ticket must be {expected}, but is {Status}.");
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);

        return value.Trim();
    }

    private static T EnsureDefined<T>(T value, string parameterName) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName, value, "Enum value is invalid.");

        return value;
    }
}
