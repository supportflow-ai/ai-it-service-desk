namespace ServiceDesk.Modules.Knowledge.Application.PublishArticle;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Application.Common;

/// <summary>
/// Xử lý xuất bản bài viết nháp (Agent/Admin; tác giả hoặc Admin).
/// Các bước chung (vai trò, tải bài viết, quyền tác giả, revision, lưu) nằm ở <see cref="ArticleTransitions"/>.
/// </summary>
internal sealed class PublishArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IPublishArticle
{
    public async Task<ArticleDto> ExecuteAsync(PublishArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new ArticleTransitions(writer, currentUser, clock).RunAsync(
            command.Id, command.ExpectedRevision, "xuất bản bài viết", "xuất bản bài viết",
            apply: (article, actorId, now) => article.Publish(actorId, now),
            cancellationToken: cancellationToken);
    }
}
