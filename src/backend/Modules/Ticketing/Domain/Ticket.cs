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

    /// <summary>Xác định phiếu có áp dụng cam kết SLA hay không.</summary>
    public bool IsSlaApplicable { get; private set; }

    /// <summary>Target phản hồi đầu tiên snapshot đang áp dụng (phút).</summary>
    public int? FirstResponseTargetMinutes { get; private set; }

    /// <summary>Thời điểm ghi nhận phản hồi đầu tiên hợp lệ (UTC).</summary>
    public DateTime? FirstRespondedAt { get; private set; }

    /// <summary>Đánh dấu đã từng vi phạm cam kết First Response (bảo toàn vĩnh viễn).</summary>
    public bool FirstResponseBreached { get; private set; }

    /// <summary>Target thời gian giải quyết snapshot đang áp dụng (phút).</summary>
    public int? ResolutionTargetMinutes { get; private set; }

    /// <summary>Tổng thời gian làm việc thực tế đã tích lũy từ các đoạn trước (đã loại trừ tạm dừng Pending và thời gian ở Resolved giữa các lần Reopen).</summary>
    public TimeSpan ResolutionAccumulatedDuration { get; private set; } = TimeSpan.Zero;

    /// <summary>Mốc thời gian bắt đầu đoạn đang chạy hiện tại (UTC). Null khi đang tạm dừng hoặc hoàn thành.</summary>
    public DateTime? ResolutionSegmentStartedAt { get; private set; }

    /// <summary>Thời điểm giải quyết phiếu gần nhất (UTC).</summary>
    public DateTime? ResolvedAt { get; private set; }

    /// <summary>Đánh dấu đã từng vi phạm cam kết Resolution (bảo toàn vĩnh viễn).</summary>
    public bool ResolutionBreached { get; private set; }

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
    public void SetTriage(Impact impact, Urgency urgency, DateTime now, string? categoryId = null)
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled)
            throw new InvalidOperationException($"Không thể phân loại vé đã ở trạng thái {Status}.");
        if (categoryId is not null && !TicketCategories.All.Contains(categoryId, StringComparer.Ordinal))
            throw new ArgumentException("Invalid category");

        if (categoryId is not null) CategoryId = categoryId;
        Impact = impact;
        Urgency = urgency;
        var newPriority = PriorityMatrix.Calculate(impact, urgency);
        if (IsSlaApplicable && Priority.HasValue && Priority.Value != newPriority)
        {
            UpdateSlaPriority(newPriority, now);
        }
        else
        {
            Priority = newPriority;
            UpdatedAt = now;
        }
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
        if (Status == TicketStatus.Closed || Status == TicketStatus.Cancelled || Status == TicketStatus.Resolved)
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
        if (AssignedToId == null)
            throw new InvalidOperationException("Ticket is not assigned.");
        if (Status != TicketStatus.Assigned)
            throw new InvalidOperationException($"Cannot unassign ticket in status {Status}. Only Assigned tickets can be unassigned.");
        AssignedToId = null;
        AssignedAt = null;
        Status = TicketStatus.Submitted;
        UpdatedAt = now;
    }

    /// <summary>
    /// Chuyển phiếu sang nhóm hỗ trợ khác (ADR-016: TEAM-05/06).
    /// </summary>
    public void TransferToTeam(Guid targetTeamId, string reason, bool targetTeamHasCurrentAssignee, DateTime now)
    {
        if (targetTeamId == Guid.Empty) throw new ArgumentException("Mã nhóm hỗ trợ đích là bắt buộc.", nameof(targetTeamId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Lý do chuyển đội là bắt buộc.", nameof(reason));

        if (Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled)
            throw new InvalidOperationException($"Không thể chuyển đội cho phiếu ở trạng thái {Status}.");

        if (SupportTeamId.HasValue && SupportTeamId.Value == targetTeamId)
            throw new InvalidOperationException("Phiếu đã thuộc nhóm hỗ trợ này.");

        SupportTeamId = targetTeamId;

        // Nếu kỹ thuật viên đang xử lý không thuộc nhóm mới, gỡ bỏ người phụ trách
        if (AssignedToId.HasValue && !targetTeamHasCurrentAssignee)
        {
            AssignedToId = null;
            AssignedAt = null;
            if (Status == TicketStatus.Assigned)
            {
                Status = TicketStatus.Submitted;
            }
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// Kỹ thuật viên tiếp nhận xử lý vé (Submitted/Assigned -> InProgress).
    /// </summary>
    public void Claim(Guid agentId, DateTime now)
    {
        if (agentId == Guid.Empty) throw new ArgumentException("Agent ID is required.");
        if (Status != TicketStatus.Submitted && Status != TicketStatus.Assigned)
        {
            throw new InvalidOperationException($"Cannot claim ticket in status {Status}");
        }

        AssignedToId = agentId;
        AssignedAt = now;
        Status = TicketStatus.InProgress;
        UpdatedAt = now;
    }

    /// <summary>
    /// Đánh dấu vé đã được giải quyết xong (InProgress hoặc PendingExternal -> Resolved).
    /// </summary>
    public void Resolve(DateTime now)
    {
        if (Status != TicketStatus.InProgress && Status != TicketStatus.PendingExternal)
        {
            throw new InvalidOperationException($"Cannot resolve ticket in status {Status}");
        }

        // Chốt đoạn đang chạy nếu còn (từ InProgress), từ PendingExternal thì segment đang null
        if (ResolutionSegmentStartedAt.HasValue)
        {
            if (now >= ResolutionSegmentStartedAt.Value)
            {
                ResolutionAccumulatedDuration += (now - ResolutionSegmentStartedAt.Value);
            }
            ResolutionSegmentStartedAt = null;
        }

        ResolvedAt = now;
        if (IsSlaApplicable && !FirstRespondedAt.HasValue)
        {
            RecordFirstResponse(now);
        }

        if (IsSlaApplicable && ResolutionTargetMinutes.HasValue)
        {
            var target = TimeSpan.FromMinutes(ResolutionTargetMinutes.Value);
            if (ResolutionAccumulatedDuration > target)
            {
                ResolutionBreached = true;
            }
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

        // Tiếp tục tích lũy ngân sách (Carry-over): bắt đầu đoạn chạy mới từ now, thời gian ở Resolved không tính
        if (IsSlaApplicable)
        {
            ResolutionSegmentStartedAt = now;
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

        if (IsSlaApplicable && ResolutionSegmentStartedAt.HasValue)
        {
            if (now >= ResolutionSegmentStartedAt.Value)
            {
                ResolutionAccumulatedDuration += (now - ResolutionSegmentStartedAt.Value);
            }
            ResolutionSegmentStartedAt = null;

            if (ResolutionTargetMinutes.HasValue && ResolutionAccumulatedDuration > TimeSpan.FromMinutes(ResolutionTargetMinutes.Value))
            {
                ResolutionBreached = true;
            }
            if (!FirstRespondedAt.HasValue && FirstResponseTargetMinutes.HasValue && (now - CreatedAt) > TimeSpan.FromMinutes(FirstResponseTargetMinutes.Value))
            {
                FirstResponseBreached = true;
            }
        }

        Status = TicketStatus.Cancelled;
        UpdatedAt = now;
    }

    /// <summary>
    /// Khởi tạo cam kết SLA cho phiếu mới tạo.
    /// </summary>
    public void InitializeSla(SlaTargetDuration targets, DateTime now)
    {
        if (IsSlaApplicable)
        {
            throw new InvalidOperationException("Ticket SLA has already been initialized.");
        }

        IsSlaApplicable = true;
        FirstResponseTargetMinutes = targets.FirstResponseMinutes;
        ResolutionTargetMinutes = targets.ResolutionMinutes;
        ResolutionAccumulatedDuration = TimeSpan.Zero;
        ResolutionSegmentStartedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Tính tổng thời gian đã trôi qua cho First Response tại thời điểm t.
    /// </summary>
    public TimeSpan GetFirstResponseElapsed(DateTime now)
    {
        if (!IsSlaApplicable) return TimeSpan.Zero;
        return FirstRespondedAt.HasValue
            ? FirstRespondedAt.Value - CreatedAt
            : (now >= CreatedAt ? now - CreatedAt : TimeSpan.Zero);
    }

    /// <summary>
    /// Tính tổng thời gian làm việc thực tế cho Resolution tại thời điểm t (đã trừ pause và thời gian ở Resolved).
    /// </summary>
    public TimeSpan GetResolutionElapsed(DateTime now)
    {
        if (!IsSlaApplicable) return TimeSpan.Zero;
        var runningSegment = ResolutionSegmentStartedAt.HasValue && now >= ResolutionSegmentStartedAt.Value
            ? now - ResolutionSegmentStartedAt.Value
            : TimeSpan.Zero;
        return ResolutionAccumulatedDuration + runningSegment;
    }

    /// <summary>
    /// Ghi nhận phản hồi đầu tiên hợp lệ của Agent/Admin.
    /// </summary>
    public void RecordFirstResponse(DateTime now)
    {
        if (!IsSlaApplicable || FirstRespondedAt.HasValue) return;

        FirstRespondedAt = now;
        if (FirstResponseTargetMinutes.HasValue)
        {
            var target = TimeSpan.FromMinutes(FirstResponseTargetMinutes.Value);
            var elapsed = now >= CreatedAt ? now - CreatedAt : TimeSpan.Zero;
            if (elapsed > target)
            {
                FirstResponseBreached = true;
            }
        }
        UpdatedAt = now;
    }

    /// <summary>
    /// Chốt đoạn đang chạy vào thời gian tích lũy và chuyển sang trạng thái tạm dừng đồng hồ Resolution.
    /// </summary>
    public void PauseResolution(DateTime now)
    {
        if (!IsSlaApplicable) return;
        if (ResolutionSegmentStartedAt.HasValue)
        {
            if (now >= ResolutionSegmentStartedAt.Value)
            {
                ResolutionAccumulatedDuration += (now - ResolutionSegmentStartedAt.Value);
            }
            ResolutionSegmentStartedAt = null;
            UpdatedAt = now;
        }
    }

    /// <summary>
    /// Tiếp tục tính giờ cho đồng hồ Resolution từ mốc now.
    /// </summary>
    public void ResumeResolution(DateTime now)
    {
        if (!IsSlaApplicable) return;
        if (!ResolutionSegmentStartedAt.HasValue)
        {
            ResolutionSegmentStartedAt = now;
            UpdatedAt = now;
        }
    }

    /// <summary>
    /// Cập nhật Target SLA khi Priority thay đổi trên phiếu đang xử lý.
    /// Đánh giá vi phạm theo target cũ trước khi áp dụng target mới.
    /// </summary>
    public void UpdateSlaPriority(Priority newPriority, DateTime now)
    {
        if (!IsSlaApplicable) return;

        var newTargets = PrioritySlaTargets.GetFor(newPriority);

        // 1. First Response: chỉ cập nhật nếu chưa phản hồi
        if (!FirstRespondedAt.HasValue && FirstResponseTargetMinutes.HasValue)
        {
            var oldFrTarget = TimeSpan.FromMinutes(FirstResponseTargetMinutes.Value);
            var frElapsed = now >= CreatedAt ? now - CreatedAt : TimeSpan.Zero;
            if (frElapsed > oldFrTarget)
            {
                FirstResponseBreached = true;
            }
            FirstResponseTargetMinutes = newTargets.FirstResponseMinutes;
            var newFrTarget = TimeSpan.FromMinutes(FirstResponseTargetMinutes.Value);
            if (frElapsed > newFrTarget)
            {
                FirstResponseBreached = true;
            }
        }

        // 2. Resolution: chỉ cập nhật nếu chưa Resolved hoặc Closed
        if (Status != TicketStatus.Resolved && Status != TicketStatus.Closed && ResolutionTargetMinutes.HasValue)
        {
            var oldResTarget = TimeSpan.FromMinutes(ResolutionTargetMinutes.Value);
            var resElapsed = GetResolutionElapsed(now);
            if (resElapsed > oldResTarget)
            {
                ResolutionBreached = true;
            }
            ResolutionTargetMinutes = newTargets.ResolutionMinutes;
            var newResTarget = TimeSpan.FromMinutes(ResolutionTargetMinutes.Value);
            if (resElapsed > newResTarget)
            {
                ResolutionBreached = true;
            }
        }

        Priority = newPriority;
        UpdatedAt = now;
    }

    /// <summary>
    /// Đánh giá trạng thái vi phạm cam kết SLA tại thời điểm hiện tại và cập nhật các cờ vi phạm nếu có.
    /// Trả về true nếu có bất kỳ cờ vi phạm nào thay đổi từ false sang true.
    /// </summary>
    public bool EvaluateSlaBreach(DateTime now)
    {
        if (!IsSlaApplicable || Status == TicketStatus.Closed || Status == TicketStatus.Cancelled)
            return false;

        var changed = false;

        // 1. First Response Clock: tiếp tục tính kể cả khi ticket Pending; vi phạm khi E > T
        if (!FirstResponseBreached && !FirstRespondedAt.HasValue && FirstResponseTargetMinutes.HasValue)
        {
            var target = TimeSpan.FromMinutes(FirstResponseTargetMinutes.Value);
            var elapsed = GetFirstResponseElapsed(now);
            if (elapsed > target)
            {
                FirstResponseBreached = true;
                changed = true;
            }
        }

        // 2. Resolution Clock: tạm dừng khi Pending; vi phạm khi E > T
        if (!ResolutionBreached && Status != TicketStatus.Resolved && Status != TicketStatus.Closed && Status != TicketStatus.Cancelled && ResolutionTargetMinutes.HasValue)
        {
            var target = TimeSpan.FromMinutes(ResolutionTargetMinutes.Value);
            var elapsed = GetResolutionElapsed(now);
            if (elapsed > target)
            {
                ResolutionBreached = true;
                changed = true;
            }
        }

        if (changed)
        {
            UpdatedAt = now;
        }

        return changed;
    }

    /// <summary>
    /// Chuyển trạng thái phiếu sang PendingUser hoặc PendingExternal (yêu cầu phiếu đang InProgress).
    /// </summary>
    public void MarkPending(TicketStatus pendingStatus, DateTime now)
    {
        if (Status != TicketStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot move ticket to {pendingStatus} from status {Status}");
        }
        if (pendingStatus != TicketStatus.PendingUser && pendingStatus != TicketStatus.PendingExternal)
        {
            throw new ArgumentException($"Invalid pending status: {pendingStatus}", nameof(pendingStatus));
        }

        PauseResolution(now);
        Status = pendingStatus;
        UpdatedAt = now;
    }

    /// <summary>
    /// Chuyển trạng thái phiếu từ PendingUser hoặc PendingExternal trở lại InProgress.
    /// </summary>
    public void ResumeFromPending(DateTime now)
    {
        if (Status != TicketStatus.PendingUser && Status != TicketStatus.PendingExternal)
        {
            throw new InvalidOperationException($"Cannot resume ticket from status {Status}");
        }

        ResumeResolution(now);
        Status = TicketStatus.InProgress;
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
