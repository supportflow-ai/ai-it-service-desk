using ServiceDesk.SharedKernel.Application;
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
            throw new InvalidInputException("article_id_required", "Cần có mã bài viết.");
        }

        var normalizedName = name?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidInputException("article_tag_empty", "Thẻ không được để trống.");
        }

        if (normalizedName.Length > 50)
        {
            throw new InvalidInputException("article_tag_too_long", "Thẻ không được vượt quá 50 ký tự.");
        }

        ArticleId = articleId;
        Name = normalizedName;
    }

    public Guid ArticleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
