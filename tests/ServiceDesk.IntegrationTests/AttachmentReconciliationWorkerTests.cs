using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ServiceDesk.Modules.Ticketing.Application.ReconcileAttachments;
using ServiceDesk.Worker;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class AttachmentReconciliationWorkerTests
{
    [Fact]
    public async Task MissingRetentionIsDisabledAndDoesNotResolveAnyDependencies()
    {
        var scopes = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        using var worker = new AttachmentReconciliationWorker(scopes.Object, Options.Create(new AttachmentReconciliationOptions()), NullLogger<AttachmentReconciliationWorker>.Instance);
        Assert.Null(await worker.RunSweepAsync());
        Assert.Equal("Disabled", worker.Status);
        scopes.VerifyNoOtherCalls();
    }

[Fact]
    public void EmptyComposeRetentionBindsToDisabledInsteadOfChoosingAPolicy()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Attachments:OrphanRetentionHours"] = "" }).Build();
        var services = new ServiceCollection();
        services.AddOptions<AttachmentReconciliationOptions>().Bind(config.GetSection("Attachments"));
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetRequiredService<IOptions<AttachmentReconciliationOptions>>().Value.OrphanRetentionHours);
    }

    [Fact]
    public async Task CursorsAdvanceIndependentlyAndFailureNeverLooksLikeZeroSuccess()
    {
        var useCase = new Mock<IReconcileAttachments>();
        var commands = new List<AttachmentReconciliationCommand>();
        useCase.Setup(s => s.ExecuteAsync(It.IsAny<AttachmentReconciliationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AttachmentReconciliationCommand, CancellationToken>((command, _) => commands.Add(command))
            .ReturnsAsync(new AttachmentReconciliationResult(100, 2, 1, 0, "page-2"));
        await using var services = new ServiceCollection().AddSingleton(useCase.Object).BuildServiceProvider();
        using var worker = new AttachmentReconciliationWorker(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new AttachmentReconciliationOptions { OrphanRetentionHours = 24 }), NullLogger<AttachmentReconciliationWorker>.Instance);
        Assert.Equal(300, (await worker.RunSweepAsync())!.Examined);
        Assert.Equal("PartialFailure", worker.Status);
        Assert.All(commands, command => Assert.Null(command.ContinuationToken)); commands.Clear();
        await worker.RunSweepAsync();
        Assert.All(commands, command => Assert.Equal("page-2", command.ContinuationToken));
        useCase.Setup(s => s.ExecuteAsync(It.IsAny<AttachmentReconciliationCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("dependency unavailable"));
        await Assert.ThrowsAsync<IOException>(() => worker.RunSweepAsync());
        Assert.Equal("Failure", worker.Status);
        useCase.Setup(s => s.ExecuteAsync(It.IsAny<AttachmentReconciliationCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AttachmentReconciliationResult(0, 0, 0, 0, null));
        await worker.RunSweepAsync(); Assert.Equal("NoWork", worker.Status);
    }
}
