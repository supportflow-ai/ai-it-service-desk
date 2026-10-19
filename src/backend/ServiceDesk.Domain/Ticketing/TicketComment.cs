namespace ServiceDesk.Domain.Ticketing;

public sealed class TicketComment
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string AuthorId { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private TicketComment()
    {
    }

    public static TicketComment Create(Guid ticketId, string authorId, string content, DateTimeOffset createdAt)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("Ticket ID is required.", nameof(ticketId));

        return new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = RequireValue(authorId, nameof(authorId)),
            Content = RequireValue(content, nameof(content)),
            CreatedAt = RequireTimestamp(createdAt, nameof(createdAt))
        };
    }

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string parameterName)
    {
        if (value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, value, "Timestamp must be within the supported range.");

        return value;
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);

        return value.Trim();
    }
}
