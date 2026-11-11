namespace ServiceDesk.Modules.Knowledge.Application.ArchiveArticle;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Knowledge.Domain;

internal sealed class ArchiveArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IArchiveArticle
{
    public async Task<ArticleDto> ExecuteAsync(ArchiveArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!KnowledgePermissions.IsStaff(currentUser))
        {
            throw KnowledgeErrors.StaffRequired("lưu trữ bài viết");
        }

        var article = await writer.GetForUpdateAsync(command.Id, cancellationToken);
        if (article == null)
        {
            throw KnowledgeErrors.NotFound();
        }

        var callerId = AuthenticatedActor.RequireId(currentUser);
        if (!KnowledgePermissions.CanManage(article, callerId, currentUser))
        {
            throw KnowledgeErrors.AuthorRequired("lưu trữ bài viết");
        }

        if (!command.ExpectedRevision.HasValue || command.ExpectedRevision.Value == 0)
        {
            throw KnowledgeErrors.ExpectedRevisionRequired();
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
