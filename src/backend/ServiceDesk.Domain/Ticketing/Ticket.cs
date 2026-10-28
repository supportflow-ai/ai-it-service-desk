namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Thực thể gốc (Aggregate Root) đại diện cho một yêu cầu hỗ trợ IT (Ticket).
/// Đảm bảo tính toàn vẹn dữ liệu và kiểm soát quy trình chuyển trạng thái theo State Machine.
/// </summary>
public class Ticket
{
    private Ticket()
    {
    }

    /// <summary>Mã định danh duy nhất (GUID) của ticket.</summary>
    public Guid Id { get; private set; }

    /// <summary>Mã số vé hiển thị định dạng IT-YYYY-NNNN (do sequence database tự động sinh).</summary>
    public string TicketNumber { get; private set; } = null!;

    /// <summary>Mã định danh người gửi yêu cầu (Requester).</summary>
    public Guid RequesterId { get; private set; }

    /// <summary>Mã phòng ban của người gửi tại thời điểm tạo vé (snapshot cố định).</summary>
    public Guid? RequesterDepartmentId { get; private set; }

    /// <summary>Mã định danh kỹ thuật viên được phân công xử lý vé (Assignee).</summary>
    public Guid? AssignedToId { get; private set; }

    /// <summary>Thời điểm vé được phân công cho kỹ thuật viên (UTC).</summary>
    public DateTime? AssignedAt { get; private set; }

    /// <summary>Tiêu đề tóm tắt sự cố (tối đa 100 ký tự).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Mô tả chi tiết sự cố (tối thiểu 10 ký tự).</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Mã danh mục sự cố (ACC, NET, VPN, SW, HW...).</summary>
    public string CategoryId { get; private set; } = string.Empty;

    /// <summary>Trạng thái hiện tại của vé trong vòng đời xử lý.</summary>
    public TicketStatus Status { get; private set; }

    /// <summary>Thời điểm tạo vé (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Thời điểm cập nhật vé gần nhất (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    // Cột hệ thống xmin của PostgreSQL đóng vai trò concurrency token - EF Core tự động xử lý

    /// <summary>
    /// Tạo mới một ticket với trạng thái khởi tạo mặc định là Submitted.
    /// </summary>
    public static Ticket Create(
        Guid requesterId,
        string title,
        string description,
        string categoryId,
        DateTime createdAt,
        Guid? requesterDepartmentId = null)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException("Requester ID is required");
        }

        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new ArgumentException("Title is required");
        }

        if (normalizedTitle.Length > 100)
        {
            throw new ArgumentException("Title must not exceed 100 characters");
        }

        var normalizedDescription = description?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            throw new ArgumentException("Description is required");
        }

        if (normalizedDescription.Length < 10)
        {
            throw new ArgumentException("Description must be at least 10 characters");
        }

        if (!TicketCategories.All.Contains(categoryId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Invalid category");
        }

        return new Ticket
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            RequesterDepartmentId = requesterDepartmentId,
            Title = normalizedTitle,
            Description = normalizedDescription,
            CategoryId = categoryId,
            Status = TicketStatus.Submitted,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    /// <summary>Kiểm tra quyền sở hữu của người gửi đối với ticket này.</summary>
    public bool IsOwnedBy(Guid requesterId) =>
        requesterId != Guid.Empty && RequesterId == requesterId;

    /// <summary>
    /// Phân công kỹ thuật viên xử lý vé.
    /// </summary>
    public void Assign(Guid agentId, DateTime now)
    {
        if (agentId == Guid.Empty) throw new ArgumentException("Agent ID is required.");
        if (Status == TicketStatus.Closed || Status == TicketStatus.Cancelled)
            throw new InvalidOperationException($"Cannot assign ticket in status {Status}");
        AssignedToId = agentId;
        AssignedAt = now;
        if (Status == TicketStatus.Submitted) Status = TicketStatus.Assigned;
        UpdatedAt = now;
    }

    /// <summary>
    /// Hủy phân công kỹ thuật viên xử lý vé.
    /// </summary>
    public void Unassign(DateTime now)
    {
        if (Status == TicketStatus.Closed || Status == TicketStatus.Cancelled)
            throw new InvalidOperationException($"Cannot unassign ticket in status {Status}");
        AssignedToId = null;
        AssignedAt = null;
        if (Status == TicketStatus.Assigned) Status = TicketStatus.Submitted;
        UpdatedAt = now;
    }

    /// <summary>
    /// Kỹ thuật viên tiếp nhận xử lý vé (Submitted/Assigned -> InProgress).
    /// </summary>
    public void Claim(DateTime now)
    {
        if (Status != TicketStatus.Submitted && Status != TicketStatus.Assigned)
        {
            throw new InvalidOperationException($"Cannot claim ticket in status {Status}");
        }

        Status = TicketStatus.InProgress;
        UpdatedAt = now;
    }

    /// <summary>
    /// Đánh dấu vé đã được giải quyết xong (InProgress -> Resolved).
    /// </summary>
    public void Resolve(DateTime now)
    {
        if (Status != TicketStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot resolve ticket in status {Status}");
        }

        Status = TicketStatus.Resolved;
        UpdatedAt = now;
    }

    /// <summary>
    /// Người dùng mở lại vé nếu chưa hài lòng với kết quả (Resolved -> InProgress).
    /// </summary>
    public void Reopen(DateTime now)
    {
        if (Status != TicketStatus.Resolved)
        {
            throw new InvalidOperationException($"Cannot reopen ticket in status {Status}");
        }

        Status = TicketStatus.InProgress;
        UpdatedAt = now;
    }

    /// <summary>
    /// Đóng vé hoàn tất sau khi nghiệm thu (Resolved -> Closed).
    /// </summary>
    public void Close(DateTime now)
    {
        if (Status != TicketStatus.Resolved)
        {
            throw new InvalidOperationException($"Cannot close ticket in status {Status}");
        }

        Status = TicketStatus.Closed;
        UpdatedAt = now;
    }

    /// <summary>
    /// Hủy vé khi còn ở trạng thái chờ tiếp nhận (Submitted -> Cancelled).
    /// </summary>
    public void Cancel(DateTime now)
    {
        if (Status != TicketStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot cancel ticket in status {Status}");
        }

        Status = TicketStatus.Cancelled;
        UpdatedAt = now;
    }

    /// <summary>
    /// Cập nhật thông tin vé (chỉ cho phép khi vé ở trạng thái Submitted).
    /// </summary>
    public void Update(string title, string description, string categoryId, DateTime now)
    {
        if (Status != TicketStatus.Submitted)
        {
            throw new InvalidOperationException("Ticket can only be updated when Submitted");
        }

        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new ArgumentException("Title is required");
        }

        if (normalizedTitle.Length > 100)
        {
            throw new ArgumentException("Title must not exceed 100 characters");
        }

        var normalizedDescription = description?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            throw new ArgumentException("Description is required");
        }

        if (normalizedDescription.Length < 10)
        {
            throw new ArgumentException("Description must be at least 10 characters");
        }

        if (!TicketCategories.All.Contains(categoryId, StringComparer.Ordinal))
        {
            throw new ArgumentException("Invalid category");
        }

        Title = normalizedTitle;
        Description = normalizedDescription;
        CategoryId = categoryId;
        UpdatedAt = now;
    }
}
