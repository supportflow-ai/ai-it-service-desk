using ServiceDesk.SharedKernel.Application;
using ServiceDesk.Modules.Knowledge.Domain;
namespace ServiceDesk.Modules.Knowledge.Domain;

/// <summary>
/// Thực thể gốc (Aggregate Root) đại diện cho bài viết tri thức (Knowledge Base Article).
/// </summary>
public class Article
{
    private readonly List<ArticleTag> _tags = [];
    private readonly List<ArticleVersion> _versions = [];

    private Article() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public ArticleStatus Status { get; private set; }
    public Guid AuthorId { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    public IReadOnlyCollection<ArticleTag> Tags => _tags.AsReadOnly();
    public IReadOnlyCollection<ArticleVersion> Versions => _versions.AsReadOnly();

    public static Article CreateDraft(
        Guid authorId,
        string title,
        string content,
        IEnumerable<string>? tags,
        DateTime now)
    {
        if (authorId == Guid.Empty)
        {
            throw new InvalidInputException("article_author_required", "Cần có mã tác giả.");
        }

        var normalizedTitle = ValidateAndNormalizeTitle(title);
        var normalizedContent = ValidateAndNormalizeContent(content);

        var articleId = Guid.NewGuid();
        var normalizedTags = NormalizeTags(articleId, tags);

        var article = new Article
        {
            Id = articleId,
            AuthorId = authorId,
            Title = normalizedTitle,
            Content = normalizedContent,
            Status = ArticleStatus.Draft,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        article._tags.AddRange(normalizedTags);
        return article;
    }

    public void UpdateDraft(string title, string content, IEnumerable<string>? tags, DateTime now)
    {
        if (Status != ArticleStatus.Draft)
        {
            throw KnowledgeErrors.StateViolation("article_not_draft", "Chỉ có thể chỉnh sửa bài viết ở trạng thái nháp.");
        }

        var normalizedTitle = ValidateAndNormalizeTitle(title);
        var normalizedContent = ValidateAndNormalizeContent(content);
        var normalizedTags = NormalizeTags(Id, tags);

        Title = normalizedTitle;
        Content = normalizedContent;
        _tags.Clear();
        _tags.AddRange(normalizedTags);
        UpdatedAt = now;
    }

    public void Publish(Guid publishedById, DateTime now)
    {
        if (Status != ArticleStatus.Draft)
        {
            throw KnowledgeErrors.StateViolation("article_publish_requires_draft", "Chỉ có thể xuất bản bài viết từ trạng thái nháp.");
        }

        if (publishedById == Guid.Empty)
        {
            throw new InvalidInputException("article_publisher_required", "Cần có mã người xuất bản.");
        }

        Status = ArticleStatus.Published;
        PublishedAt = now;
        UpdatedAt = now;
        _versions.Add(new ArticleVersion(Guid.NewGuid(), Id, Version, Title, Content, publishedById, now));
    }

    public void Archive(DateTime now)
    {
        if (Status != ArticleStatus.Published)
        {
            throw KnowledgeErrors.StateViolation("article_archive_requires_published", "Chỉ có thể lưu trữ bài viết đang ở trạng thái đã xuất bản.");
        }

        Status = ArticleStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = now;
    }

    public bool IsAuthor(Guid userId) => AuthorId == userId;

    private static string ValidateAndNormalizeTitle(string title)
    {
        var normalized = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidInputException("article_title_required", "Tiêu đề không được để trống.");
        }

        if (normalized.Length > 200)
        {
            throw new InvalidInputException("article_title_too_long", "Tiêu đề không được vượt quá 200 ký tự.");
        }

        return normalized;
    }

    private static string ValidateAndNormalizeContent(string content)
    {
        var normalized = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidInputException("article_content_required", "Nội dung không được để trống.");
        }

        if (normalized.Length < 10)
        {
            throw new InvalidInputException("article_content_too_short", "Nội dung phải có ít nhất 10 ký tự.");
        }

        return normalized;
    }

    private static List<ArticleTag> NormalizeTags(Guid articleId, IEnumerable<string>? tags)
    {
        if (tags is null) return [];

        var result = new List<ArticleTag>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new InvalidInputException("article_tag_empty", "Thẻ không được để trống.");
            }

            var normalized = tag.Trim().ToLowerInvariant();
            if (normalized.Length > 50)
            {
                throw new InvalidInputException("article_tag_too_long", "Thẻ không được vượt quá 50 ký tự.");
            }

            if (seen.Add(normalized))
            {
                result.Add(new ArticleTag(articleId, normalized));
            }
        }

        if (result.Count > 10)
        {
            throw new InvalidInputException("article_too_many_tags", "Bài viết không được có quá 10 thẻ.");
        }

        return result;
    }
}
