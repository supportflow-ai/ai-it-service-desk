using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Knowledge.Application.Authorization;

/// <summary>
/// Quy tắc quyền của Knowledge ở một chỗ: ai được soạn/xuất bản/lưu trữ bài viết và mỗi vai trò đọc được phạm vi nào.
/// Hành vi giống hệt các phép kiểm trước đây trong từng use case.
/// </summary>
internal static class KnowledgePermissions
{
    /// <summary>Agent hoặc Admin: được tạo, sửa, xuất bản, lưu trữ bài viết.</summary>
    public static bool IsStaff(ICurrentUser currentUser) =>
        currentUser.IsInRole(RoleNames.Agent) || currentUser.IsInRole(RoleNames.Admin);

    /// <summary>Tác giả của bài viết hoặc Admin: được thay đổi bài viết đó.</summary>
    public static bool CanManage(Article article, Guid callerId, ICurrentUser currentUser) =>
        currentUser.IsInRole(RoleNames.Admin) || article.IsAuthor(callerId);

    /// <summary>
    /// Phạm vi đọc: Admin đọc mọi bài; Agent đọc bài đã xuất bản và bài của mình; người khác chỉ đọc bài đã xuất bản.
    /// </summary>
    public static KnowledgeReadScope ReadScope(ICurrentUser currentUser)
    {
        if (currentUser.IsInRole(RoleNames.Admin)) return new KnowledgeReadScope.Staff();
        if (currentUser.IsInRole(RoleNames.Agent)) return new KnowledgeReadScope.Author(AuthenticatedActor.RequireId(currentUser));
        return new KnowledgeReadScope.Public();
    }
}
