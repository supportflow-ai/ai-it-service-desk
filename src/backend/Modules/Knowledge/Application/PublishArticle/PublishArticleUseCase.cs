namespace ServiceDesk.Modules.Knowledge.Application.PublishArticle;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Knowledge.Domain;

internal sealed class PublishArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IPublishArticle
{
    public async Task<ArticleDto> ExecuteAsync(PublishArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!KnowledgePermissions.IsStaff(currentUser))
        {
            throw KnowledgeErrors.StaffRequired("xuất bản bài viết");
        }

        var article = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (article == null)
        {
            throw KnowledgeErrors.NotFound();
        }

        var callerId = AuthenticatedActor.RequireId(currentUser);
        if (!KnowledgePermissions.CanManage(article, callerId, currentUser))
        {
            throw KnowledgeErrors.AuthorRequired("xuất bản bài viết");
        }

        if (!command.ExpectedRevision.HasValue || command.ExpectedRevision.Value == 0)
        {
            throw KnowledgeErrors.ExpectedRevisionRequired();
        }

        if (writer.GetRevision(article) != command.ExpectedRevision.Value)
        {
            throw new ConcurrencyConflictException("Phiên bản bài viết đã thay đổi. Vui lòng tải lại.");
        }

        var publisherId = AuthenticatedActor.RequireId(currentUser);
        article.Publish(publisherId, clock.UtcNow.UtcDateTime);

        var updated = await writer.UpdateArticleAsync(article, command.ExpectedRevision, cancellationToken);
        var revision = writer.GetRevision(updated);
        return ArticleDtoMapper.ToDto(updated, revision);
    }
}
