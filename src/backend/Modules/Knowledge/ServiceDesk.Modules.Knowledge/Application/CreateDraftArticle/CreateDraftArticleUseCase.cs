namespace ServiceDesk.Modules.Knowledge.Application.CreateDraftArticle;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Knowledge.Domain;

internal sealed class CreateDraftArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : ICreateDraftArticle
{
    public async Task<ArticleDto> ExecuteAsync(CreateDraftArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsInRole(RoleNames.Agent) && !currentUser.IsInRole(RoleNames.Admin))
        {
            throw new UnauthorizedAccessException("Chỉ kỹ thuật viên hoặc quản trị viên mới có thể tạo bài viết nháp.");
        }

        var authorId = AuthenticatedActor.RequireId(currentUser);
        var article = Article.CreateDraft(authorId, command.Title, command.Content, command.Tags, clock.UtcNow.UtcDateTime);
        var created = await writer.CreateDraftAsync(article, cancellationToken);
        var revision = writer.GetRevision(created);
        return ArticleDtoMapper.ToDto(created, revision);
    }
}
