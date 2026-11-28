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
    private readonly Mock<IAiAnalysisStore> _analysisStore = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public AiTriageUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_actorId.ToString("D"));
    }

    private RequestAiTriageUseCase Request() => new(_ticketBridge.Object, _classificationService.Object, _analysisStore.Object, _clock.Object);
    private GetTicketTriageUseCase Get() => new(_analysisStore.Object, _ticketBridge.Object);
    private DecideAiTriageUseCase Decide() => new(_ticketBridge.Object, _analysisStore.Object, _currentUser.Object, _clock.Object);

    private void SetupSnapshot(bool canTriage, string title = Title, string category = TicketCategories.VPN) =>
        _ticketBridge.Setup(b => b.GetSnapshotAsync(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageSnapshotDto(_ticketId, 1, title, Description, category, Guid.NewGuid(), canTriage));

    private AIAnalysis SetupAnalysis(params (SuggestionType Type, string Value)[] suggestions)
    {
        var analysis = AIAnalysis.CreateSucceeded(_ticketId, 1, InputHashComputer.Compute(Title, Description), _clock.Object.UtcNow);
        foreach (var (type, value) in suggestions) analysis.AddSuggestion(AISuggestion.Create(analysis.Id, type, value, 0.9));
        _analysisStore.Setup(s => s.GetLatestAnalysisAsync(_ticketId, It.IsAny<CancellationToken>())).ReturnsAsync(analysis);
        return analysis;
    }

    private AIAnalysis SetupFullAnalysis() =>
        SetupAnalysis((SuggestionType.Category, "NET"), (SuggestionType.Impact, "High"), (SuggestionType.Urgency, "Medium"));

    private void VerifyTicketNotWritten() =>
        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(It.IsAny<Guid>(), It.IsAny<Impact>(), It.IsAny<Urgency>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<uint?>(), It.IsAny<CancellationToken>()), Times.Never);

    private void VerifyAnalysisNotSaved() =>
        _analysisStore.Verify(s => s.SaveAnalysisAsync(It.IsAny<AIAnalysis>(), It.IsAny<CancellationToken>()), Times.Never);

    // ---------- POST ai-analyze ----------

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
        AIAnalysis? saved = null;
        _analysisStore.Setup(s => s.SaveAnalysisAsync(It.IsAny<AIAnalysis>(), It.IsAny<CancellationToken>()))
            .Callback<AIAnalysis, CancellationToken>((a, _) => saved = a).Returns(Task.CompletedTask);

        var view = await Request().ExecuteAsync(_ticketId);

        Assert.Equal(TriageOutcome.Success, view.Outcome);
        Assert.Equal(TicketCategory.NET, view.SuggestedCategory);
        Assert.NotNull(saved);
        Assert.Equal(_ticketId, saved.TicketId);
        Assert.Equal(3, saved.Suggestions.Count);
    }

    // ---------- GET triage ----------

    [Fact]
    public async Task Get_WhenTicketNotVisible_ThrowsNotFoundWithoutReadingAnalysis()
    {
        _ticketBridge.Setup(b => b.GetSnapshotAsync(_ticketId, It.IsAny<CancellationToken>())).ReturnsAsync((TicketTriageSnapshotDto?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Get().ExecuteAsync(_ticketId));
        _analysisStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_StaffWithoutAnalysis_ReturnsUnavailableAndNeverWrites()
    {
        SetupSnapshot(canTriage: true);
        _analysisStore.Setup(s => s.GetLatestAnalysisAsync(_ticketId, It.IsAny<CancellationToken>())).ReturnsAsync((AIAnalysis?)null);

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
            new TriageDecisionCommand(_ticketId, decision, Impact.Low, Urgency.Low, null, "lý do", 1u)));
        VerifyTicketNotWritten();
        VerifyAnalysisNotSaved();
        Assert.All(analysis.Suggestions, s => Assert.Equal(SuggestionDecision.Pending, s.Decision));
    }

    [Fact]
    public async Task Accept_AppliesSuggestedCategoryImpactUrgencyThenMarksSuggestions()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();

        var result = await Decide().ExecuteAsync(new TriageDecisionCommand(_ticketId, TriageDecisionType.Accept, ExpectedRevision: 1u));

        Assert.True(result.AppliedToTicket);
        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(_ticketId, Impact.High, Urgency.Medium, "NET",
            It.IsAny<string>(), 1u, It.IsAny<CancellationToken>()), Times.Once);
        Assert.All(analysis.Suggestions, s => Assert.Equal(SuggestionDecision.Accepted, s.Decision));
        Assert.All(analysis.Suggestions, s => Assert.Equal(_actorId.ToString(), s.ReviewedBy));
        _analysisStore.Verify(s => s.SaveAnalysisAsync(analysis, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Accept_WithoutCategorySuggestion_KeepsTicketCategory()
    {
        SetupSnapshot(canTriage: true);
        SetupAnalysis((SuggestionType.Impact, "Low"), (SuggestionType.Urgency, "Low"));

        await Decide().ExecuteAsync(new TriageDecisionCommand(_ticketId, TriageDecisionType.Accept, ExpectedRevision: 1u));

        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(_ticketId, Impact.Low, Urgency.Low, null,
            It.IsAny<string>(), 1u, It.IsAny<CancellationToken>()), Times.Once);
    }

    public static TheoryData<string> InvalidAcceptStates => new()
    {
        "none", "failed", "empty", "partial", "stale", "decided"
    };

    [Theory]
    [MemberData(nameof(InvalidAcceptStates))]
    public async Task Accept_WithoutValidPendingAnalysis_IsConflictWithoutDefaultValues(string state)
    {
        SetupSnapshot(canTriage: true, title: state == "stale" ? "Tiêu đề đã sửa" : Title);
        switch (state)
        {
            case "none":
                _analysisStore.Setup(s => s.GetLatestAnalysisAsync(_ticketId, It.IsAny<CancellationToken>())).ReturnsAsync((AIAnalysis?)null);
                break;
            case "failed":
                _analysisStore.Setup(s => s.GetLatestAnalysisAsync(_ticketId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(AIAnalysis.CreateFailed(_ticketId, 1, InputHashComputer.Compute(Title, Description), _clock.Object.UtcNow, "timeout"));
                break;
            case "empty": SetupAnalysis(); break;
            case "partial": SetupAnalysis((SuggestionType.Category, "NET"), (SuggestionType.Impact, "High")); break;
            case "stale": SetupFullAnalysis(); break;
            case "decided": SetupFullAnalysis().Suggestions[0].Reject("someone", _clock.Object.UtcNow); break;
        }

        await Assert.ThrowsAsync<TriageDecisionConflictException>(() =>
            Decide().ExecuteAsync(new TriageDecisionCommand(_ticketId, TriageDecisionType.Accept, ExpectedRevision: 1u)));
        VerifyTicketNotWritten();
        VerifyAnalysisNotSaved();
    }

    [Fact]
    public async Task Accept_WhenTicketWriteFails_LeavesSuggestionsPending()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();
        _ticketBridge.Setup(b => b.ApplyTriageDecisionAsync(It.IsAny<Guid>(), It.IsAny<Impact>(), It.IsAny<Urgency>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<uint?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            Decide().ExecuteAsync(new TriageDecisionCommand(_ticketId, TriageDecisionType.Accept, ExpectedRevision: 1u)));
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
            _ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, "SEC", "Sự cố an ninh", 1u));

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
            _ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, null, "Giữ danh mục", 1u));

        Assert.Equal(TicketCategories.VPN, analysis.Suggestions.Single(s => s.Type == SuggestionType.Category).FinalValue);
    }

    [Fact]
    public async Task Override_WithoutCategoryOnUnknownStoredCategory_DoesNotFailAfterTicketWrite()
    {
        SetupSnapshot(canTriage: true, category: "LEGACY");
        var analysis = SetupFullAnalysis();

        var result = await Decide().ExecuteAsync(new TriageDecisionCommand(
            _ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, null, "Giữ danh mục", 1u));

        Assert.True(result.AppliedToTicket);
        Assert.Equal(SuggestionDecision.Rejected, analysis.Suggestions.Single(s => s.Type == SuggestionType.Category).Decision);
        _analysisStore.Verify(s => s.SaveAnalysisAsync(analysis, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Override_WithoutAnalysis_StillAppliesAsManualFallback()
    {
        SetupSnapshot(canTriage: true);
        _analysisStore.Setup(s => s.GetLatestAnalysisAsync(_ticketId, It.IsAny<CancellationToken>())).ReturnsAsync((AIAnalysis?)null);

        await Decide().ExecuteAsync(new TriageDecisionCommand(
            _ticketId, TriageDecisionType.Override, Impact.Low, Urgency.Low, null, "AI không khả dụng", 1u));

        _ticketBridge.Verify(b => b.ApplyTriageDecisionAsync(_ticketId, Impact.Low, Urgency.Low, null,
            It.IsAny<string>(), 1u, It.IsAny<CancellationToken>()), Times.Once);
        VerifyAnalysisNotSaved();
    }

    [Fact]
    public async Task Reject_RecordsDecisionWithoutTouchingTicket()
    {
        SetupSnapshot(canTriage: true);
        var analysis = SetupFullAnalysis();

        var result = await Decide().ExecuteAsync(new TriageDecisionCommand(_ticketId, TriageDecisionType.Reject, Reason: "Sai danh mục"));

        Assert.False(result.AppliedToTicket);
        Assert.All(analysis.Suggestions, s => Assert.Equal(SuggestionDecision.Rejected, s.Decision));
        VerifyTicketNotWritten();
    }

    [Fact]
    public async Task Reject_WithoutAnalysis_IsConflictNotFalseSuccess()
    {
        SetupSnapshot(canTriage: true);
        _analysisStore.Setup(s => s.GetLatestAnalysisAsync(_ticketId, It.IsAny<CancellationToken>())).ReturnsAsync((AIAnalysis?)null);

        await Assert.ThrowsAsync<TriageDecisionConflictException>(() =>
            Decide().ExecuteAsync(new TriageDecisionCommand(_ticketId, TriageDecisionType.Reject, Reason: "Sai")));
        VerifyAnalysisNotSaved();
    }
}
