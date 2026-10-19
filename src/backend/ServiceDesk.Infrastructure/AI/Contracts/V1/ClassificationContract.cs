using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceDesk.Application.AIAssistance.Contracts;
using ServiceDesk.Domain.AIAssistance;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Infrastructure.AI.Contracts.V1;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ClassificationRequest(Guid RequestId, Guid TicketId, long TicketRevision, string InputHash, string Title, string Description);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ClassificationSuggestion(string Field, string Value, double? Score);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ClassificationResponse(Guid RequestId, Guid TicketId, long TicketRevision, string InputHash,
    IReadOnlyList<ClassificationSuggestion> Suggestions);

internal static class ClassificationContractMapper
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
                case "category" when suggestion.Value is not null && Categories.TryGetValue(suggestion.Value, out var c):
                    category = c; categoryScore = suggestion.Score; break;
                case "impact" when suggestion.Value is not null && Impacts.TryGetValue(suggestion.Value, out var i):
                    impact = i; impactScore = suggestion.Score; break;
                case "urgency" when suggestion.Value is not null && Urgencies.TryGetValue(suggestion.Value, out var u):
                    urgency = u; urgencyScore = suggestion.Score; break;
                default: return Invalid();
            }
        }
        return TriageResult.Success(category, impact, urgency, categoryScore, impactScore, urgencyScore);
    }

    // Versioned allowlists: adding a domain enum never silently expands the v1 protocol.
    private static readonly IReadOnlyDictionary<string, TicketCategory> Categories = new Dictionary<string, TicketCategory>(StringComparer.Ordinal)
    {
        ["ACC"] = TicketCategory.ACC, ["NET"] = TicketCategory.NET, ["VPN"] = TicketCategory.VPN,
        ["MAIL"] = TicketCategory.MAIL, ["SW"] = TicketCategory.SW, ["HW"] = TicketCategory.HW,
        ["PRINT"] = TicketCategory.PRINT, ["ACCESS"] = TicketCategory.ACCESS, ["EQUIP"] = TicketCategory.EQUIP,
        ["SEC"] = TicketCategory.SEC, ["OTHER"] = TicketCategory.OTHER
    };
    private static readonly IReadOnlyDictionary<string, Impact> Impacts = new Dictionary<string, Impact>(StringComparer.Ordinal)
    { ["Low"] = Impact.Low, ["Medium"] = Impact.Medium, ["High"] = Impact.High };
    private static readonly IReadOnlyDictionary<string, Urgency> Urgencies = new Dictionary<string, Urgency>(StringComparer.Ordinal)
    { ["Low"] = Urgency.Low, ["Medium"] = Urgency.Medium, ["High"] = Urgency.High };
    private static TriageResult Invalid() => TriageResult.InvalidResponse("ai_response_invalid");
}
