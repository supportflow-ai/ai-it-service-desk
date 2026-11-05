using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Indexing;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Domain.Knowledge;

namespace ServiceDesk.Application.Knowledge.RebuildArticleIndex;

public sealed record RebuildArticleIndexCommand(Guid Id, Guid RequestId, uint? ExpectedRevision);
public sealed record RebuildArticleIndexResult(Guid JobId);

public interface IRebuildArticleIndex
{
    Task<RebuildArticleIndexResult> ExecuteAsync(RebuildArticleIndexCommand command, CancellationToken ct = default);
}

internal sealed class RebuildArticleIndexUseCase(IKnowledgeWriter writer, IKnowledgeIndexStore store,
    ICurrentUser user, IClock clock) : IRebuildArticleIndex
{
    public async Task<RebuildArticleIndexResult> ExecuteAsync(RebuildArticleIndexCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!user.IsInRole(RoleNames.Agent) && !user.IsInRole(RoleNames.Admin))
            throw new UnauthorizedAccessException("Không có quyền lập chỉ mục.");
        var article = await writer.GetForUpdateAsync(command.Id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy bài viết.");
        if (!user.IsInRole(RoleNames.Admin) && !article.IsAuthor(Guid.Parse(user.UserId!)))
            throw new UnauthorizedAccessException("Chỉ tác giả hoặc quản trị viên được lập chỉ mục lại.");
        if (article.Status != ArticleStatus.Published)
            throw new InvalidOperationException("Chỉ lập chỉ mục bài đã xuất bản.");
        if (command.RequestId == Guid.Empty || command.ExpectedRevision is null or 0)
            throw new ArgumentException("RequestId và ExpectedRevision là bắt buộc.");
        return new(await store.RequestRebuildAsync(article.Id, command.RequestId,
            command.ExpectedRevision.Value, clock.UtcNow.UtcDateTime, ct));
    }
}
