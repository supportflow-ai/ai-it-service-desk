namespace ServiceDesk.Modules.AIAssistance.Domain;

/// <summary>
/// Đại diện cho một phiên phân tích AI trên một snapshot của ticket.
/// Lưu trữ revision của ticket và mã băm inputHash tại thời điểm phân tích
/// để phục vụ cơ chế bảo vệ chống áp dụng gợi ý cũ (stale-suggestion protection).
/// </summary>
public class AIAnalysis
{
    public Guid Id { get; private set; }

    /// <summary>Mã ticket được phân tích.</summary>
    public Guid TicketId { get; private set; }

    /// <summary>
    /// Số revision của ticket tại thời điểm phân tích.
    /// Dùng để phát hiện dữ liệu cũ: nếu revision hiện tại khác revision này, gợi ý bị coi là stale.
    /// </summary>
    public long TicketRevision { get; private set; }

    /// <summary>
    /// Mã hash tất định của nội dung ticket (Title + Description) tại thời điểm phân tích.
    /// Dùng kiểm tra phụ trợ phát hiện thay đổi nội dung.
    /// </summary>
    public string InputHash { get; private set; } = string.Empty;

    /// <summary>Thời điểm tạo phiên phân tích.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Cho biết provider AI có trả về kết quả phân loại thành công hay không.</summary>
    public bool Succeeded { get; private set; }

    public TriageOutcome Outcome { get; private set; }

    public string[] ValidationErrors { get; private set; } = [];

    /// <summary>Lý do thất bại nếu Succeeded = false (dùng mã lỗi có kiểm soát, không leak raw exception).</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Quyết định của người duyệt; <see cref="SuggestionDecision.Pending"/> là chưa quyết định.</summary>
    public SuggestionDecision Decision { get; private set; } = SuggestionDecision.Pending;

    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? DecisionReason { get; private set; }

    public long? DecisionRevision { get; private set; }

    /// <summary>Phân loại của phiếu ngay trước quyết định. Null khi quyết định không ghi phiếu (Reject).</summary>
    public TriageValues? Previous { get; private set; }

    /// <summary>Phân loại đã ghi vào phiếu. Null khi quyết định không ghi phiếu (Reject).</summary>
    public TriageValues? Applied { get; private set; }

    /// <summary>Danh sách các gợi ý trường thông tin do phiên phân tích này tạo ra.</summary>
    public IReadOnlyList<AISuggestion> Suggestions => _suggestions.AsReadOnly();
    private readonly List<AISuggestion> _suggestions = [];

    private AIAnalysis() { } // Dành cho EF Core

    /// <summary>
    /// Tạo mới một phiên phân tích AI thành công.
    /// </summary>
    public static AIAnalysis CreateSucceeded(
        Guid ticketId,
        long ticketRevision,
        string inputHash,
        DateTimeOffset createdAt)
    {
        ValidateSnapshot(ticketId, ticketRevision, inputHash);
        return new AIAnalysis
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TicketRevision = ticketRevision,
            InputHash = inputHash ?? throw new ArgumentNullException(nameof(inputHash)),
            CreatedAt = createdAt,
            Succeeded = true
        };
    }

    /// <summary>
    /// Tạo mới một bản ghi phân tích thất bại / degraded.
    /// </summary>
    public static AIAnalysis CreateFailed(
        Guid ticketId,
        long ticketRevision,
        string inputHash,
        DateTimeOffset createdAt,
        string failureReason,
        TriageOutcome outcome = TriageOutcome.Unavailable,
        IReadOnlyList<string>? validationErrors = null)
    {
        ValidateSnapshot(ticketId, ticketRevision, inputHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);
        if (!Enum.IsDefined(outcome) || outcome == TriageOutcome.Success) throw new ArgumentOutOfRangeException(nameof(outcome));
        return new AIAnalysis
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TicketRevision = ticketRevision,
            InputHash = inputHash ?? throw new ArgumentNullException(nameof(inputHash)),
            CreatedAt = createdAt,
            Succeeded = false,
            Outcome = outcome,
            ValidationErrors = validationErrors?.ToArray() ?? [],
            FailureReason = failureReason ?? throw new ArgumentNullException(nameof(failureReason))
        };
    }

    /// <summary>
    /// Thêm gợi ý vào phiên phân tích (chỉ áp dụng cho phiên thành công, mỗi trường chỉ có tối đa 1 gợi ý).
    /// </summary>
    public void AddSuggestion(AISuggestion suggestion)
    {
        if (!Succeeded)
            throw new InvalidOperationException("Cannot add suggestions to a failed analysis.");

        ArgumentNullException.ThrowIfNull(suggestion);
        if (suggestion.AnalysisId != Id)
            throw new ArgumentException("Suggestion belongs to a different analysis.", nameof(suggestion));
        if (_suggestions.Any(existing => existing.Id == suggestion.Id || existing.Type == suggestion.Type))
            throw new ArgumentException("Only one suggestion per field is allowed.", nameof(suggestion));
        _suggestions.Add(suggestion);
    }

    /// <summary>
    /// Ghi quyết định HITL đúng một lần và đánh dấu từng gợi ý. Accept/Override phải kèm giá trị trước và sau
    /// khi ghi phiếu; Reject không có giá trị nào vì phiếu không đổi.
    /// </summary>
    public void RecordDecision(SuggestionDecision decision, Guid reviewerId, DateTimeOffset decidedAt,
        string? reason = null, TriageValues? previous = null, TriageValues? applied = null, long? expectedRevision = null)
    {
        if (!Succeeded) throw new InvalidOperationException("Cannot decide a failed analysis.");
        if (Decision != SuggestionDecision.Pending) throw new InvalidOperationException("Analysis already has a decision.");
        if (reviewerId == Guid.Empty) throw new ArgumentException("Reviewer identity is required.", nameof(reviewerId));
        var writesTicket = decision is SuggestionDecision.Accepted or SuggestionDecision.Overridden;
        if (!writesTicket && decision != SuggestionDecision.Rejected)
            throw new ArgumentOutOfRangeException(nameof(decision));
        if (writesTicket ? previous is null || applied is null : previous is not null || applied is not null)
            throw new ArgumentException("Ticket values must be present exactly when the decision writes the ticket.");
        if (decision != SuggestionDecision.Accepted && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required.", nameof(reason));

        var reviewer = reviewerId.ToString();
        foreach (var suggestion in _suggestions)
        {
            if (decision == SuggestionDecision.Accepted) suggestion.Accept(reviewer, decidedAt);
            else if (decision == SuggestionDecision.Rejected) suggestion.Reject(reviewer, decidedAt);
            else
            {
                var finalValue = suggestion.Type switch
                {
                    // Mã danh mục cũ ngoài enum không làm giá trị cuối được: không được lỗi sau khi phiếu đã ghi.
                    SuggestionType.Category => TriagePolicy.ParseCategory(applied!.CategoryId)?.ToString(),
                    SuggestionType.Impact => applied!.Impact,
                    _ => applied!.Urgency
                };
                if (finalValue is null) suggestion.Reject(reviewer, decidedAt);
                else suggestion.Override(finalValue, reviewer, decidedAt);
            }
        }

        Decision = decision;
        DecidedBy = reviewerId;
        DecidedAt = decidedAt;
        DecisionReason = reason?.Trim();
        DecisionRevision = expectedRevision;
        Previous = previous;
        Applied = applied;
    }

    private static void ValidateSnapshot(Guid ticketId, long revision, string inputHash)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("Ticket identity is required.", nameof(ticketId));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        ArgumentNullException.ThrowIfNull(inputHash);
        if (!InputHashComputer.IsValid(inputHash)) throw new ArgumentException("A lowercase SHA-256 input hash is required.", nameof(inputHash));
    }
}
