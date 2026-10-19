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
            CreatedAt = RequireTimestamp(createdAt, nameof(createdAt)),
            UpdatedAt = RequireTimestamp(createdAt, nameof(createdAt))
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
        var timestamp = RequireTimestamp(updatedAt, nameof(updatedAt));
        Title = RequireValue(title, nameof(title));
        Description = RequireValue(description, nameof(description));
        Category = RequireValue(category, nameof(category));
        Impact = EnsureDefined(impact, nameof(impact));
        Urgency = EnsureDefined(urgency, nameof(urgency));
        Priority = PriorityMatrix.Calculate(impact, urgency);
        UpdatedAt = timestamp;
    }

    public void Claim(string agentId, DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Submitted);
        var timestamp = RequireTimestamp(now, nameof(now));
        if (!string.IsNullOrWhiteSpace(AssigneeId))
            throw new InvalidOperationException("Ticket is already assigned.");

        AssigneeId = RequireValue(agentId, nameof(agentId));
        Status = TicketStatus.InProgress;
        UpdatedAt = timestamp;
    }

    public void Assign(string assigneeId, DateTimeOffset now)
    {
        if (Status is not (TicketStatus.Submitted or TicketStatus.InProgress))
            throw new InvalidOperationException($"Cannot assign ticket while {Status}.");

        var timestamp = RequireTimestamp(now, nameof(now));
        AssigneeId = RequireValue(assigneeId, nameof(assigneeId));
        if (Status == TicketStatus.Submitted)
            Status = TicketStatus.InProgress;
        UpdatedAt = timestamp;
    }

    public void Resolve(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.InProgress);
        if (string.IsNullOrWhiteSpace(AssigneeId))
            throw new InvalidOperationException("Assigned ticket is required to resolve.");

        var timestamp = RequireTimestamp(now, nameof(now));
        Status = TicketStatus.Resolved;
        ResolvedAt = timestamp;
        UpdatedAt = timestamp;
    }

    public void Reopen(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Resolved);
        Status = TicketStatus.InProgress;
        ResolvedAt = null;
        UpdatedAt = RequireTimestamp(now, nameof(now));
    }

    public void Close(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Resolved);
        var timestamp = RequireTimestamp(now, nameof(now));
        Status = TicketStatus.Closed;
        ClosedAt = timestamp;
        UpdatedAt = timestamp;
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Submitted);
        var timestamp = RequireTimestamp(now, nameof(now));
        Status = TicketStatus.Cancelled;
        CancelledAt = timestamp;
        UpdatedAt = timestamp;
    }

    public void AddComment(string authorId, string content, DateTimeOffset now)
    {
        if (Status is TicketStatus.Cancelled or TicketStatus.Closed)
            throw new InvalidOperationException("Terminal tickets cannot receive comments.");

        _comments.Add(TicketComment.Create(Id, authorId, content, RequireTimestamp(now, nameof(now))));
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

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string parameterName)
    {
        if (value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, value, "Timestamp must be within the supported range.");

        return value;
    }

    private static T EnsureDefined<T>(T value, string parameterName) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName, value, "Enum value is invalid.");

        return value;
    }
}
