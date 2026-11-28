using Moq;
using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Application.Triage;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.Application.Tests.AIAssistance;

/// <summary>G1-01 (quyền qua Ticketing), G1-02 (GET chỉ đọc), G1-03 (không fallback), G1-04 (Category).</summary>
public class AiTriageUseCaseTests
{
    private const string Title = "VPN down";
    private const string Description = "Cannot connect from home";

    private readonly Mock<ITicketTriageBridge> _ticketBridge = new();
    private readonly Mock<ITicketClassificationService> _classificationService = new();
    private readonly FakeAnalysisStore _store = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public AiTriageUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_actorId.ToString("D"));
        _ticketBridge.Setup(b => b.GetDecisionSnapshotAsync(It.IsAny<Guid>(), It.IsAny<uint?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, uint? _, CancellationToken ct) => _ticketBridge.Object.GetSnapshotAsync(id, ct));
        SetupApply().ReturnsAsync((Guid _, Impact impact, Urgency urgency, string? category, string _, uint? _, CancellationToken _) =>
            Change(category ?? TicketCategories.VPN, impact, urgency));
    }

    private Moq.Language.Flow.ISetup<ITicketTriageBridge, Task<TicketTriageChange>> SetupApply() =>
        _ticketBridge.Setup(b => b.ApplyTriageDecisionAsync(It.IsAny<Guid>(), It.IsAny<Impact>(), It.IsAny<Urgency>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<uint?>(), It.IsAny<CancellationToken>()));

    private static TicketTriageChange Change(string category, Impact impact, Urgency urgency, string previousCategory = TicketCategories.VPN) =>
        new(new TicketTriageValues(previousCategory, "Medium", "Medium", "P3"),
            new TicketTriageValues(category, impact.ToString(), urgency.ToString(), "P2"));

    private void VerifyTicketWrittenOnce() =>
        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(It.IsAny<Guid>(), It.IsAny<Impact>(), It.IsAny<Urgency>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<uint?>(), It.IsAny<CancellationToken>()), Times.Once);

    private RequestAiTriageUseCase Request() => new(_ticketBridge.Object, _classificationService.Object, _store, _clock.Object);
    private GetTicketTriageUseCase Get() => new(_store, _ticketBridge.Object);
    private DecideAiTriageUseCase Decide() => new(_ticketBridge.Object, _store, _currentUser.Object, _clock.Object);

    private void SetupSnapshot(bool canTriage, string title = Title, string category = TicketCategories.VPN) =>
        _ticketBridge.Setup(b => b.GetSnapshotAsync(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageSnapshotDto(_ticketId, 1, title, Description, category, Guid.NewGuid(), canTriage));

    private AIAnalysis SetupAnalysis(params (SuggestionType Type, string Value)[] suggestions)
    {
        var analysis = AIAnalysis.CreateSucceeded(_ticketId, 1, InputHashComputer.Compute(Title, Description), _clock.Object.UtcNow);
        foreach (var (type, value) in suggestions) analysis.AddSuggestion(AISuggestion.Create(analysis.Id, type, value, 0.9));
        _store.Items.Add(analysis);
        return analysis;
    }

    private AIAnalysis SetupFullAnalysis() =>
        SetupAnalysis((SuggestionType.Category, "NET"), (SuggestionType.Impact, "High"), (SuggestionType.Urgency, "Medium"));

    private void VerifyTicketNotWritten() =>
        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(It.IsAny<Guid>(), It.IsAny<Impact>(), It.IsAny<Urgency>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<uint?>(), It.IsAny<CancellationToken>()), Times.Never);

    private void VerifyAnalysisNotSaved()
    {
        Assert.Equal(0, _store.Adds);
        Assert.Equal(0, _store.Commits);
    }

    private TriageDecisionCommand Accept(AIAnalysis analysis) =>
        new(_ticketId, TriageDecisionType.Accept, ExpectedRevision: 1u, AnalysisId: analysis.Id);

    private sealed class FakeAnalysisStore : IAiAnalysisStore
    {
        public List<AIAnalysis> Items { get; } = [];
        public int Adds { get; private set; }
        public int Commits { get; private set; }

        public Task AddAsync(AIAnalysis analysis, CancellationToken cancellationToken = default)
        {
            Adds++;
            Items.Add(analysis);
            return Task.CompletedTask;
        }

        public Task<AIAnalysis?> GetLatestAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.LastOrDefault(a => a.TicketId == ticketId));

        // Chỉ ghi nhận commit khi delegate không ném lỗi; tính nguyên tử thật được kiểm trên PostgreSQL.
        public async Task<TResult> DecideAsync<TResult>(Guid ticketId, Guid analysisId, Func<AIAnalysis?, Task<TResult>> decide,
            CancellationToken cancellationToken = default)
        {
            var result = await decide(Items.SingleOrDefault(a => a.Id == analysisId && a.TicketId == ticketId));
            Commits++;
            return result;
        }
    }

    // ---------- POST ai-analyze ----------

    [Theory]
    [InlineData(TriageOutcome.Timeout)]
    [InlineData(TriageOutcome.InvalidResponse)]
    public async Task FailedExecutionKeepsOutcomeAndValidationErrorsWhenReadAgain(TriageOutcome outcome)
    {
        SetupSnapshot(canTriage: true);
        var failure = outcome == TriageOutcome.Timeout ? TriageResult.Timeout("timeout")
            : TriageResult.InvalidResponse("invalid", ["Invalid category"]);
        _classificationService.Setup(s => s.ClassifyAsync(It.IsAny<TriageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);
        var first = await Request().ExecuteAsync(_ticketId);
        var read = (await Get().ExecuteAsync(_ticketId)).StaffView!;
        Assert.Equal(first.Outcome, read.Outcome);
        Assert.Equal(first.ValidationErrors, read.ValidationErrors);
    }

    [Fact]
    public async Task Request_WhenTicketNotVisible_ThrowsNotFound()
    {
        _ticketBridge.Setup(b => b.GetSnapshotAsync(_ticketId, It.IsAny<CancellationToken>())).ReturnsAsync((TicketTriageSnapshotDto?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Request().ExecuteAsync(_ticketId));
    }

    [Fact]
    public async Task Request_WhenActorCannotTriage_ThrowsForbiddenWithoutCallingAi()
    {
        SetupSnapshot(canTriage: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Request().ExecuteAsync(_ticketId));
        _classificationService.Verify(s => s.ClassifyAsync(It.IsAny<TriageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyAnalysisNotSaved();
    }

    [Fact]
    public async Task Request_WhenClassificationSucceeds_SavesAnalysisAndReturnsView()
    {
        SetupSnapshot(canTriage: true);
        _classificationService.Setup(s => s.ClassifyAsync(It.IsAny<TriageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TriageResult.Success(TicketCategory.NET, Impact.Medium, Urgency.High, 0.95, 0.85, 0.90));
        var view = await Request().ExecuteAsync(_ticketId);

        Assert.Equal(TriageOutcome.Success, view.Outcome);
        Assert.Equal(TicketCategory.NET, view.SuggestedCategory);
        var saved = Assert.Single(_store.Items);
        Assert.Equal(saved.Id, view.AnalysisId);
        Assert.Equal("Pending", view.DecisionStatus);
        Assert.Equal(_ticketId, saved.TicketId);
        Assert.Equal(3, saved.Suggestions.Count);
    }

    // ---------- GET triage ----------

    [Fact]
    public async Task Get_WhenTicketNotVisible_ThrowsNotFoundWithoutReadingAnalysis()
    {
        _ticketBridge.Setup(b => b.GetSnapshotAsync(_ticketId, It.IsAny<CancellationToken>())).ReturnsAsync((TicketTriageSnapshotDto?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Get().ExecuteAsync(_ticketId));
    }

    [Fact]
    public async Task Get_StaffWithoutAnalysis_ReturnsUnavailableAndNeverWrites()
    {
        SetupSnapshot(canTriage: true);

        var first = await Get().ExecuteAsync(_ticketId);
        var second = await Get().ExecuteAsync(_ticketId);

        Assert.Equal(TriageOutcome.Unavailable, first.StaffView!.Outcome);
        Assert.Equal(TriageOutcome.Unavailable, second.StaffView!.Outcome);
        VerifyAnalysisNotSaved();
        VerifyTicketNotWritten();
    }

    [Fact]
    public async Task Get_StaffWithStoredAnalysis_ReturnsSuggestionsWithConfidence()
    {
        SetupSnapshot(canTriage: true);
        SetupFullAnalysis();

        var result = await Get().ExecuteAsync(_ticketId);

        Assert.Null(result.RequesterView);
        Assert.Equal(TriageOutcome.Success, result.StaffView!.Outcome);
        Assert.Equal(TicketCategory.NET, result.StaffView.SuggestedCategory);
        Assert.Equal(Impact.High, result.StaffView.SuggestedImpact);
        Assert.Equal(Urgency.Medium, result.StaffView.SuggestedUrgency);
        Assert.NotNull(result.StaffView.CategoryConfidence);
        Assert.Equal(_store.Items[0].Id, result.StaffView.AnalysisId);
        Assert.False(result.StaffView.IsStale);
    }

    [Fact]
    public async Task Get_AfterTicketContentChanged_MarksAnalysisStale()
    {
        SetupSnapshot(canTriage: true, title: "Tiêu đề đã sửa");
        SetupFullAnalysis();

        var result = await Get().ExecuteAsync(_ticketId);

        Assert.True(result.StaffView!.IsStale);
    }

    [Fact]
    public async Task Get_WithoutTriageRight_ReturnsRequesterProjectionOnly()
    {
        SetupSnapshot(canTriage: false);
        SetupFullAnalysis();

        var result = await Get().ExecuteAsync(_ticketId);

        Assert.Null(result.StaffView);
        Assert.Equal(TicketCategory.NET, result.RequesterView!.SuggestedCategory);
    }

    // ---------- POST triage/decision ----------

    [Fact]
    public async Task Decide_Anonymous_IsRejectedBeforeAnyRead()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Decide().ExecuteAsync(new TriageDecisionCommand(_ticketId, TriageDecisionType.Accept, ExpectedRevision: 1u)));
        _ticketBridge.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TriageDecisionType.Accept)]
    [InlineData(TriageDecisionType.Override)]
    [InlineData(TriageDecisionType.Reject)]
    public async Task Decide_WhenActorCannotTriage_IsForbiddenAndChangesNothing(TriageDecisionType decision)
    {
        SetupSnapshot(canTriage: false);
        var analysis = SetupFullAnalysis();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Decide().ExecuteAsync(
            new TriageDecisionCommand(_ticketId, decision, Impact.Low, Urgency.Low, null, "lý do", 1u, analysis.Id)));
        VerifyTicketNotWritten();
        VerifyAnalysisNotSaved();
        Assert.All(analysis.Suggestions, s => Assert.Equal(SuggestionDecision.Pending, s.Decision));
    }

    [Fact]
    public async Task Accept_AppliesSuggestedCategoryImpactUrgencyThenMarksSuggestions()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();

        var result = await Decide().ExecuteAsync(Accept(analysis));

        Assert.True(result.AppliedToTicket);
        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(_ticketId, Impact.High, Urgency.Medium, "NET",
            It.IsAny<string>(), 1u, It.IsAny<CancellationToken>()), Times.Once);
        Assert.All(analysis.Suggestions, s => Assert.Equal(SuggestionDecision.Accepted, s.Decision));
        Assert.All(analysis.Suggestions, s => Assert.Equal(_actorId.ToString(), s.ReviewedBy));
        Assert.Equal(SuggestionDecision.Accepted, analysis.Decision);
        Assert.Equal(_actorId, analysis.DecidedBy);
        Assert.Equal("VPN", analysis.Previous!.CategoryId);
        Assert.Equal("NET", analysis.Applied!.CategoryId);
        Assert.Equal(1, _store.Commits);
    }

    [Fact]
    public async Task Accept_WithoutCategorySuggestion_KeepsTicketCategory()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupAnalysis((SuggestionType.Impact, "Low"), (SuggestionType.Urgency, "Low"));

        await Decide().ExecuteAsync(Accept(analysis));

        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(_ticketId, Impact.Low, Urgency.Low, null,
            It.IsAny<string>(), 1u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("empty")]
    [InlineData("partial")]
    [InlineData("stale")]
    public async Task Accept_OfUndecidableAnalysis_IsConflictWithoutDefaultValues(string state)
    {
        SetupSnapshot(canTriage: true, title: state == "stale" ? "Tiêu đề đã sửa" : Title);
        AIAnalysis analysis;
        if (state == "failed")
        {
            analysis = AIAnalysis.CreateFailed(_ticketId, 1, InputHashComputer.Compute(Title, Description), _clock.Object.UtcNow, "timeout");
            _store.Items.Add(analysis);
        }
        else
        {
            analysis = state switch
            {
                "empty" => SetupAnalysis(),
                "partial" => SetupAnalysis((SuggestionType.Category, "NET"), (SuggestionType.Impact, "High")),
                _ => SetupFullAnalysis()
            };
        }

        await Assert.ThrowsAsync<TriageDecisionConflictException>(() => Decide().ExecuteAsync(Accept(analysis)));
        VerifyTicketNotWritten();
        VerifyAnalysisNotSaved();
        Assert.Equal(SuggestionDecision.Pending, analysis.Decision);
    }

    [Theory]
    [InlineData(TriageDecisionType.Accept)]
    [InlineData(TriageDecisionType.Reject)]
    public async Task AcceptOrReject_WithoutAnalysisId_IsBadRequest(TriageDecisionType decision)
    {
        SetupSnapshot(canTriage: true);
        SetupFullAnalysis();

        await Assert.ThrowsAsync<ArgumentException>(() => Decide().ExecuteAsync(
            new TriageDecisionCommand(_ticketId, decision, Reason: "lý do", ExpectedRevision: 1u)));
        VerifyTicketNotWritten();
        VerifyAnalysisNotSaved();
    }

    [Fact]
    public async Task Accept_OfAnalysisFromAnotherTicket_IsNotFound()
    {
        SetupSnapshot(canTriage: true);
        var foreign = AIAnalysis.CreateSucceeded(Guid.NewGuid(), 1, InputHashComputer.Compute(Title, Description), _clock.Object.UtcNow);
        _store.Items.Add(foreign);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Decide().ExecuteAsync(Accept(foreign)));
        VerifyTicketNotWritten();
    }

    [Fact]
    public async Task Accept_UsesReferencedAnalysisNotLatest()
    {
        SetupSnapshot(canTriage: true);
        var seen = SetupAnalysis((SuggestionType.Impact, "Low"), (SuggestionType.Urgency, "Low"));
        var newer = SetupAnalysis((SuggestionType.Impact, "High"), (SuggestionType.Urgency, "High"));

        await Decide().ExecuteAsync(Accept(seen));

        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(_ticketId, Impact.Low, Urgency.Low, null,
            It.IsAny<string>(), 1u, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(SuggestionDecision.Pending, newer.Decision);
    }

    [Fact]
    public async Task Replay_OfSameAccept_ReturnsSameResultWithoutWritingTicketTwice()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();

        var first = await Decide().ExecuteAsync(Accept(analysis));
        var replay = await Decide().ExecuteAsync(Accept(analysis));

        Assert.Equal(first, replay);
        VerifyTicketWrittenOnce();
    }

    [Theory]
    [InlineData("other-decision")]
    [InlineData("other-reviewer")]
    [InlineData("other-values")]
    [InlineData("other-reason")]
    [InlineData("other-revision")]
    public async Task SecondDecision_WithDifferentPayload_IsConflict(string difference)
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();
        var original = new TriageDecisionCommand(_ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, null, "lý do", 1u, analysis.Id);
        await Decide().ExecuteAsync(original);
        var second = difference switch
        {
            "other-decision" => Accept(analysis),
            "other-values" => original with { Impact = Impact.High },
            "other-reason" => original with { Reason = "lý do khác" },
            "other-revision" => original with { ExpectedRevision = 2u },
            _ => original
        };
        if (difference == "other-reviewer") _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString("D"));

        await Assert.ThrowsAsync<TriageDecisionConflictException>(() => Decide().ExecuteAsync(second));
        VerifyTicketWrittenOnce();
    }

    [Fact]
    public async Task Accept_WhenTicketWriteFails_LeavesAnalysisPending()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();
        SetupApply().ThrowsAsync(new ConcurrencyConflictException());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Decide().ExecuteAsync(Accept(analysis)));
        Assert.Equal(SuggestionDecision.Pending, analysis.Decision);
        Assert.All(analysis.Suggestions, s => Assert.Equal(SuggestionDecision.Pending, s.Decision));
        VerifyAnalysisNotSaved();
    }

    [Fact]
    public async Task Override_WithoutReason_IsBadRequest()
    {
        SetupSnapshot(canTriage: true);

        await Assert.ThrowsAsync<ArgumentException>(() => Decide().ExecuteAsync(
            new TriageDecisionCommand(_ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, Reason: null, ExpectedRevision: 1u)));
        VerifyTicketNotWritten();
    }

    [Fact]
    public async Task Override_AppliesReviewerValuesAndRecordsFinalValues()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();

        var result = await Decide().ExecuteAsync(new TriageDecisionCommand(
            _ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, "SEC", "Sự cố an ninh", 1u, analysis.Id));

        Assert.True(result.AppliedToTicket);
        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(_ticketId, Impact.Low, Urgency.Low, "SEC",
            It.Is<string>(r => r.Contains("Sự cố an ninh")), 1u, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("SEC", analysis.Suggestions.Single(s => s.Type == SuggestionType.Category).FinalValue);
        Assert.Equal("Low", analysis.Suggestions.Single(s => s.Type == SuggestionType.Impact).FinalValue);
    }

    [Fact]
    public async Task Override_WithoutCategory_RecordsUnchangedTicketCategory()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();

        await Decide().ExecuteAsync(new TriageDecisionCommand(
            _ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, null, "Giữ danh mục", 1u, analysis.Id));

        Assert.Equal(TicketCategories.VPN, analysis.Suggestions.Single(s => s.Type == SuggestionType.Category).FinalValue);
    }

    [Fact]
    public async Task Override_WithoutCategoryOnUnknownStoredCategory_DoesNotFailAfterTicketWrite()
    {
        SetupSnapshot(canTriage: true, category: "LEGACY");
        var analysis = SetupFullAnalysis();
        SetupApply().ReturnsAsync(Change("LEGACY", Impact.Low, Urgency.Low, previousCategory: "LEGACY"));

        var result = await Decide().ExecuteAsync(new TriageDecisionCommand(
            _ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, null, "Giữ danh mục", 1u, analysis.Id));

        Assert.True(result.AppliedToTicket);
        Assert.Equal(SuggestionDecision.Rejected, analysis.Suggestions.Single(s => s.Type == SuggestionType.Category).Decision);
        Assert.Equal(1, _store.Commits);
    }

    [Fact]
    public async Task Override_WithoutAnalysisId_IsManualFallbackAndTouchesNoAnalysis()
    {
        SetupSnapshot(canTriage: true);
        var pending = SetupFullAnalysis();

        await Decide().ExecuteAsync(new TriageDecisionCommand(
            _ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, null, "AI không khả dụng", 1u));

        VerifyTicketWrittenOnce();
        VerifyAnalysisNotSaved();
        Assert.Equal(SuggestionDecision.Pending, pending.Decision);
    }

    [Fact]
    public async Task Reject_RecordsDecisionAndReasonWithoutTouchingTicket()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();

        var result = await Decide().ExecuteAsync(new TriageDecisionCommand(
            _ticketId, TriageDecisionType.Reject, Reason: "Sai danh mục", AnalysisId: analysis.Id));

        Assert.False(result.AppliedToTicket);
        Assert.Equal(SuggestionDecision.Rejected, analysis.Decision);
        Assert.Equal("Sai danh mục", analysis.DecisionReason);
        Assert.Null(analysis.Applied);
        Assert.All(analysis.Suggestions, s => Assert.Equal(SuggestionDecision.Rejected, s.Decision));
        VerifyTicketNotWritten();
    }
}
