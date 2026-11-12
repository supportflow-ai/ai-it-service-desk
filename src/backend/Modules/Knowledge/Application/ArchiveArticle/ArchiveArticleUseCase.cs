namespace ServiceDesk.Modules.Knowledge.Application.ArchiveArticle;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Application.Common;

/// <summary>
/// Xử lý lưu trữ bài viết đã xuất bản (Agent/Admin; tác giả hoặc Admin).
/// Các bước chung (vai trò, tải bài viết, quyền tác giả, revision, lưu) nằm ở <see cref="ArticleTransitions"/>.
/// </summary>
internal sealed class ArchiveArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IArchiveArticle
{
    public async Task<ArticleDto> ExecuteAsync(ArchiveArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new ArticleTransitions(writer, currentUser, clock).RunAsync(
            command.Id, command.ExpectedRevision, "lưu trữ bài viết", "lưu trữ bài viết",
            apply: (article, _, now) => article.Archive(now),
            cancellationToken: cancellationToken);
    }
}
