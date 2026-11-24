using Moq;
using ServiceDesk.Modules.AIAssistance.Application.Contracts;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Application.Triage;
using ServiceDesk.Modules.AIAssistance.Domain;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.Application.Tests.AIAssistance;

public class AiTriageUseCaseTests
{
    private readonly Mock<ITicketTriageBridge> _ticketBridge = new();
    private readonly Mock<ITicketClassificationService> _classificationService = new();
    private readonly Mock<IAiAnalysisStore> _analysisStore = new();
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
}
