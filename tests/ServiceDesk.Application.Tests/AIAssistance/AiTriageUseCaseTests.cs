using Moq;
using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Application.Triage;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.AIAssistance;

public class AiTriageUseCaseTests
{
    private readonly Mock<ITicketTriageBridge> _ticketBridge = new();
    private readonly Mock<ITicketClassificationService> _classificationService = new();
    private readonly Mock<IAiAnalysisStore> _analysisStore = new();
    private readonly Mock<IRequestAiTriage> _requestAiTriage = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();

    public AiTriageUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task RequestAiTriage_WhenTicketNotFound_ThrowsKeyNotFoundException()
    {
        var ticketId = Guid.NewGuid();
        _ticketBridge.Setup(b => b.GetSnapshotAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketTriageSnapshotDto?)null);

        var useCase = new RequestAiTriageUseCase(_ticketBridge.Object, _classificationService.Object, _analysisStore.Object, _clock.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(ticketId));
    }

    [Fact]
    public async Task RequestAiTriage_WhenClassificationSucceeds_SavesAnalysisAndReturnsView()
    {
        var ticketId = Guid.NewGuid();
        var snapshot = new TicketTriageSnapshotDto(
            ticketId,
            1,
            "Cannot connect to VPN",
            "I am working remotely and VPN fails with timeout error 800.",
            "NET",
            null,
            null,
            null,
            TicketStatus.Submitted,
            Guid.NewGuid()
        );

        _ticketBridge.Setup(b => b.GetSnapshotAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var triageResult = TriageResult.Success(
            TicketCategory.NET,
            Impact.Medium,
            Urgency.High,
            0.95,
            0.85,
            0.90);

        _classificationService.Setup(s => s.ClassifyAsync(It.IsAny<TriageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(triageResult);

        AIAnalysis? savedAnalysis = null;
        _analysisStore.Setup(s => s.SaveAnalysisAsync(It.IsAny<AIAnalysis>(), It.IsAny<CancellationToken>()))
            .Callback<AIAnalysis, CancellationToken>((a, _) => savedAnalysis = a)
            .Returns(Task.CompletedTask);

        var useCase = new RequestAiTriageUseCase(_ticketBridge.Object, _classificationService.Object, _analysisStore.Object, _clock.Object);

        var view = await useCase.ExecuteAsync(ticketId);

        Assert.Equal(TriageOutcome.Success, view.Outcome);
        Assert.Equal(TicketCategory.NET, view.SuggestedCategory);
        Assert.Equal(Impact.Medium, view.SuggestedImpact);
        Assert.Equal(Urgency.High, view.SuggestedUrgency);

        Assert.NotNull(savedAnalysis);
        Assert.True(savedAnalysis.Succeeded);
        Assert.Equal(ticketId, savedAnalysis.TicketId);
        Assert.Equal(3, savedAnalysis.Suggestions.Count);
    }

    [Fact]
    public async Task DecideAiTriage_WhenAccept_AppliesSuggestedValuesToTicket()
    {
        var ticketId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var validHash = InputHashComputer.Compute("Cannot connect to VPN", "Description");

        var analysis = AIAnalysis.CreateSucceeded(ticketId, 1, validHash, _clock.Object.UtcNow);
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Category, "NET", 0.95));
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Impact, "High", 0.90));
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Urgency, "High", 0.88));

        _analysisStore.Setup(s => s.GetLatestAnalysisAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        _ticketBridge.Setup(b => b.ApplyTriageDecisionAsync(
                ticketId,
                Impact.High,
                Urgency.High,
                "NET",
                It.IsAny<string>(),
                reviewerId,
                1u,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new DecideAiTriageUseCase(_ticketBridge.Object, _analysisStore.Object, _clock.Object);

        var command = new TriageDecisionCommand(ticketId, TriageDecisionType.Accept, ExpectedRevision: 1u);
        var result = await useCase.ExecuteAsync(command, reviewerId);

        Assert.True(result.AppliedToTicket);
        Assert.Equal(TriageDecisionType.Accept, result.Decision);
    }

    [Fact]
    public async Task DecideAiTriage_WhenOverrideWithoutReason_ThrowsArgumentException()
    {
        var ticketId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var useCase = new DecideAiTriageUseCase(_ticketBridge.Object, _analysisStore.Object, _clock.Object);

        var command = new TriageDecisionCommand(
            ticketId,
            TriageDecisionType.Override,
            Impact: Impact.Low,
            Urgency: Urgency.Low,
            Reason: null);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command, reviewerId));
    }

    [Fact]
    public async Task DecideAiTriage_WhenOverrideValid_AppliesOverriddenValues()
    {
        var ticketId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var validHash = InputHashComputer.Compute("Cannot connect to VPN", "Description");

        var analysis = AIAnalysis.CreateSucceeded(ticketId, 1, validHash, _clock.Object.UtcNow);
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Category, "NET", 0.95));
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Impact, "High", 0.90));
        analysis.AddSuggestion(AISuggestion.Create(analysis.Id, SuggestionType.Urgency, "High", 0.88));

        _analysisStore.Setup(s => s.GetLatestAnalysisAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        _ticketBridge.Setup(b => b.ApplyTriageDecisionAsync(
                ticketId,
                Impact.Low,
                Urgency.Low,
                "SEC",
                It.IsAny<string>(),
                reviewerId,
                1u,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new DecideAiTriageUseCase(_ticketBridge.Object, _analysisStore.Object, _clock.Object);

        var command = new TriageDecisionCommand(
            ticketId,
            TriageDecisionType.Override,
            Impact: Impact.Low,
            Urgency: Urgency.Low,
            CategoryId: "SEC",
            Reason: "Sự cố an ninh mạng cần xử lý theo quy trình bảo mật",
            ExpectedRevision: 1u);

        var result = await useCase.ExecuteAsync(command, reviewerId);

        Assert.True(result.AppliedToTicket);
        Assert.Equal(TriageDecisionType.Override, result.Decision);
    }

    [Fact]
    public async Task GetTicketTriage_WhenTicketNotFound_ThrowsKeyNotFoundException()
    {
        var ticketId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SetupCurrentUser(actorId, RoleNames.Requester);
        _ticketBridge.Setup(b => b.GetSnapshotAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketTriageSnapshotDto?)null);

        var useCase = new GetTicketTriageUseCase(_analysisStore.Object, _requestAiTriage.Object, _ticketBridge.Object, _currentUser.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(ticketId));
    }

    [Fact]
    public async Task GetTicketTriage_WhenRequesterDoesNotOwnTicket_ThrowsUnauthorizedAccessException()
    {
        var ticketId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        SetupCurrentUser(actorId, RoleNames.Requester);
        _ticketBridge.Setup(b => b.GetSnapshotAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(ticketId, ownerId));

        var useCase = new GetTicketTriageUseCase(_analysisStore.Object, _requestAiTriage.Object, _ticketBridge.Object, _currentUser.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(ticketId));
        _analysisStore.Verify(s => s.GetLatestAnalysisAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _requestAiTriage.Verify(r => r.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTicketTriage_WhenRequesterOwnsTicketAndNoAnalysis_ReturnsUnavailableWithoutTriggeringAi()
    {
        var ticketId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        SetupCurrentUser(ownerId, RoleNames.Requester);
        _ticketBridge.Setup(b => b.GetSnapshotAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(ticketId, ownerId));
        _analysisStore.Setup(s => s.GetLatestAnalysisAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIAnalysis?)null);

        var useCase = new GetTicketTriageUseCase(_analysisStore.Object, _requestAiTriage.Object, _ticketBridge.Object, _currentUser.Object);

        var result = await useCase.ExecuteAsync(ticketId);

        Assert.Equal(TriageOutcome.Unavailable, result.Outcome);
        _requestAiTriage.Verify(r => r.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTicketTriage_WhenAgentAndNoAnalysis_TriggersAiAnalysis()
    {
        var ticketId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        SetupCurrentUser(agentId, RoleNames.Agent);
        _ticketBridge.Setup(b => b.GetSnapshotAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(ticketId, ownerId));
        _analysisStore.Setup(s => s.GetLatestAnalysisAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIAnalysis?)null);
        var expected = new AgentTriageView { Outcome = TriageOutcome.Success, SuggestedCategory = TicketCategory.NET };
        _requestAiTriage.Setup(r => r.ExecuteAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var useCase = new GetTicketTriageUseCase(_analysisStore.Object, _requestAiTriage.Object, _ticketBridge.Object, _currentUser.Object);

        var result = await useCase.ExecuteAsync(ticketId);

        Assert.Same(expected, result);
        _requestAiTriage.Verify(r => r.ExecuteAsync(ticketId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupCurrentUser(Guid userId, string role)
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId.ToString("D"));
        _currentUser.SetupGet(u => u.Roles).Returns([role]);
        _currentUser.Setup(u => u.IsInRole(It.IsAny<string>()))
            .Returns((string r) => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    private static TicketTriageSnapshotDto CreateSnapshot(Guid ticketId, Guid requesterId) =>
        new(ticketId, 1, "VPN down", "Cannot connect", "NET", null, null, null, TicketStatus.Submitted, requesterId);
}
