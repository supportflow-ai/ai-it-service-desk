using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Knowledge.Indexing;
using ServiceDesk.Application.Knowledge.Ports;
using ServiceDesk.Domain.Identity;

namespace ServiceDesk.Application.Knowledge.GetArticleIndexStatus;

public sealed record GetArticleIndexStatusQuery(Guid Id);
public interface IGetArticleIndexStatus
{
    Task<KnowledgeIndexStatusDto> ExecuteAsync(GetArticleIndexStatusQuery query, CancellationToken ct = default);
}

internal sealed class GetArticleIndexStatusUseCase(IKnowledgeWriter writer, IKnowledgeIndexStore store,
    ICurrentUser user) : IGetArticleIndexStatus
{
    public async Task<KnowledgeIndexStatusDto> ExecuteAsync(GetArticleIndexStatusQuery query, CancellationToken ct = default)
    {
        if (!user.IsInRole(RoleNames.Agent) && !user.IsInRole(RoleNames.Admin))
            throw new UnauthorizedAccessException("Không có quyền xem trạng thái lập chỉ mục.");
        var article = await writer.GetForUpdateAsync(query.Id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy bài viết.");
        if (!user.IsInRole(RoleNames.Admin) && !article.IsAuthor(Guid.Parse(user.UserId!)))
            throw new UnauthorizedAccessException("Chỉ tác giả hoặc quản trị viên được xem trạng thái lập chỉ mục.");
        return await store.GetStatusAsync(article.Id, ct);
    }
}
