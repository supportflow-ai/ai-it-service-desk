using ServiceDesk.SharedKernel.Application;
namespace ServiceDesk.Modules.Ticketing.Domain;

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

    /// <summary>
    /// Nhóm hỗ trợ đang giữ phiếu. Phiếu mới luôn vào hàng đợi phân loại thủ công (<see cref="SupportTeam.ManualTriageId"/>);
    /// null chỉ còn ở phiếu lịch sử đã Closed/Cancelled trước khi có nhóm. Độc lập với <see cref="AssignedToId"/>:
    /// phiếu có người phụ trách vẫn có thể thuộc hàng đợi phân loại. Chưa dùng để cấp quyền đọc (ADR-016).
    /// </summary>
    public Guid? SupportTeamId { get; private set; }

    /// <summary>Mã định danh kỹ thuật viên được phân công xử lý vé (Assignee).</summary>
    public Guid? AssignedToId { get; private set; }

    /// <summary>Thời điểm vé được phân công cho kỹ thuật viên (UTC).</summary>
    public DateTime? AssignedAt { get; private set; }

    /// <summary>Mức độ ảnh hưởng của sự cố đối với hoạt động của tổ chức.</summary>
    public Impact? Impact { get; private set; }

    /// <summary>Mức độ khẩn cấp về thời gian cần xử lý sự cố.</summary>
    public Urgency? Urgency { get; private set; }

    /// <summary>Mức độ ưu tiên xử lý (P1 - P4) được tính tất định từ Impact × Urgency.</summary>
    public Priority? Priority { get; private set; }

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
            throw new InvalidInputException("ticket_requester_required", "Cần có mã người gửi yêu cầu.");
        }

        // Thứ tự kiểm giữ nguyên: tiêu đề, mô tả, danh mục; quy tắc nằm ở value object để Create và Update dùng chung.
        var validTitle = TicketTitle.Parse(title);
        var validDescription = TicketDescription.Parse(description);
        var validCategory = TicketCategoryCode.Parse(categoryId);

        return new Ticket
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            RequesterDepartmentId = requesterDepartmentId,
            Title = validTitle.Value,
            Description = validDescription.Value,
            CategoryId = validCategory.Value,
            Status = TicketStatus.Submitted,
            SupportTeamId = SupportTeam.ManualTriageId,
            Impact = Domain.Impact.Medium,
            Urgency = Domain.Urgency.Medium,
            Priority = Domain.Priority.P3,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    /// <summary>
    /// Thiết lập phân loại Impact và Urgency, tự động tính Priority theo ma trận ITIL.
    /// </summary>
    public void SetTriage(Impact impact, Urgency urgency, DateTime now)
    {
        Impact = impact;
        Urgency = urgency;
        Priority = PriorityMatrix.Calculate(impact, urgency);
        UpdatedAt = now;
    }

    /// <summary>Kiểm tra quyền sở hữu của người gửi đối với ticket này.</summary>
    public bool IsOwnedBy(Guid requesterId) =>
        requesterId != Guid.Empty && RequesterId == requesterId;

    /// <summary>
    /// Phân công kỹ thuật viên xử lý vé.
    /// </summary>
    public void Assign(Guid agentId, DateTime now)
    {
        if (agentId == Guid.Empty) throw new InvalidInputException("ticket_agent_required", "Cần có mã kỹ thuật viên.");
        if (Status == TicketStatus.Closed || Status == TicketStatus.Cancelled)
            throw new RuleViolationException("ticket_cannot_assign", $"Không thể phân công phiếu ở trạng thái {Status}.");
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
            throw new RuleViolationException("ticket_cannot_unassign", $"Không thể hủy phân công phiếu ở trạng thái {Status}.");
        if (AssignedToId == null)
            throw new RuleViolationException("ticket_not_assigned", "Phiếu chưa được phân công.");
        if (Status != TicketStatus.Assigned)
            throw new RuleViolationException("ticket_cannot_unassign", $"Không thể hủy phân công phiếu ở trạng thái {Status}. Chỉ phiếu ở trạng thái Assigned mới được hủy phân công.");
        AssignedToId = null;
        AssignedAt = null;
        Status = TicketStatus.Submitted;
        UpdatedAt = now;
    }

    /// <summary>
    /// Kỹ thuật viên tiếp nhận xử lý vé (Submitted/Assigned -> InProgress).
    /// </summary>
    public void Claim(Guid agentId, DateTime now)
    {
        if (agentId == Guid.Empty) throw new InvalidInputException("ticket_agent_required", "Cần có mã kỹ thuật viên.");
        if (Status != TicketStatus.Submitted && Status != TicketStatus.Assigned)
        {
            throw new RuleViolationException("ticket_cannot_claim", $"Không thể nhận xử lý phiếu ở trạng thái {Status}.");
        }

        AssignedToId = agentId;
        AssignedAt = now;
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
            throw new RuleViolationException("ticket_cannot_resolve", $"Không thể giải quyết phiếu ở trạng thái {Status}.");
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
            throw new RuleViolationException("ticket_cannot_reopen", $"Không thể mở lại phiếu ở trạng thái {Status}.");
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
            throw new RuleViolationException("ticket_cannot_close", $"Không thể đóng phiếu ở trạng thái {Status}.");
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
            throw new RuleViolationException("ticket_cannot_cancel", $"Không thể hủy phiếu ở trạng thái {Status}.");
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
            throw new RuleViolationException("ticket_update_requires_submitted", "Chỉ có thể cập nhật phiếu khi đang ở trạng thái Submitted.");
        }

        // Thứ tự kiểm giữ nguyên: tiêu đề, mô tả, danh mục; quy tắc nằm ở value object để Create và Update dùng chung.
        var validTitle = TicketTitle.Parse(title);
        var validDescription = TicketDescription.Parse(description);
        var validCategory = TicketCategoryCode.Parse(categoryId);

        Title = validTitle.Value;
        Description = validDescription.Value;
        CategoryId = validCategory.Value;
        UpdatedAt = now;
    }
}
