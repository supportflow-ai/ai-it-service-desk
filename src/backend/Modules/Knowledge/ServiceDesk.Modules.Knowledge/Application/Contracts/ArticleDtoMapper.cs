namespace ServiceDesk.Modules.Knowledge.Application.Contracts;

using ServiceDesk.Modules.Knowledge.Domain;

internal static class ArticleDtoMapper
{
    public static ArticleDto ToDto(Article article, uint revision) => new(
        article.Id,
        article.Title,
        article.Content,
        article.Status,
        article.AuthorId,
        article.Version,
        revision,
        article.Tags.Select(t => t.Name).ToList(),
        article.CreatedAt,
        article.UpdatedAt,
        article.PublishedAt,
        article.ArchivedAt
    );
}
