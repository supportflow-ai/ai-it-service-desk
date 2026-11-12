using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Ticketing.Application.Contracts;

/// <summary>Phạm vi lọc của hàng đợi Agent.</summary>
public enum AgentQueueScope
{
    /// <summary>Mọi phiếu trong phạm vi quyền đọc (mặc định, kể cả khi client gửi giá trị không nhận diện được).</summary>
    All,

    /// <summary>Phiếu chưa phân công và chưa Closed/Cancelled (<c>scope=unassigned</c>).</summary>
    Unassigned,

    /// <summary>Phiếu được phân công cho chính người gọi và chưa Closed/Cancelled (<c>scope=my</c>).</summary>
    Mine
}

/// <summary>
/// Bộ lọc hàng đợi Agent đã chuẩn hóa và hợp lệ. Application dựng từ giá trị thô của HTTP bằng
/// <see cref="FromRaw"/> (nơi duy nhất kiểm đầu vào); Infrastructure chỉ áp dụng bộ lọc, không tự kiểm.
/// </summary>
/// <param name="Status">Giá trị số của trạng thái; không kiểm tra đã định nghĩa hay chưa (giá trị lạ cho kết quả rỗng).</param>
/// <param name="CategoryId">Mã danh mục; rỗng hoặc toàn khoảng trắng nghĩa là không lọc.</param>
/// <param name="Scope">Phạm vi lọc.</param>
/// <param name="Priority">Mức ưu tiên cần lọc; <c>null</c> nghĩa là không lọc.</param>
/// <param name="SortByPriority">Sắp theo ưu tiên (P1 trước) rồi mới theo thời gian tạo.</param>
public sealed record AgentQueueFilter(
    int? Status = null,
    string? CategoryId = null,
    AgentQueueScope Scope = AgentQueueScope.All,
    Priority? Priority = null,
    bool SortByPriority = false)
{
    /// <summary>
    /// Dựng bộ lọc từ giá trị thô của HTTP, giữ đúng hành vi cũ:
    /// <c>scope</c> không phân biệt hoa thường, chỉ nhận "unassigned" và "my", mọi giá trị khác (kể cả rỗng) nghĩa là không lọc;
    /// <c>priority</c> rỗng hoặc toàn khoảng trắng nghĩa là không lọc, còn lại phân tích như <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/>
    /// (không phân biệt hoa thường) và từ chối bằng <see cref="InvalidInputException"/> nếu không đọc được.
    /// </summary>
    /// <exception cref="InvalidInputException"><paramref name="priority"/> không phải một mức ưu tiên (P1–P4).</exception>
    public static AgentQueueFilter FromRaw(int? status, string? categoryId, string? scope, string? priority, bool sortByPriority)
    {
        Priority? parsedPriority = null;
        if (!string.IsNullOrWhiteSpace(priority))
        {
            if (!Enum.TryParse<Priority>(priority, true, out var value))
                throw new InvalidInputException("priority_invalid", "priority phải là một trong P1, P2, P3, P4.");
            parsedPriority = value;
        }

        var parsedScope = string.Equals(scope, "unassigned", StringComparison.OrdinalIgnoreCase) ? AgentQueueScope.Unassigned
            : string.Equals(scope, "my", StringComparison.OrdinalIgnoreCase) ? AgentQueueScope.Mine
            : AgentQueueScope.All;

        return new AgentQueueFilter(status, categoryId, parsedScope, parsedPriority, sortByPriority);
    }
}
