using ServiceDesk.Modules.Knowledge.Application.Authorization;
using ServiceDesk.Modules.Knowledge.Application.Contracts;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Knowledge.Application.Common;

/// <summary>
/// Khung chung của mọi lệnh sửa/xuất bản/lưu trữ bài viết, theo cùng thứ tự bước như
/// <c>TicketTransitions</c> của Ticketing: kiểm vai trò (403) → tải bài viết (404) → kiểm tác giả/Admin (403)
/// → kiểm <c>expectedRevision</c> (400/409) → áp dụng quy tắc Domain (Domain tự ném <see cref="RuleViolationException"/> 422)
/// → lưu và dựng DTO với revision mới.
/// </summary>
internal sealed class ArticleTransitions(IKnowledgeWriter writer, ICurrentUser currentUser, IClock clock)
{
    /// <summary>Thông điệp 409 khi revision bài viết đã cũ.</summary>
    internal const string StaleRevisionMessage = "Phiên bản bài viết đã thay đổi. Vui lòng tải lại.";

    /// <param name="staffAction">Cụm động từ cho thông điệp 403 của Agent/Admin, ví dụ "xuất bản bài viết".</param>
    /// <param name="authorAction">Cụm động từ cho thông điệp 403 của tác giả/Admin, ví dụ "chỉnh sửa bản nháp".</param>
    /// <param name="apply">Thao tác Domain: (bài viết, mã người thực hiện, thời điểm hiện tại).</param>
    public async Task<ArticleDto> RunAsync(
        Guid articleId,
        uint? expectedRevision,
        string staffAction,
        string authorAction,
        Action<Article, Guid, DateTime> apply,
        CancellationToken cancellationToken)
    {
        if (!KnowledgePermissions.IsStaff(currentUser))
        {
            throw KnowledgeErrors.StaffRequired(staffAction);
        }

        var article = await writer.GetForUpdateAsync(articleId, cancellationToken)
            ?? throw KnowledgeErrors.NotFound();

        var actorId = AuthenticatedActor.RequireId(currentUser);
        if (!KnowledgePermissions.CanManage(article, actorId, currentUser))
        {
            throw KnowledgeErrors.AuthorRequired(authorAction);
        }

        ExpectedRevision.Require(expectedRevision, () => writer.GetRevision(article), StaleRevisionMessage);

        apply(article, actorId, clock.UtcNow.UtcDateTime);

        var updated = await writer.UpdateArticleAsync(article, expectedRevision, cancellationToken);
        return ArticleDtoMapper.ToDto(updated, writer.GetRevision(updated));
    }
}
