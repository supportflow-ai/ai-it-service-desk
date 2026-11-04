namespace ServiceDesk.Domain.Knowledge;

/// <summary>
/// Bản ghi bất biến lưu lịch sử các phiên bản đã xuất bản của bài viết tri thức.
/// </summary>
public sealed class ArticleVersion
{
    private ArticleVersion() { }

    public ArticleVersion(
        Guid id,
        Guid articleId,
        int versionNumber,
        string title,
        string content,
        Guid publishedById,
        DateTime publishedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Version ID is required.", nameof(id));
        }

        if (articleId == Guid.Empty)
        {
            throw new ArgumentException("Article ID is required.", nameof(articleId));
        }

        if (versionNumber <= 0)
        {
            throw new ArgumentException("Version number must be positive.", nameof(versionNumber));
        }

        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        var normalizedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        if (publishedById == Guid.Empty)
        {
            throw new ArgumentException("PublishedBy ID is required.", nameof(publishedById));
        }

        Id = id;
        ArticleId = articleId;
        VersionNumber = versionNumber;
        Title = normalizedTitle;
        Content = normalizedContent;
        PublishedById = publishedById;
        PublishedAt = publishedAt;
    }

    public Guid Id { get; private set; }
    public Guid ArticleId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public Guid PublishedById { get; private set; }
    public DateTime PublishedAt { get; private set; }
}
