namespace ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Knowledge.Domain;

internal sealed class UpdateDraftArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IUpdateDraftArticle
{
    public async Task<ArticleDto> ExecuteAsync(UpdateDraftArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!KnowledgePermissions.IsStaff(currentUser))
        {
            throw KnowledgeErrors.StaffRequired("chỉnh sửa bài viết");
        }

        var article = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (article == null)
        {
            throw KnowledgeErrors.NotFound();
        }

        var callerId = AuthenticatedActor.RequireId(currentUser);
        if (!KnowledgePermissions.CanManage(article, callerId, currentUser))
        {
            throw KnowledgeErrors.AuthorRequired("chỉnh sửa bản nháp");
        }

        if (!command.ExpectedRevision.HasValue || command.ExpectedRevision.Value == 0)
        {
            throw KnowledgeErrors.ExpectedRevisionRequired();
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
