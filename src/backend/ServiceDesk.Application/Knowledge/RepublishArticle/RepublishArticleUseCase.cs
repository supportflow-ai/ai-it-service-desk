using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Knowledge.RepublishArticle;

public sealed record RepublishArticleCommand(Guid Id, string Title, string Content,
    IReadOnlyList<string>? Tags, uint? ExpectedRevision);

public interface IRepublishArticle
{
    Task<ArticleDto> ExecuteAsync(RepublishArticleCommand command, CancellationToken ct = default);
}

internal sealed class RepublishArticleUseCase(IKnowledgeWriter writer, ICurrentUser user, IClock clock)
    : IRepublishArticle
{
    public async Task<ArticleDto> ExecuteAsync(RepublishArticleCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!user.IsInRole(RoleNames.Agent) && !user.IsInRole(RoleNames.Admin))
            throw new UnauthorizedAccessException("Không có quyền xuất bản tri thức.");
        var article = await writer.GetForUpdateAsync(command.Id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy bài viết.");
        var actor = Guid.Parse(user.UserId!);
        if (!user.IsInRole(RoleNames.Admin) && !article.IsAuthor(actor))
            throw new UnauthorizedAccessException("Chỉ tác giả hoặc quản trị viên được xuất bản lại.");
        if (command.ExpectedRevision is null or 0) throw new ArgumentException("Expected revision is required.");
        if (writer.GetRevision(article) != command.ExpectedRevision)
            throw new ConcurrencyConflictException("Phiên bản bài viết đã thay đổi.");
        article.Republish(command.Title, command.Content, command.Tags, actor, clock.UtcNow.UtcDateTime);
        var updated = await writer.UpdateArticleAsync(article, command.ExpectedRevision, ct);
        return ArticleDtoMapper.ToDto(updated, writer.GetRevision(updated));
    }
}
