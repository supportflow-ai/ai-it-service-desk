namespace ServiceDesk.Application.Knowledge.PublishArticle;

using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;

internal sealed class PublishArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IPublishArticle
{
    public async Task<ArticleDto> ExecuteAsync(PublishArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsInRole(RoleNames.Agent) && !currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ kỹ thuật viên hoặc quản trị viên mới có thể xuất bản bài viết.");
        }

        var article = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (article == null)
        {
            throw new KeyNotFoundException("Không tìm thấy bài viết.");
        }

        var callerId = Guid.Parse(currentUser.UserId!);
        if (!currentUser.IsInRole(RoleNames.Admin) && !article.IsAuthor(callerId))
        {
            throw new UnauthorizedAccessException("Chỉ tác giả hoặc quản trị viên mới có quyền xuất bản bài viết.");
        }

        if (!command.ExpectedRevision.HasValue || command.ExpectedRevision.Value == 0)
        {
            throw new ArgumentException("Expected revision is required.");
        }

        if (writer.GetRevision(article) != command.ExpectedRevision.Value)
        {
            throw new ConcurrencyConflictException("Phiên bản bài viết đã thay đổi. Vui lòng tải lại.");
        }

        var publisherId = Guid.Parse(currentUser.UserId!);
        article.Publish(publisherId, clock.UtcNow.UtcDateTime);

        var updated = await writer.UpdateArticleAsync(article, command.ExpectedRevision, cancellationToken);
        var revision = writer.GetRevision(updated);
        return ArticleDtoMapper.ToDto(updated, revision);
    }
}
