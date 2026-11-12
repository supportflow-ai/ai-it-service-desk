using ServiceDesk.SharedKernel.Application;
namespace ServiceDesk.Modules.Knowledge.Domain;

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
            throw new InvalidInputException("article_version_id_required", "Cần có mã phiên bản.");
        }

        if (articleId == Guid.Empty)
        {
            throw new InvalidInputException("article_id_required", "Cần có mã bài viết.");
        }

        if (versionNumber <= 0)
        {
            throw new InvalidInputException("article_version_number_invalid", "Số phiên bản phải lớn hơn 0.");
        }

        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new InvalidInputException("article_title_required", "Tiêu đề không được để trống.");
        }

        var normalizedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new InvalidInputException("article_content_required", "Nội dung không được để trống.");
        }

        if (publishedById == Guid.Empty)
        {
            throw new InvalidInputException("article_publisher_required", "Cần có mã người xuất bản.");
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
