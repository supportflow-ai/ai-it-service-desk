using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Knowledge.Domain;

/// <summary>
/// Danh mục lỗi nghiệp vụ lặp lại của Knowledge: một mã ổn định và một thông điệp tiếng Việt cho mỗi tình huống.
/// Vi phạm quy tắc trạng thái bài viết giữ mã HTTP 422 theo hợp đồng hiện hành của Knowledge (Ticketing dùng 400).
/// </summary>
internal static class KnowledgeErrors
{
    /// <summary>Bài viết không tồn tại hoặc người gọi không được biết (404 không body).</summary>
    public static NotFoundException NotFound() => new("article_not_found", "Không tìm thấy bài viết.");

    /// <summary>Thao tác chỉ dành cho Agent/Admin. <paramref name="action"/> là cụm động từ, ví dụ "xuất bản bài viết".</summary>
    public static ForbiddenException StaffRequired(string action) =>
        new("knowledge_staff_required", $"Chỉ kỹ thuật viên hoặc quản trị viên mới có thể {action}.");

    /// <summary>Thao tác chỉ dành cho tác giả hoặc Admin.</summary>
    public static ForbiddenException AuthorRequired(string action) =>
        new("knowledge_author_required", $"Chỉ tác giả hoặc quản trị viên mới có quyền {action}.");

    public static InvalidInputException ExpectedRevisionRequired() =>
        new("expected_revision_required", "Cần cung cấp expectedRevision khác 0.");

    public static InvalidInputException PagingInvalid() =>
        new("paging_invalid", "page quá lớn so với pageSize.");

    /// <summary>Vi phạm quy tắc trạng thái của bài viết (422).</summary>
    public static RuleViolationException StateViolation(string code, string message) =>
        new(code, message, RuleViolationException.UnprocessableEntityStatus);
}
