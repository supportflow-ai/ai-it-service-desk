namespace ServiceDesk.Domain.Knowledge;

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
            throw new ArgumentException("Author ID is required.");
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
            throw new InvalidOperationException("Chỉ có thể chỉnh sửa bài viết ở trạng thái nháp.");
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
            throw new InvalidOperationException("Chỉ có thể xuất bản bài viết từ trạng thái nháp.");
        }

        if (publishedById == Guid.Empty)
        {
            throw new ArgumentException("PublishedBy ID is required.");
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
            throw new InvalidOperationException("Chỉ có thể lưu trữ bài viết đang ở trạng thái đã xuất bản.");
        }

        Status = ArticleStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = now;
    }

    public void Republish(string title, string content, IEnumerable<string>? tags, Guid publishedById, DateTime now)
    {
        if (Status != ArticleStatus.Published)
            throw new InvalidOperationException("Chỉ có thể xuất bản lại bài đang được công bố.");
        if (publishedById == Guid.Empty) throw new ArgumentException("Publisher ID is required.");
        var nextTitle = ValidateAndNormalizeTitle(title);
        var nextContent = ValidateAndNormalizeContent(content);
        var nextTags = NormalizeTags(Id, tags);
        var nextVersion = checked(Version + 1);
        Title = nextTitle;
        Content = nextContent;
        _tags.Clear();
        _tags.AddRange(nextTags);
        Version = nextVersion;
        PublishedAt = UpdatedAt = now;
        _versions.Add(new ArticleVersion(Guid.NewGuid(), Id, Version, Title, Content, publishedById, now));
    }

    public bool IsAuthor(Guid userId) => AuthorId == userId;

    private static string ValidateAndNormalizeTitle(string title)
    {
        var normalized = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Title is required.");
        }

        if (normalized.Length > 200)
        {
            throw new ArgumentException("Title must not exceed 200 characters.");
        }

        return normalized;
    }

    private static string ValidateAndNormalizeContent(string content)
    {
        var normalized = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Content is required.");
        }

        if (normalized.Length < 10)
        {
            throw new ArgumentException("Content must be at least 10 characters.");
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
                throw new ArgumentException("Tag cannot be empty or whitespace.");
            }

            var normalized = tag.Trim().ToLowerInvariant();
            if (normalized.Length > 50)
            {
                throw new ArgumentException("Tag must not exceed 50 characters.");
            }

            if (seen.Add(normalized))
            {
                result.Add(new ArticleTag(articleId, normalized));
            }
        }

        if (result.Count > 10)
        {
            throw new ArgumentException("Article cannot have more than 10 tags.");
        }

        return result;
    }
}
