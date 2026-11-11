using ServiceDesk.SharedKernel.Application;
namespace ServiceDesk.Modules.Ticketing.Domain;

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
        if (ticketId == Guid.Empty) throw new InvalidInputException("ticket_id_required", "Cần có mã phiếu.");
        if (authorId == Guid.Empty) throw new InvalidInputException("comment_author_required", "Cần có mã tác giả.");
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length < 2)
            throw new InvalidInputException("comment_content_too_short", "Nội dung phải có ít nhất 2 ký tự.");
        if (content.Trim().Length > 4000)
            throw new InvalidInputException("comment_content_too_long", "Nội dung không được vượt quá 4000 ký tự.");
    }
}
