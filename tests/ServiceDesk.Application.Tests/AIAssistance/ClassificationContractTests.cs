using System.Text.Json;
using ServiceDesk.Application.AIAssistance.Contracts.V1;
using ServiceDesk.Application.AIAssistance.Ports;
using ServiceDesk.Application.AIAssistance.Contracts;
using ServiceDesk.Domain.AIAssistance;
using Xunit;
namespace ServiceDesk.Application.Tests.AIAssistance;

public sealed class ClassificationContractTests
{
    private static ClassificationRequest Request() => ClassificationContractMapper.ToRequest(new TriageRequest
        { TicketId = Guid.NewGuid(), TicketRevision = 1, Title = "VPN issue", Description = "Cannot connect" }, Guid.NewGuid());
    private static ClassificationResponse Response(ClassificationRequest r, string field = "category", string value = "NET", double? score = .8) =>
        new(r.RequestId, r.TicketId, r.TicketRevision, r.InputHash, [new(field, value, score)]);

    [Fact]
    public async Task FakeAdapter_UsesValidatedContractWithoutNetwork()
    {
        var request = Request();
        ITicketClassificationService fake = new FakeAdapter();
        var result = await fake.ClassifyAsync(new TriageRequest { TicketId = request.TicketId, TicketRevision = 1, Title = request.Title, Description = request.Description });
        Assert.Equal(TriageOutcome.Success, result.Outcome);
        Assert.Null(result.CategoryScore);
    }

    [Theory]
    [InlineData("category", "999")] [InlineData("category", "1")] [InlineData("category", "UNKNOWN")]
    [InlineData("impact", "Critical")] [InlineData("urgency", "0")] [InlineData("status", "Closed")]
    [InlineData("priority", "High")] [InlineData("roles", "Admin")]
    public void UnknownOrPrivilegedSuggestion_IsRejected(string field, string value)
    {
        var request = Request();
        Assert.Equal(TriageOutcome.InvalidResponse, ClassificationContractMapper.Map(request, Response(request, field, value)).Outcome);
    }

    [Theory]
    [InlineData(-.1)] [InlineData(1.1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidScore_IsRejected(double score)
    {
        var request = Request();
        Assert.Equal(TriageOutcome.InvalidResponse, ClassificationContractMapper.Map(request, Response(request, score: score)).Outcome);
    }

    [Theory]
    [InlineData("request")] [InlineData("ticket")] [InlineData("revision")] [InlineData("hash")]
    public void StaleOrUnrelatedResult_IsRejected(string field)
    {
        var request = Request(); var response = Response(request);
        response = field switch
        {
            "request" => response with { RequestId = Guid.NewGuid() },
            "ticket" => response with { TicketId = Guid.NewGuid() },
            "revision" => response with { TicketRevision = 99 },
            _ => response with { InputHash = "wrong" }
        };
        Assert.Equal(TriageOutcome.InvalidResponse, ClassificationContractMapper.Map(request, response).Outcome);
    }

    [Fact]
    public void UnexpectedActionAndMissingFields_AreRejected()
    {
        var request = Request();
        var json = JsonSerializer.Serialize(Response(request), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(TriageOutcome.Success, ClassificationContractMapper.FromJson(request, json).Outcome);
        Assert.Equal(TriageOutcome.InvalidResponse, ClassificationContractMapper.FromJson(request, json[..^1] + ",\"actions\":[\"close\"]}").Outcome);
        Assert.Equal(TriageOutcome.InvalidResponse, ClassificationContractMapper.FromJson(request, "{}").Outcome);
    }

    [Fact]
    public void SecurityCategory_RemainsManualAndDuplicateFieldsAreRejected()
    {
        var request = Request(); var response = Response(request, value: "SEC");
        Assert.True(ClassificationContractMapper.Map(request, response).RequiresManualTriage);
        Assert.Equal(TriageOutcome.InvalidResponse, ClassificationContractMapper.Map(request, response with
            { Suggestions = [response.Suggestions[0], response.Suggestions[0]] }).Outcome);
    }

    private sealed class FakeAdapter : ITicketClassificationService
    {
        public Task<TriageResult> ClassifyAsync(TriageRequest snapshot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = ClassificationContractMapper.ToRequest(snapshot, Guid.NewGuid());
            return Task.FromResult(ClassificationContractMapper.Map(request, Response(request, score: null)));
        }
    }
}
