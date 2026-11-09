namespace ServiceDesk.Modules.Knowledge.Domain;

/// <summary>
/// Thẻ phân loại gắn liền với bài viết tri thức (Knowledge Base Article).
/// </summary>
public sealed class ArticleTag
{
    private ArticleTag() { }

    public ArticleTag(Guid articleId, string name)
    {
        if (articleId == Guid.Empty)
        {
            throw new ArgumentException("Article ID is required.", nameof(articleId));
        }

        var normalizedName = name?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Tag name is required.", nameof(name));
        }

        if (normalizedName.Length > 50)
        {
            throw new ArgumentException("Tag name must not exceed 50 characters.", nameof(name));
        }

        ArticleId = articleId;
        Name = normalizedName;
    }

    public Guid ArticleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
