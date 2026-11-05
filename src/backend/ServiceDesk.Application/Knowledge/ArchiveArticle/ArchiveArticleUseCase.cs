namespace ServiceDesk.Application.Knowledge.ArchiveArticle;

using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;

internal sealed class ArchiveArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IArchiveArticle
{
    public async Task<ArticleDto> ExecuteAsync(ArchiveArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsInRole(RoleNames.Agent) && !currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ kỹ thuật viên hoặc quản trị viên mới có thể lưu trữ bài viết.");
        }

        var article = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (article == null)
        {
            throw new KeyNotFoundException("Không tìm thấy bài viết.");
        }

        var callerId = AuthenticatedActor.RequireId(currentUser);
        if (!currentUser.IsInRole(RoleNames.Admin) && !article.IsAuthor(callerId))
        {
            throw new UnauthorizedAccessException("Chỉ tác giả hoặc quản trị viên mới có quyền lưu trữ bài viết.");
        }

        if (!command.ExpectedRevision.HasValue || command.ExpectedRevision.Value == 0)
        {
            throw new ArgumentException("Expected revision is required.");
        }

        if (writer.GetRevision(article) != command.ExpectedRevision.Value)
        {
            throw new ConcurrencyConflictException("Phiên bản bài viết đã thay đổi. Vui lòng tải lại.");
        }

        article.Archive(clock.UtcNow.UtcDateTime);

        var updated = await writer.UpdateArticleAsync(article, command.ExpectedRevision, cancellationToken);
        return ArticleDtoMapper.ToDto(updated, writer.GetRevision(updated));
    }
}
