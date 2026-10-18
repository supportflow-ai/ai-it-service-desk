using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceDesk.Application.AIAssistance.Contracts;
using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.AIAssistance.Contracts.V1;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClassificationRequest(Guid RequestId, Guid TicketId, long TicketRevision, string InputHash, string Title, string Description);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClassificationSuggestion(string Field, string Value, double? Score);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClassificationResponse(Guid RequestId, Guid TicketId, long TicketRevision, string InputHash,
    IReadOnlyList<ClassificationSuggestion> Suggestions);

public static class ClassificationContractMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true
    };

    public static ClassificationRequest ToRequest(TriageRequest request, Guid requestId)
    {
        if (requestId == Guid.Empty || request.TicketId == Guid.Empty || request.TicketRevision < 0 ||
            string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            throw new ArgumentException("A valid authorized ticket snapshot is required.");
        return new(requestId, request.TicketId, request.TicketRevision,
            InputHashComputer.Compute(request.Title, request.Description), request.Title, request.Description);
    }

    public static TriageResult FromJson(ClassificationRequest request, string json)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ClassificationResponse>(json, JsonOptions);
            return response is null ? Invalid() : Map(request, response);
        }
        catch (JsonException) { return Invalid(); }
    }

    public static TriageResult Map(ClassificationRequest request, ClassificationResponse response)
    {
        if (response.RequestId != request.RequestId || response.TicketId != request.TicketId ||
            response.TicketRevision != request.TicketRevision || response.InputHash != request.InputHash ||
            response.Suggestions is null || response.Suggestions.Count > 3) return Invalid();
        TicketCategory? category = null; Impact? impact = null; Urgency? urgency = null;
        double? categoryScore = null, impactScore = null, urgencyScore = null;
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suggestion in response.Suggestions)
        {
            if (suggestion is null || suggestion.Field is null || !fields.Add(suggestion.Field) ||
                (suggestion.Score.HasValue && !ConfidencePolicy.IsValidScore(suggestion.Score.Value))) return Invalid();
            switch (suggestion.Field)
            {
                case "category" when IsName<TicketCategory>(suggestion.Value):
                    category = Enum.Parse<TicketCategory>(suggestion.Value); categoryScore = suggestion.Score; break;
                case "impact" when IsName<Impact>(suggestion.Value):
                    impact = Enum.Parse<Impact>(suggestion.Value); impactScore = suggestion.Score; break;
                case "urgency" when IsName<Urgency>(suggestion.Value):
                    urgency = Enum.Parse<Urgency>(suggestion.Value); urgencyScore = suggestion.Score; break;
                default: return Invalid();
            }
        }
        return TriageResult.Success(category, impact, urgency, categoryScore, impactScore, urgencyScore);
    }

    private static bool IsName<T>(string? value) where T : struct, Enum =>
        value is not null && Enum.GetNames<T>().Contains(value, StringComparer.Ordinal);
    private static TriageResult Invalid() => TriageResult.InvalidResponse("ai_response_invalid");
}
