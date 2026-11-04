namespace ServiceDesk.Application.Knowledge.UpdateDraftArticle;

using ServiceDesk.Application.Common;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Contracts;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;

internal sealed class UpdateDraftArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IUpdateDraftArticle
{
    public async Task<ArticleDto> ExecuteAsync(UpdateDraftArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsInRole(RoleNames.Agent) && !currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ kỹ thuật viên hoặc quản trị viên mới có thể chỉnh sửa bài viết.");
        }

        var article = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (article == null)
        {
            throw new KeyNotFoundException("Không tìm thấy bài viết.");
        }

        var callerId = Guid.Parse(currentUser.UserId!);
        if (!currentUser.IsInRole(RoleNames.Admin) && !article.IsAuthor(callerId))
        {
            throw new UnauthorizedAccessException("Chỉ tác giả hoặc quản trị viên mới có quyền chỉnh sửa bản nháp.");
        }

        if (!command.ExpectedRevision.HasValue || command.ExpectedRevision.Value == 0)
        {
            throw new ArgumentException("Expected revision is required.");
        }

        if (writer.GetRevision(article) != command.ExpectedRevision.Value)
        {
            throw new ConcurrencyConflictException("Phiên bản bài viết đã thay đổi. Vui lòng tải lại.");
        }

        article.UpdateDraft(command.Title, command.Content, command.Tags, clock.UtcNow.UtcDateTime);
        var updated = await writer.UpdateArticleAsync(article, command.ExpectedRevision, cancellationToken);
        var revision = writer.GetRevision(updated);
        return ArticleDtoMapper.ToDto(updated, revision);
    }
}
