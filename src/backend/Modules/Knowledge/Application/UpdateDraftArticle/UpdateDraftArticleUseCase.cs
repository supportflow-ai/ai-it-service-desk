namespace ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle;

using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Application.Common;

/// <summary>
/// Xử lý chỉnh sửa bản nháp bài viết (Agent/Admin; tác giả hoặc Admin).
/// Các bước chung (vai trò, tải bài viết, quyền tác giả, revision, lưu) nằm ở <see cref="ArticleTransitions"/>.
/// </summary>
internal sealed class UpdateDraftArticleUseCase(
    IKnowledgeWriter writer,
    ICurrentUser currentUser,
    IClock clock) : IUpdateDraftArticle
{
    public async Task<ArticleDto> ExecuteAsync(UpdateDraftArticleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await new ArticleTransitions(writer, currentUser, clock).RunAsync(
            command.Id, command.ExpectedRevision, "chỉnh sửa bài viết", "chỉnh sửa bản nháp",
            apply: (article, _, now) => article.UpdateDraft(command.Title, command.Content, command.Tags, now),
            cancellationToken: cancellationToken);
    }
}
