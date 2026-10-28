namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Thực thể biểu diễn một bình luận công khai hoặc ghi chú nội bộ trên Ticket.
/// Phân biệt chặt chẽ qua cờ IsInternal.
/// </summary>
public sealed class TicketComment
{
    private TicketComment() { }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public string AuthorRole { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Tạo bình luận công khai (Requester hoặc Staff).</summary>
    public static TicketComment CreatePublic(
        Guid ticketId,
        Guid authorId,
        string authorName,
        string authorRole,
        string content,
        DateTime now)
    {
        Validate(ticketId, authorId, content);
        return new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = authorId,
            AuthorName = authorName,
            AuthorRole = authorRole,
            Content = content.Trim(),
            IsInternal = false,
            CreatedAt = now
        };
    }

    /// <summary>Tạo ghi chú nội bộ (chỉ dành cho Agent hoặc Admin).</summary>
    public static TicketComment CreateInternalNote(
        Guid ticketId,
        Guid authorId,
        string authorName,
        string authorRole,
        string content,
        DateTime now)
    {
        Validate(ticketId, authorId, content);
        return new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = authorId,
            AuthorName = authorName,
            AuthorRole = authorRole,
            Content = content.Trim(),
            IsInternal = true,
            CreatedAt = now
        };
    }

    private static void Validate(Guid ticketId, Guid authorId, string content)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("Ticket ID is required.");
        if (authorId == Guid.Empty) throw new ArgumentException("Author ID is required.");
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length < 2)
            throw new ArgumentException("Content must be at least 2 characters.");
        if (content.Trim().Length > 4000)
            throw new ArgumentException("Content must not exceed 4000 characters.");
    }
}
